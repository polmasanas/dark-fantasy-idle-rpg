"""
M6 skills for the balance simulation (spec Docs/specs/2026-11-20-m6-skills-design.md).

Mirrors the C# rules where they are exact (points, tier gates, auto-spend order, stat passives, cooldowns) and
models combat as expected values: each active replaces one attack every cooldown (cooldowns carry between
fights, M6-2), buffs and slows by uptime, damage over time by its total. Cross-checked against the real engine
(Tools/BalanceSim.CSharp "Skills" table) by skill_gain_table().
"""
import json
import math
import os

STAT_KEYS = ("pct_hp", "pct_armour", "attack_speed", "crit_chance", "crit_damage", "counter_chance",
             "pct_elemental", "pct_damage")


def load_skills(path=None):
    path = path or os.path.join(os.path.dirname(os.path.abspath(__file__)), "skills_config.json")
    with open(path) as fh:
        d = json.load(fh)
    d["by_id"] = {n["id"]: n for n in d["nodes"]}
    return d


def at(effect, field, rank):
    return effect.get(field, 0.0) + effect.get("per_rank", {}).get(field, 0.0) * (rank - 1)


def auto_spend(cfg, sk, hero, level):
    """Port of SkillTree.AutoSpend: returns (ranks, loadout)."""
    earned = level // cfg["combat"]["skill_point_every_levels"]
    tier_points = cfg["skill_rules"]["tier_points"]
    slots = cfg["combat"]["active_skill_slots"]
    ranks, loadout = {}, []
    order = sk["suggested_order"].get(hero, [])
    progress = True
    while sum(ranks.values()) < earned and progress:
        progress = False
        mentions = {}
        for nid in order:
            mentions[nid] = mentions.get(nid, 0) + 1
            node = sk["by_id"][nid]
            r = ranks.get(nid, 0)
            if r >= mentions[nid] or r >= node["max_rank"]:
                continue
            if sum(ranks.values()) < (node["tier"] - 1) * tier_points:
                continue
            ranks[nid] = r + 1
            if r == 0 and node["kind"] == "active" and len(loadout) < slots:
                loadout.append(nid)
            progress = True
            break
    return ranks, loadout


def stat_bonuses(sk, ranks):
    out = {k: 0.0 for k in STAT_KEYS}
    for nid, r in ranks.items():
        for e in sk["by_id"][nid]["effects"]:
            if e["kind"] == "stat" and e["stat"] in out:
                out[e["stat"]] += at(e, "value", r)
    return out


def kit(cfg, sk, ranks, loadout):
    """Combat passives and actives (with final cooldowns), like SkillBuild.Kit."""
    k = {"dodge": 0.0, "damage_taken": 0.0, "boss_damage_taken": 0.0, "skill_crit_damage": 0.0, "lifesteal": 0.0,
         "heal_below": 0.0, "cheat_death": 0.0, "momentum": 0.0, "momentum_max": 0.0, "echo": 0.0,
         "conditional": [], "actives": []}
    cdr = 0.0
    for nid, r in ranks.items():
        for e in sk["by_id"][nid]["effects"]:
            if e["kind"] == "stat":
                v = at(e, "value", r)
                if e["stat"] == "cooldown_reduction":
                    cdr += v
                elif e["stat"] in k:
                    k[e["stat"]] += v
            elif e["kind"] == "conditional_damage":
                k["conditional"].append((nid, e["condition"], at(e, "value", r), at(e, "threshold", r)))
            elif e["kind"] == "trigger":
                v = at(e, "value", r)
                t = e["trigger"]
                if t == "heal_below":
                    k["heal_below"] += v
                elif t == "cheat_death":
                    k["cheat_death"] += v
                elif t == "lifesteal":
                    k["lifesteal"] += v
                elif t == "momentum":
                    k["momentum"] += v
                    k["momentum_max"] = at(e, "max_stacks", r)
                elif t == "echo":
                    k["echo"] += v
    rules = cfg["skill_rules"]
    k["dodge"] = min(rules["hero_dodge_cap"], max(0.0, k["dodge"]))
    cut = min(rules["cooldown_reduction_cap"], max(0.0, cdr))
    for nid in loadout:
        node = sk["by_id"][nid]
        r = ranks[nid]
        cd = node["cooldown_s"] * max(0.0, 1 - node["rank_cdr"] * (r - 1)) * (1 - cut)
        k["actives"].append((node, r, cd))
    return k


def conditional_mult(k, ep, boss, hp_share_above=0.7):
    """Largest matching bonus per node, summed. 'own_hp_above' is weighted by an assumed uptime."""
    best = {}
    for nid, cond, value, _thr in k["conditional"]:
        w = 0.0
        if cond == "vs_boss":
            w = 1.0 if boss else 0.0
        elif cond == "own_hp_above":
            w = hp_share_above
        elif cond.startswith("vs_family:"):
            w = 1.0 if ep["family"] == cond.split(":", 1)[1] else 0.0
        elif cond.startswith("vs_archetype:"):
            w = 1.0 if ep["archetype"] == cond.split(":", 1)[1] else 0.0
        best[nid] = max(best.get(nid, 0.0), value * w)
    return 1 + sum(best.values())


def fight_mods(cfg, k, s, ep, boss, rel_elem, hit, fight_s):
    """
    Returns (dps_mult, taken_mult, hp_mult, lifesteal, other_hits_mult) for one enemy profile; other_hits_mult is
    what buffs and conditional passives add to hits that are not attacks (Counter).
    rel_elem(element) = expected hit multiplier with that element replacing Physical / the normal one.
    s: stats (cc, cd, interval). hit: hit chance. fight_s: expected fight length (for once-per-fight skills).
    """
    cc, cd, iv = s["cc"], s["cd"], s["interval"]
    crit_avg = 1 + cc * (cd - 1)
    buff = 1.0
    slow_share = 0.0
    value_sum, rate_sum = 0.0, 0.0
    for node, r, cooldown in k["actives"]:
        # A skill fires only on an attack: its real cycle is the cooldown rounded up to whole attacks.
        rate = 1.0 / max(iv, math.ceil(cooldown / iv - 1e-9) * iv)
        value = 0.0
        for e in node["effects"]:
            kind = e["kind"]
            el = e.get("element", "physical")
            if kind == "hit":
                crit = (cd + k["skill_crit_damage"]) / crit_avg if e.get("always_crit") else \
                    (1 + cc * (cd + k["skill_crit_damage"] - 1)) / crit_avg
                miss = 1.0 / hit if e.get("cannot_miss") else 1.0
                value += e.get("hits", 1) * at(e, "percent", r) * rel_elem(el) * crit * miss
                if e.get("condition") == "enemy_hp_below":
                    rate = min(rate, 1.0 / max(fight_s, 1e-9)) if not boss else rate
            elif kind == "dot":
                value += at(e, "percent", r) * rel_elem(el) / crit_avg / hit
            elif kind == "buff":
                buff *= 1 + at(e, "value", r) * min(1.0, at(e, "duration", r) / cooldown)
            elif kind == "enemy_slow":
                v = at(e, "value", r)
                slow_share += min(1.0, at(e, "duration", r) / cooldown) * (1 - 1 / (1 + v))
        value *= 1 + k["echo"]
        rate_sum += rate
        value_sum += rate * value
    attacks = 1.0 / iv
    if rate_sum > attacks:  # more casts than attacks: scale down
        value_sum *= attacks / rate_sum
        rate_sum = attacks
    dps_mult = ((attacks - rate_sum) + value_sum) / attacks
    momentum = 1 + k["momentum"] * k["momentum_max"] * 0.8
    other = buff * conditional_mult(k, ep, boss)
    dps_mult *= other * momentum
    taken = (1 - k["dodge"]) * (1 - min(1.0, k["damage_taken"])) * (1 - min(1.0, slow_share))
    if boss:
        taken *= 1 - min(1.0, k["boss_damage_taken"])
    hp_mult = 1 + k["heal_below"] + (k["cheat_death"] if boss else 0.0)
    return dps_mult, taken, hp_mult, k["lifesteal"], other
