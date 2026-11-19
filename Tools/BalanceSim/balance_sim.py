#!/usr/bin/env python3
"""
Dark Fantasy Idle RPG - progression & economy balance simulation (design phase).

Pure standard library. Reads balance_config.json, simulates several player
behaviour profiles over N days, writes a Markdown report + CSV time series.

The formulas here are the *reference implementation* of Docs/GAME_BALANCE.md.
The C# core (Game.Core) must reproduce them; golden-value tests will compare
both implementations.

Usage:
    python3 balance_sim.py [--config balance_config.json] [--days 30] [--out results]
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import os
import random
from dataclasses import dataclass, field

HOUR = 3600.0
DAY = 24 * HOUR


# --------------------------------------------------------------------------
# Formulas (mirror GAME_BALANCE.md section numbers)
# --------------------------------------------------------------------------
class Formulas:
    def __init__(self, cfg: dict):
        self.c = cfg
        self.w, self.e, self.p = cfg["world"], cfg["enemy"], cfg["player"]
        self.x, self.g, self.ec = cfg["xp"], cfg["gear"], cfg["economy"]
        self.pr = cfg["prestige"]

    # 2.1 world layout
    def kills_to_advance(self, region: int) -> int:
        w = self.w
        return int(min(w["kills_to_advance_cap"],
                       w["kills_to_advance_base"] + w["kills_to_advance_per_region"] * (region - 1)))

    def subzone_level(self, region: int, sub: int) -> int:
        w = self.w
        z = (region - 1) * w["subzones_per_region"] + sub  # global subzone index, 1-based
        return (z - 1) * w["levels_per_subzone"] + 1

    def boss_level(self, region: int) -> int:
        return region * self.w["subzones_per_region"] * self.w["levels_per_subzone"]

    # 2.2 enemies
    def enemy_hp(self, lvl: int, boss=False) -> float:
        hp = self.e["hp_base"] * self.e["hp_growth"] ** (lvl - 1)
        return hp * (self.e["boss_hp_mult"] if boss else 1.0)

    def enemy_atk(self, lvl: int, boss=False) -> float:
        a = self.e["atk_base"] * self.e["atk_growth"] ** (lvl - 1)
        return a * (self.e["boss_atk_mult"] if boss else 1.0)

    # 2.3 experience
    def xp_to_next(self, level: int) -> float:
        x = self.x
        kills = x["kills_per_level_base"] + x["kills_per_level_per_level"] * level
        return x["enemy_xp_base"] * x["level_req_growth"] ** (level - 1) * kills

    def enemy_xp(self, enemy_lvl: int, player_lvl: int, boss=False) -> float:
        x = self.x
        xp = x["enemy_xp_base"] * x["enemy_xp_growth"] ** (enemy_lvl - 1)
        over = player_lvl - enemy_lvl
        if over > 5:
            xp *= max(x["overlevel_penalty_floor"], 1 - x["overlevel_penalty_per_level"] * (over - 5))
        return xp * (x["boss_xp_mult"] if boss else 1.0)

    # 2.5 defence
    def armour_k(self, enemy_lvl: int) -> float:
        return self.p["armour_k_base"] * self.p["armour_k_growth"] ** (enemy_lvl - 1)

    # 2.7 economy
    def gold_drop(self, lvl: int, boss=False) -> float:
        g = self.ec["gold_base"] * self.ec["gold_growth"] ** (lvl - 1)
        return g * (self.ec["boss_gold_mult"] if boss else 1.0)

    def upgrade_cost(self, ilvl: int, current_upgrade: int) -> float:
        return (self.ec["upgrade_cost_base"] * self.g["item_growth"] ** (ilvl - 1)
                * self.ec["upgrade_cost_growth"] ** current_upgrade)

    # 6.1 item stat lines (M3-4)
    def item_line(self, base: float, scaling: str, per_item_level: float, ilvl: int, rarity: int, upgrade: int) -> float:
        rm = self.g["rarity_base_mult"][rarity]
        up = 1 + self.g["upgrade_base_pct"] * upgrade
        if scaling == "exponential":
            return base * self.g["item_growth"] ** (ilvl - 1) * rm * up
        if scaling == "linear":
            return (base + per_item_level * ilvl) * rm * up
        if scaling == "rarity":
            return base * rm * up
        if scaling == "fixed":
            return base
        raise ValueError(scaling)

    # 2.8 prestige
    def dust_for_run(self, highest_boss_region: int) -> int:
        pr = self.pr
        if highest_boss_region < pr["min_region_boss"]:
            return 0
        return int(pr["dust_base"] * pr["dust_growth_per_region"] ** (highest_boss_region - pr["min_region_boss"]))

    def dust_for_prestige(self, highest_boss_region: int, prestiges_done: int) -> int:
        """Dust actually paid, including the first-prestiges bonus (D-037)."""
        dust = self.dust_for_run(highest_boss_region)
        if prestiges_done < self.pr["first_prestiges_count"]:
            dust = int(dust * self.pr["first_prestiges_dust_mult"])
        return dust

    def artifact_cost(self, name: str, level: int) -> float:
        if name == "haste":
            c = self.c["combat"]
            if level >= c["haste_max_level"]:
                return float("inf")
            return c["haste_cost_base"] * c["haste_cost_growth"] ** level
        a = self.pr["artifacts"][name]
        return a["cost_base"] * a["cost_growth"] ** level

    def artifact_mult(self, name: str, level: int) -> float:
        if name == "haste":  # additive attack speed, used as a multiplier on attack rate
            return 1 + self.c["combat"]["haste_per_level"] * level
        a = self.pr["artifacts"][name]
        return (1 + a["per_level"]) ** level


# --------------------------------------------------------------------------
# State
# --------------------------------------------------------------------------
@dataclass
class Item:
    slot: str
    ilvl: int
    rarity: int
    kind: str = "main_hand"
    type_id: str = "sword"
    attrs: list = field(default_factory=list)   # [(stat, value)] (M3-3)
    class_bonus: str = None                      # class whose +damage line is active (R2-6)
    upgrade: int = 0
    gold_spent: float = 0.0


@dataclass
class Meta:  # survives prestige
    dust: float = 0.0
    dust_total: float = 0.0
    artifacts: dict = field(default_factory=lambda: {"might": 0, "bulwark": 0, "greed": 0, "wisdom": 0, "haste": 0})
    scrolls_primary: int = 0
    prestiges: int = 0
    kills_by_subzone: dict = field(default_factory=dict)  # global subzone index -> kills (mastery)


@dataclass
class Run:  # reset on prestige
    level: int = 1
    xp: float = 0.0
    gold: float = 0.0
    materials: float = 0.0
    region: int = 1
    sub: int = 1
    kills_in_sub: int = 0
    highest_boss: int = 0
    gear: dict = field(default_factory=dict)  # slot_id -> Item
    stuck_since: float | None = None


ARMOUR_KINDS = ["helmet", "chest", "legs", "boots", "armguards", "gloves"]
# M3-1: 11 active slots (accessories and pet come later)
SLOT_IDS = ["main_hand0", "off_hand0"] + [k + "0" for k in ARMOUR_KINDS] + ["ring0", "ring1", "amulet0"]
GEAR_STATS = ["weapon_damage", "armour", "hp", "pct_damage", "primary", "crit_points", "crit_damage_points",
              "attack_speed", "counter", "pct_hp", "pct_armour", "vit", "gold_find", "luck", "drop_chance"]


def kind_of_slot(slot_id: str) -> str:
    return slot_id.rstrip("0123456789")


def type_group(kind: str) -> str:
    return "armour" if kind in ARMOUR_KINDS else kind


class Sim:
    def __init__(self, cfg: dict, profile: dict, seed: int):
        self.cfg, self.f = cfg, Formulas(cfg)
        self.profile = profile
        self.rng = random.Random(seed)
        self.meta, self.run = Meta(), self.new_run()
        self.t = 0.0           # wall-clock seconds
        self.active = True     # current period active or offline
        self.slot_ids = list(SLOT_IDS)
        sw = cfg["gear"]["slot_drop_weights"]
        self.kinds = ["main_hand", "off_hand"] + ARMOUR_KINDS + ["ring", "amulet"]
        self.kind_weights = [sw["main_hand"], sw["off_hand"]] + [sw["armour"]] * 6 + [2 * sw["jewellery"], sw["jewellery"]]
        self._stats = None
        self.log_rows = []
        self.events = []
        self.drops_by_rarity = [0] * 6
        self.gold_earned = 0.0
        self.gold_spent = 0.0
        self.boss_attempts = 0
        self.boss_fails = 0
        self.first_reach = {}  # region -> wall-clock seconds
        self.offline_reports = []
        self.scrolls_found = 0
        self.crafted = 0

    @staticmethod
    def new_run() -> Run:
        r = Run()
        r.gear["main_hand0"] = Item(slot="main_hand0", ilvl=1, rarity=0, kind="main_hand", type_id="sword")  # D-015
        return r

    # ---------------- stats ----------------
    def item_power(self, it: Item) -> dict:
        """Stats an item adds (M3-3/M3-4), mirroring C# ItemStats.Contributions."""
        out = {k: 0.0 for k in GEAR_STATS}
        ttype = next(t for t in self.cfg["items"]["types"][type_group(it.kind)] if t["id"] == it.type_id)
        for line in ttype["lines"]:
            out[line["stat"]] += self.f.item_line(line["base"], line["scaling"], line["per_item_level"],
                                                  it.ilvl, it.rarity, it.upgrade)
        for stat, value in it.attrs:
            out[stat] += value
        if it.class_bonus == "fighter":
            out["pct_damage"] += self.cfg["items"]["class_bonus_pct_damage"]
        return out

    def compute_stats(self, gear=None) -> dict:
        p, f = self.cfg["player"], self.f
        gear = self.run.gear if gear is None else gear
        lvl = self.run.level
        auto = p["points_per_level_auto"] * (lvl - 1)
        manual = p["points_per_level_manual"] * (lvl - 1)
        # Fighter template: 50% STR, 33% VIT, 17% DEF of auto points.
        # Fighter template per level (D-014 + 5b): STR 1.5, VIT 1.0, DEF 0.25, COUNTER 0.25
        primary = auto * 0.5 + self.meta.scrolls_primary
        vit = auto / 3
        dfn = auto / 12
        counter_pts = auto / 12
        crit_pts = 0.0
        alloc = self.profile.get("manual_alloc", "primary")
        if alloc == "primary":
            primary += manual
        else:  # balanced optimiser: 50% primary, 25% crit, 25% vit
            primary += manual * 0.5
            crit_pts += manual * 0.25
            vit += manual * 0.25
        tot = {k: 0.0 for k in GEAR_STATS}
        for it in gear.values():
            for k, v in self.item_power(it).items():
                tot[k] += v
        crit_pts += tot["crit_points"]
        primary += tot["primary"]
        vit += tot["vit"]
        counter_pts += tot["counter"]
        art = self.meta.artifacts
        dmg_mult = (1 + primary * p["dmg_per_primary_pct"]) * (1 + tot["pct_damage"]) * f.artifact_mult("might", art["might"])
        cb = self.cfg["combat"]
        base_iv = cb["base_interval_fighter"]
        class_dmg_scale = base_iv / cb["reference_interval_s"]  # keeps early TTK unchanged
        hit_dmg = max(3.0, tot["weapon_damage"]) * dmg_mult * class_dmg_scale
        attack_speed = max(-0.9, self.f.artifact_mult("haste", art["haste"]) - 1 + tot["attack_speed"])
        interval = max(cb["attack_interval_floor_s"], base_iv / (1 + attack_speed))
        cc = min(p["crit_chance_cap"], p["base_crit_chance"] + crit_pts * p["crit_chance_per_point"])
        cd = p["base_crit_damage"] + crit_pts * p["crit_damage_per_point"] * 0.5 \
            + tot["crit_damage_points"] * p["crit_damage_per_point"]
        avg_hit = hit_dmg * (1 + cc * (cd - 1))
        hp = (p["base_hp"] + tot["hp"]) * (1 + vit * p["hp_per_vit_pct"]) * (1 + tot["pct_hp"]) \
            * f.artifact_mult("bulwark", art["bulwark"])
        armour = tot["armour"] * (1 + dfn * p["armour_pct_per_def"]) * (1 + tot["pct_armour"])
        counter = min(p["counter_cap"], counter_pts * p["counter_chance_per_point"])
        return {"avg_hit": avg_hit, "hp": hp, "armour": armour, "interval": interval,
                "hit": p["base_hit_chance"], "cc": cc, "cd": cd, "counter": counter,
                "gold_find": tot["gold_find"], "luck": tot["luck"], "drop_chance": tot["drop_chance"]}

    @property
    def stats(self):
        if self._stats is None:
            self._stats = self.compute_stats()
        return self._stats

    def invalidate(self):
        self._stats = None

    # ---------------- combat (expected value) ----------------
    def fight(self, enemy_lvl: int, boss=False, stats=None):
        """Returns (win, seconds). Expected-value model, validated by combat_check()."""
        s = stats or self.stats
        e, p, f = self.cfg["enemy"], self.cfg["player"], self.f
        var = self.rng.uniform(e["variance_mult_min"], e["variance_mult_max"])
        ehp = f.enemy_hp(enemy_lvl, boss) * var
        swings = math.ceil(ehp / s["avg_hit"]) / s["hit"]
        # Counter (5b): each enemy attack has s["counter"] chance of a free 60% hit.
        main_dps = s["avg_hit"] * s["hit"] / s["interval"]
        counter_dps = s["counter"] * p["counter_damage"] * s["avg_hit"] * s["hit"] / e["attack_interval_s"]
        ttk = swings * s["interval"] / (1 + counter_dps / main_dps)
        dr = min(p["damage_reduction_cap"], s["armour"] / (s["armour"] + f.armour_k(enemy_lvl)))
        edps = f.enemy_atk(enemy_lvl, boss) * (1 - dr) / e["attack_interval_s"]
        ttd = s["hp"] / edps
        if not boss:  # round 2, item 1c: no death vs normal enemies; enemies escape after a timer
            esc = self.cfg["combat"]["escape_timer_s"]
            if ttk <= esc:
                return True, ttk + p["between_fight_s"]
            return False, esc + p["between_fight_s"]
        limit = e["boss_time_limit_s"]
        if ttk < ttd and ttk <= limit:
            return True, ttk + p["between_fight_s"]
        return False, min(ttd, limit) + p["between_fight_s"]

    # ---------------- rewards ----------------
    def gain_xp(self, amount: float):
        r = self.run
        r.xp += amount * self.f.artifact_mult("wisdom", self.meta.artifacts["wisdom"])
        while r.xp >= self.f.xp_to_next(r.level):
            r.xp -= self.f.xp_to_next(r.level)
            r.level += 1
            self.invalidate()

    def gain_gold(self, amount: float):
        amount *= self.f.artifact_mult("greed", self.meta.artifacts["greed"])
        self.run.gold += amount
        self.gold_earned += amount

    def roll_rarity(self, min_rarity=0, luck=0.0) -> int:
        w = self.cfg["gear"]["rarity_weights"][:]
        mult = 1 + luck / (luck + self.cfg["combat"]["luck_k"]) if luck > 0 else 1.0
        for i in range(len(w)):
            w[i] = 0 if i < min_rarity else w[i] * (mult if i > 0 else 1.0)
        return self.rng.choices(range(6), weights=w)[0]

    def craft(self):
        """Crafting (GDD 6.6): spend materials for an item of a chosen slot at the frontier
        item level. Policy: improve the equipped slot with the lowest item level."""
        ec, r = self.cfg["economy"], self.run
        ilvl = self.f.subzone_level(r.region, r.sub)
        while True:
            cost = ec["craft_cost_base"] + ec["craft_cost_per_ilvl"] * ilvl
            if r.materials < cost:
                return
            worst = min(self.slot_ids, key=lambda sid: r.gear[sid].ilvl if sid in r.gear else -1)
            if worst in r.gear and r.gear[worst].ilvl >= ilvl:
                return
            r.materials -= cost
            self.crafted += 1
            it = self.make_item(ilvl, self.roll_rarity(0, self.stats["luck"]), kind=kind_of_slot(worst))
            it.slot = worst
            self.consider_item(it)

    def make_item(self, ilvl: int, rarity: int, kind: str = None) -> Item:
        """Mirrors C# LootSystem.Roll (kind weights, type, attributes, class line)."""
        it_cfg = self.cfg["items"]
        if kind is None:
            kind = self.rng.choices(self.kinds, weights=self.kind_weights)[0]
        types = it_cfg["types"][type_group(kind)]
        ttype = types[int(self.rng.random() * len(types))]
        candidates = [a for a in it_cfg["attributes"] if kind in a["kinds"]]
        attrs = []
        for _ in range(self.cfg["gear"]["random_affixes"][rarity]):
            if not candidates:
                break
            a = candidates.pop(int(self.rng.random() * len(candidates)))
            q = self.rng.random() ** (1.0 / (1.0 + it_cfg["rarity_roll_bias"] * rarity))
            attrs.append((a["stat"], a["min"] + (a["max"] - a["min"]) * q + a["per_item_level"] * ilvl))
        class_bonus = None
        if self.rng.random() < it_cfg["class_item_share"]:
            class_bonus = self.rng.choice(["fighter", "archer", "mage"])
        slot = "ring0" if kind == "ring" else kind + "0"
        return Item(slot=slot, ilvl=ilvl, rarity=rarity, kind=kind, type_id=ttype["id"], attrs=attrs, class_bonus=class_bonus)

    def score(self, stats: dict) -> float:
        # power proxy: damage x survivability (sqrt to keep both relevant)
        return stats["avg_hit"] * math.sqrt(stats["hp"] * (1 + stats["armour"] / 100))

    def consider_item(self, it: Item):
        if it.kind == "ring":  # two ring slots: try the one where the swap helps most
            best = None
            for slot in ("ring0", "ring1"):
                trial = dict(self.run.gear)
                trial[slot] = Item(**{**it.__dict__, "slot": slot})
                sc = self.score(self.compute_stats(trial))
                if best is None or sc > best[0] or slot not in self.run.gear:
                    best = (sc, slot)
                    if slot not in self.run.gear:
                        break
            it.slot = best[1]
        cur = self.run.gear.get(it.slot)
        trial = dict(self.run.gear)
        trial[it.slot] = Item(**{**it.__dict__})
        if cur is None or self.score(self.compute_stats(trial)) > self.score(self.stats):
            if cur is not None:
                self.salvage(cur)
            self.run.gear[it.slot] = it
            self.invalidate()
        else:
            self.salvage(it)

    def salvage(self, it: Item):
        self.run.materials += self.cfg["economy"]["material_per_salvage"][it.rarity]
        refund = it.gold_spent * self.cfg["economy"]["salvage_upgrade_refund"]
        self.run.gold += refund

    def spend_gold(self):
        """Buy cheapest available upgrades on equipped items."""
        g, f = self.cfg["gear"], self.f
        changed = False
        while True:
            best, best_cost = None, None
            for it in self.run.gear.values():
                if it.upgrade >= g["upgrade_max_by_rarity"][it.rarity]:
                    continue
                c = f.upgrade_cost(it.ilvl, it.upgrade)
                if best_cost is None or c < best_cost:
                    best, best_cost = it, c
            if best is None or best_cost > self.run.gold:
                break
            self.run.gold -= best_cost
            self.gold_spent += best_cost
            best.upgrade += 1
            best.gold_spent += best_cost
            changed = True
        if changed:
            self.invalidate()

    # ---------------- one action ----------------
    def step(self, can_manage: bool, pending: list) -> float:
        """Performs one fight. Returns seconds of *simulated game time* consumed."""
        r, f = self.run, self.f
        kreq = f.kills_to_advance(r.region)
        boss_ready = r.sub == self.cfg["world"]["subzones_per_region"] and r.kills_in_sub >= kreq
        if boss_ready:
            self.boss_attempts += 1
            win, sec = self.fight(f.boss_level(r.region), boss=True)
            if win:
                self.on_boss_kill(r.region, can_manage, pending)
            else:
                # R2-2 (owner change): the boss returns after a new wave of kills in the last subzone
                self.boss_fails += 1
                frac = self.cfg["combat"]["boss_retry_kill_fraction"]
                r.kills_in_sub = kreq - math.ceil(kreq * frac)
                self._farm_back_until = self.t + self.cfg["simulation"]["farmer_farm_back_s"]
                if r.stuck_since is None:
                    r.stuck_since = self.t
            return sec

        # choose farm location
        region, sub = r.region, r.sub
        farm_back = self.profile.get("farm_back_subzones", 0)
        if r.stuck_since is not None and farm_back and self.t < getattr(self, "_farm_back_until", 0):
            z = max(1, (region - 1) * 5 + sub - farm_back)
            region, sub = (z - 1) // 5 + 1, (z - 1) % 5 + 1
        lvl = f.subzone_level(region, sub)
        win, sec = self.fight(lvl)
        if not win:
            # enemy escaped; after N escapes in a row retreat one subzone (round 2, item 1c)
            self._escapes = getattr(self, "_escapes", 0) + 1
            if self._escapes >= self.cfg["combat"]["escapes_before_retreat"]:
                self._escapes = 0
                if r.stuck_since is None:
                    r.stuck_since = self.t
                if r.sub > 1:
                    r.sub -= 1
                    r.kills_in_sub = kreq
            return sec
        self._escapes = 0
        self.on_kill(lvl, region == r.region and sub == r.sub, can_manage, pending)
        return sec

    def mastery_bonus(self) -> tuple:
        m = self.cfg["mastery"]
        stars = sum(min(m["max_stars"], k // m["kills_per_star"]) for k in self.meta.kills_by_subzone.values())
        return stars * m["gold_per_star"], stars * m["drop_per_star"]

    def on_kill(self, lvl, at_frontier, can_manage, pending):
        r, f, g = self.run, self.f, self.cfg["gear"]
        z = (lvl - 1) // self.cfg["world"]["levels_per_subzone"] + 1
        kb = self.meta.kills_by_subzone
        kb[z] = kb.get(z, 0) + 1
        if kb[z] % self.cfg["mastery"]["kills_per_star"] == 0:
            self._mastery = None
        if getattr(self, "_mastery", None) is None:
            self._mastery = self.mastery_bonus()
        m_gold, m_drop = self._mastery
        self.gain_xp(f.enemy_xp(lvl, r.level))
        st = self.stats
        self.gain_gold(f.gold_drop(lvl) * (1 + m_gold) * (1 + st["gold_find"]))
        chance = g["drop_chance_normal"] * (1 + m_drop) * (1 + st["drop_chance"])
        n_drops = int(chance) + (1 if self.rng.random() < chance - int(chance) else 0)
        for _ in range(n_drops):
            it = self.make_item(lvl, self.roll_rarity(0, st["luck"]))
            self.drops_by_rarity[it.rarity] += 1
            (self.consider_item(it) if can_manage else pending.append(it))
        if self.rng.random() < self.cfg["economy"]["scroll_drop_chance"]:
            self.meta.scrolls_primary += 1
            self.scrolls_found += 1
            self.invalidate()
        if at_frontier:
            r.kills_in_sub += 1
            if r.kills_in_sub >= f.kills_to_advance(r.region) and r.sub < self.cfg["world"]["subzones_per_region"]:
                r.sub += 1
                r.kills_in_sub = 0
                r.stuck_since = None

    def on_boss_kill(self, region, can_manage, pending):
        r, f, g = self.run, self.f, self.cfg["gear"]
        lvl = f.boss_level(region)
        self.gain_xp(f.enemy_xp(lvl, r.level, boss=True))
        self.gain_gold(f.gold_drop(lvl, boss=True))
        # M3-2: Rare+ guaranteed on the first kill of each boss per run (the sim kills each boss once per run)
        it = self.make_item(lvl, self.roll_rarity(g["boss_min_rarity"], self.stats["luck"]))
        self.drops_by_rarity[it.rarity] += 1
        (self.consider_item(it) if can_manage else pending.append(it))
        r.highest_boss = max(r.highest_boss, region)
        r.region, r.sub, r.kills_in_sub, r.stuck_since = region + 1, 1, 0, None
        self.first_reach.setdefault(region + 1, self.t)
        self.events.append((self.t, f"boss {region} defeated (prestige {self.meta.prestiges}, lvl {r.level})"))

    # ---------------- prestige ----------------
    def maybe_prestige(self):
        pol = self.profile.get("prestige", True)
        if not pol:
            return
        r = self.run
        hrs = self.cfg["simulation"]["prestige_when_stuck_hours"]
        if r.stuck_since is None or self.t - r.stuck_since < hrs * HOUR:
            return
        dust = self.f.dust_for_prestige(r.highest_boss, self.meta.prestiges)
        # rational player: only reset when it adds a meaningful share of all dust ever earned
        min_share = self.cfg["simulation"]["prestige_min_dust_share"]
        if dust <= 0 or dust < min_share * self.meta.dust_total:
            return
        self.meta.dust += dust
        self.meta.dust_total += dust
        self.meta.prestiges += 1
        self.events.append((self.t, f"PRESTIGE #{self.meta.prestiges}: boss {r.highest_boss}, lvl {r.level}, +{dust} dust"))
        self.run = self.new_run()
        self.buy_artifacts()
        self.invalidate()

    def buy_artifacts(self):
        m, f = self.meta, self.f
        while True:
            costs = {a: f.artifact_cost(a, m.artifacts[a]) for a in m.artifacts}
            core = min(("might", "bulwark"), key=lambda a: costs[a])
            # utility artifacts only when no more expensive than the cheaper core artifact
            util = min(("greed", "wisdom", "haste"), key=lambda a: costs[a])
            pick = util if costs[util] <= costs[core] else core
            if costs[pick] > m.dust:
                break
            m.dust -= costs[pick]
            m.artifacts[pick] += 1

    # ---------------- time driving ----------------
    def simulate_period(self, seconds: float, active: bool):
        """Active: real-time fights with item management. Offline: capped+reduced
        efficiency, loot held until return (offline report)."""
        pending = []
        if active:
            sim_seconds = seconds
            mult = 1.0
        else:
            cap = self.cfg["offline"]["cap_hours"] * HOUR
            sim_seconds = min(seconds, cap) * self.cfg["offline"]["efficiency"]
            mult = seconds / sim_seconds if sim_seconds > 0 else 1.0  # map game time to wall time
        start_t = self.t
        start = (self.run.region, self.run.level, self.gold_earned)
        elapsed = 0.0
        if active:
            self.craft()
            self.spend_gold()
        while elapsed < sim_seconds:
            sec = self.step(can_manage=active, pending=pending)
            elapsed += sec
            self.t = start_t + elapsed * (mult if not active else 1.0)
            if active:
                self.maybe_prestige()
                if self.profile.get("upgrade_continuously", False):
                    self.craft()
                    self.spend_gold()
        self.t = start_t + seconds
        if not active:
            self.offline_reports.append({
                "hours": seconds / HOUR, "items": len(pending),
                "regions_gained": self.run.region - start[0], "levels_gained": self.run.level - start[1],
                "gold": self.gold_earned - start[2]})
            for it in pending:
                self.consider_item(it)
            self.craft()
            self.spend_gold()
            self.maybe_prestige()

    def snapshot(self, day: float):
        r, m = self.run, self.meta
        s = self.stats
        self.log_rows.append({
            "day": round(day, 2), "prestiges": m.prestiges, "region": r.region, "sub": r.sub,
            "highest_boss_run": r.highest_boss, "level": r.level,
            "avg_hit": round(s["avg_hit"], 1), "hp": round(s["hp"], 1),
            "dust_total": int(m.dust_total), "might": m.artifacts["might"], "bulwark": m.artifacts["bulwark"],
            "greed": m.artifacts["greed"], "wisdom": m.artifacts["wisdom"], "scrolls": m.scrolls_primary,
            "avg_ilvl": round(sum(i.ilvl for i in r.gear.values()) / max(1, len(r.gear)), 1),
            "gear_slots": len(r.gear),
        })

    def run_schedule(self, days: int):
        sessions = self.profile["sessions"]  # list of (start_hour, duration_minutes)
        cursor = sessions[0][0] * HOUR  # the game starts at the first session
        self.t = cursor
        for d in range(days):
            for start_h, dur_m in sessions:
                st = d * DAY + start_h * HOUR
                if st > cursor:
                    self.simulate_period(st - cursor, active=False)
                self.simulate_period(dur_m * 60.0, active=True)
                cursor = st + dur_m * 60.0
            self.snapshot(d + 1)


# --------------------------------------------------------------------------
# Profiles (Prompt 3: behaviours to test)
# --------------------------------------------------------------------------
PROFILES = {
    "active": {"desc": "4 x 45 min sessions/day, upgrades continuously, prestiges",
               "sessions": [(8, 45), (13, 45), (18, 45), (22, 45)], "upgrade_continuously": True},
    "casual": {"desc": "3 x 10 min check-ins/day, upgrades only at login",
               "sessions": [(8, 10), (13, 10), (21, 10)]},
    "offline_focused": {"desc": "2 x 3 min check-ins/day (12h gaps), rest offline",
                        "sessions": [(8, 3), (20, 3)]},
    "farmer": {"desc": "Like casual, but farms 5 subzones back when stuck",
               "sessions": [(8, 10), (13, 10), (21, 10)], "farm_back_subzones": 5},
    "optimiser": {"desc": "Like active + balanced manual stat allocation",
                  "sessions": [(8, 45), (13, 45), (18, 45), (22, 45)], "upgrade_continuously": True,
                  "manual_alloc": "balanced"},
    "never_prestige": {"desc": "Like active but never prestiges (shows the wall)",
                       "sessions": [(8, 45), (13, 45), (18, 45), (22, 45)], "upgrade_continuously": True,
                       "prestige": False},
}


# --------------------------------------------------------------------------
# Combat validation: tick-based stochastic fight vs expected-value model
# --------------------------------------------------------------------------
def combat_check(cfg: dict, trials=4000, seed=7):
    f = Formulas(cfg)
    rng = random.Random(seed)
    rows = []
    for avg_hit_ratio in (0.15, 0.35, 0.6, 1.2):  # player hit as share of enemy HP
        lvl = 20
        ehp = f.enemy_hp(lvl)
        p = cfg["player"]
        cc, cd, hit, interval = 0.15, 1.6, p["base_hit_chance"], p["base_attack_interval_s"]
        base_hit = ehp * avg_hit_ratio / (1 + cc * (cd - 1))
        total = 0.0
        for _ in range(trials):
            hp, t = ehp, 0.0
            while hp > 0:
                t += interval
                if rng.random() < hit:
                    hp -= base_hit * (cd if rng.random() < cc else 1.0)
            total += t
        mc = total / trials
        ev = math.ceil(ehp / (base_hit * (1 + cc * (cd - 1)))) / hit * interval
        rows.append((avg_hit_ratio, mc, ev, (ev - mc) / mc * 100))
    return rows


# --------------------------------------------------------------------------
# Report
# --------------------------------------------------------------------------
def export_golden(cfg: dict, path: str):
    """Golden values for the C# formula tests (TESTING_STRATEGY section 5)."""
    f = Formulas(cfg)
    levels = [1, 2, 10, 50, 100, 250, 500]
    regions = list(range(1, 31))
    rows = []
    add = lambda fn, args, v: rows.append({"fn": fn, "args": list(args), "value": v})
    for r in regions:
        add("KillsToAdvance", [r], f.kills_to_advance(r))
        add("BossLevel", [r], f.boss_level(r))
        for sub in (1, 3, 5):
            add("SubzoneLevel", [r, sub], f.subzone_level(r, sub))
        add("DustForRun", [r], f.dust_for_run(r))
        for done in (0, 2, 3, 10):
            add("DustForPrestige", [r, done], f.dust_for_prestige(r, done))
    for L in levels:
        for boss in (False, True):
            add("EnemyHp", [L, boss], f.enemy_hp(L, boss))
            add("EnemyAtk", [L, boss], f.enemy_atk(L, boss))
            add("GoldDrop", [L, boss], f.gold_drop(L, boss))
            for pl in (L, L + 10, L + 40):
                add("EnemyXp", [L, pl, boss], f.enemy_xp(L, pl, boss))
        add("XpToNext", [L], f.xp_to_next(L))
        add("ArmourK", [L], f.armour_k(L))
        for up in (0, 5, 20):
            add("UpgradeCost", [L, up], f.upgrade_cost(L, up))
    for name in list(cfg["prestige"]["artifacts"].keys()) + ["haste"]:
        for lvl in (0, 1, 5, 20, 39, 40):
            add("ArtifactCost", [name, lvl], f.artifact_cost(name, lvl))
            add("ArtifactMult", [name, lvl], f.artifact_mult(name, lvl))
    for group, types in cfg["items"]["types"].items():
        for t in types:
            for i, line in enumerate(t["lines"]):
                for ilvl in (1, 11, 60, 250):
                    for rarity in (0, 2, 5):
                        for up in (0, 3):
                            add("ItemLine", [group, t["id"], i, ilvl, rarity, up],
                                f.item_line(line["base"], line["scaling"], line["per_item_level"], ilvl, rarity, up))
    with open(path, "w") as fh:
        json.dump(rows, fh, indent=1, allow_nan=True)
    print(f"golden values: {len(rows)} rows -> {path}")


def fmt_t(sec):
    if sec is None:
        return "-"
    h = sec / HOUR
    return f"{h:.1f} h" if h < 48 else f"{h / 24:.1f} d"


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default=os.path.join(here, "balance_config.json"))
    ap.add_argument("--days", type=int, default=None)
    ap.add_argument("--out", default=os.path.join(here, "results"))
    ap.add_argument("--golden", default=None, help="write golden formula values to this path and exit")
    args = ap.parse_args()
    with open(args.config) as fh:
        cfg = json.load(fh)
    if args.golden:
        export_golden(cfg, args.golden)
        return
    days = args.days or cfg["simulation"]["days"]
    os.makedirs(args.out, exist_ok=True)
    f = Formulas(cfg)

    lines = ["# Balance Simulation Report", "",
             f"Config: `{os.path.basename(args.config)}` · {days} simulated days · seed {cfg['simulation']['seed']}",
             "", "Generated by `Tools/BalanceSim/balance_sim.py`. Do not edit by hand.", ""]

    # Static curves
    lines += ["## 1. Static curves", "",
              "| Region | Boss lvl | Kills/subzone | Enemy HP (sub 1) | Boss HP | Enemy ATK | XP to next (at boss lvl) | Gold/kill | Dust if prestiged here |",
              "|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for reg in (1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 50, 100):
        bl = f.boss_level(reg)
        l1 = f.subzone_level(reg, 1)
        lines.append(f"| {reg} | {bl} | {f.kills_to_advance(reg)} | {f.enemy_hp(l1):.3g} | {f.enemy_hp(bl, True):.3g} | "
                     f"{f.enemy_atk(l1):.3g} | {f.xp_to_next(bl):.3g} | {f.gold_drop(l1):.3g} | {f.dust_for_run(reg)} |")
    lines += ["", "## 2. Combat model validation (expected-value vs 4 000 stochastic fights)", "",
              "| Avg hit / enemy HP | Monte-Carlo TTK (s) | Model TTK (s) | Error |", "|---:|---:|---:|---:|"]
    for ratio, mc, ev, err in combat_check(cfg):
        lines.append(f"| {ratio:.2f} | {mc:.2f} | {ev:.2f} | {err:+.1f}% |")

    lines += ["", "## 3. Profiles", ""]
    summary = ["| Profile | Prestiges | Region (current run) | Best boss ever | Level | Dust total | Artifacts M/B/G/W/H | Scrolls | Boss fail rate |",
               "|---|---:|---:|---:|---:|---:|---|---:|---:|"]
    detail = []
    for name, prof in PROFILES.items():
        sim = Sim(cfg, prof, cfg["simulation"]["seed"])
        sim.run_schedule(days)
        m, r = sim.meta, sim.run
        best = max([int(e[1].split()[1]) for e in sim.events if e[1].startswith("boss")] or [0])
        a = m.artifacts
        fail = sim.boss_fails / max(1, sim.boss_attempts) * 100
        summary.append(f"| {name} | {m.prestiges} | {r.region} | {best} | {r.level} | {int(m.dust_total)} | "
                       f"{a['might']}/{a['bulwark']}/{a['greed']}/{a['wisdom']}/{a['haste']} | {m.scrolls_primary} | {fail:.0f}% |")
        with open(os.path.join(args.out, f"timeseries_{name}.csv"), "w", newline="") as fh:
            w = csv.DictWriter(fh, fieldnames=list(sim.log_rows[0].keys()), lineterminator="\n")
            w.writeheader()
            w.writerows(sim.log_rows)
        detail += [f"### {name}", "", f"_{prof['desc']}_", ""]
        detail += ["| Day | Prestiges | Region | Level | Avg item lvl | Dust total |", "|---:|---:|---:|---:|---:|---:|"]
        for row in sim.log_rows:
            if row["day"] in (1, 2, 3, 5, 7, 10, 14, 21, 30) or row["day"] == days:
                detail.append(f"| {row['day']:g} | {row['prestiges']} | {row['region']} | {row['level']} | "
                              f"{row['avg_ilvl']} | {row['dust_total']} |")
        prest = [e for e in sim.events if "PRESTIGE" in e[1]][:6]
        if prest:
            detail += ["", "First prestiges:", ""] + [f"- {fmt_t(t)}: {msg}" for t, msg in prest]
        firsts = [f"R{k}: {fmt_t(v)}" for k, v in sorted(sim.first_reach.items()) if k in (2, 3, 4, 5, 6, 8, 10, 12, 15, 20)]
        detail += ["", "First time reaching region (any run): " + ", ".join(firsts)]
        tot_drops = sum(sim.drops_by_rarity)
        dist = ", ".join(f"{n} {c} ({c / max(1, tot_drops) * 100:.2f}%)"
                         for n, c in zip(cfg["gear"]["rarity_names"], sim.drops_by_rarity))
        detail += ["", f"Drops: {tot_drops} — {dist}",
                   f"Gold earned {sim.gold_earned:.3g}, spent on upgrades {sim.gold_spent:.3g} "
                   f"({sim.gold_spent / max(1, sim.gold_earned) * 100:.0f}%), items crafted {sim.crafted}"]
        if sim.offline_reports:
            capped = [o for o in sim.offline_reports if o["hours"] > cfg["offline"]["cap_hours"]]
            detail += [f"Offline periods: {len(sim.offline_reports)}, of which {len(capped)} exceeded the "
                       f"{cfg['offline']['cap_hours']:g} h cap"]
        detail.append("")
    lines += summary + [""] + detail
    out_md = os.path.join(args.out, "REPORT.md")
    with open(out_md, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(summary))
    print(f"\nReport written to {out_md}")


if __name__ == "__main__":
    main()
