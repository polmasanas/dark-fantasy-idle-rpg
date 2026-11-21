"""Writes skills_config.json (M6). Edit the values here, run it with both output paths, then re-run the simulations."""
import json, sys
def A(id, cls, tier, cd, effects): return {"id": f"{cls}_{id}", "class": cls, "tier": tier, "kind": "active", "max_rank": 3, "cooldown_s": cd, "rank_cdr": 0.05, "effects": effects}
def P(id, cls, tier, effects): return {"id": f"{cls}_{id}", "class": cls, "tier": tier, "kind": "passive", "max_rank": 5, "effects": effects}
def hit(p, pr, **kw): e = {"kind": "hit", "percent": p, **kw, "per_rank": {"percent": pr}}; return e
def stat(s, v): return {"kind": "stat", "stat": s, "value": v, "per_rank": {"value": v}}
def cond(c, v, **kw): return {"kind": "conditional_damage", "condition": c, "value": v, **kw, "per_rank": {"value": v}}
def trig(t, v, pr, **kw): return {"kind": "trigger", "trigger": t, "value": v, **kw, "per_rank": {"value": pr}}
F, R, M = "fighter", "archer", "mage"
nodes = [
 A("heavy_swing", F, 1, 8, [hit(1.8, 0.25)]),
 P("toughness", F, 1, [stat("pct_hp", 0.05)]),
 P("iron_skin", F, 1, [stat("pct_armour", 0.06)]),
 A("rend", F, 2, 10, [hit(1.0, 0.0), {"kind": "dot", "percent": 0.6, "duration": 4, "element": "physical", "per_rank": {"percent": 0.2}}]),
 P("second_wind", F, 2, [trig("heal_below", 0.15, 0.05, threshold=0.3)]),
 P("riposte", F, 2, [stat("counter_chance", 0.02)]),
 A("war_cry", F, 3, 20, [{"kind": "buff", "stat": "pct_damage", "value": 0.15, "duration": 6, "per_rank": {"value": 0.05}}]),
 P("brutality", F, 3, [stat("crit_damage", 0.06)]),
 P("stand_firm", F, 3, [stat("boss_damage_taken", 0.05)]),
 A("executioner", F, 4, 15, [hit(3.5, 0.5, condition="enemy_hp_below", threshold=0.3)]),
 P("bloodlust", F, 4, [trig("lifesteal", 0.01, 0.01)]),
 P("unbreakable", F, 4, [trig("cheat_death", 0.05, 0.05)]),
 A("aimed_shot", R, 1, 7, [hit(1.5, 0.2, always_crit=True)]),
 P("quick_hands", R, 1, [stat("attack_speed", 0.02)]),
 P("keen_eye", R, 1, [stat("crit_chance", 0.01)]),
 A("venom_arrow", R, 2, 10, [{"kind": "dot", "percent": 0.8, "duration": 5, "element": "nature", "per_rank": {"percent": 0.25}}]),
 P("evasive", R, 2, [stat("dodge", 0.03)]),
 P("hunters_mark", R, 2, [cond("vs_family:beasts", 0.04), cond("vs_archetype:brute", 0.04)]),
 A("volley", R, 3, 16, [hit(0.35, 0.03, hits=5)]),
 P("deadeye", R, 3, [stat("crit_damage", 0.03)]),
 P("wind_step", R, 3, [stat("cooldown_reduction", 0.02)]),
 A("piercing_arrow", R, 4, 18, [hit(2.0, 0.3, cannot_miss=True)]),
 P("momentum", R, 4, [trig("momentum", 0.004, 0.002, max_stacks=10)]),
 P("steady_aim", R, 4, [cond("own_hp_above", 0.02, threshold=0.5)]),
 A("firebolt", M, 1, 8, [hit(1.6, 0.2, element="fire")]),
 P("attunement", M, 1, [stat("pct_elemental", 0.03)]),
 P("arcane_ward", M, 1, [stat("pct_hp", 0.05)]),
 A("frost_lance", M, 2, 10, [hit(1.4, 0.2, element="water"), {"kind": "enemy_slow", "value": 0.3, "duration": 4, "per_rank": {}}]),
 P("focus", M, 2, [stat("cooldown_reduction", 0.02)]),
 P("mana_shield", M, 2, [stat("damage_taken", 0.03)]),
 A("thornbind", M, 3, 12, [{"kind": "dot", "percent": 1.0, "duration": 6, "element": "nature", "per_rank": {"percent": 0.3}}]),
 P("overload", M, 3, [stat("skill_crit_damage", 0.05)]),
 P("elemental_lore", M, 3, [cond("vs_family:elementals", 0.04), cond("vs_family:wizards", 0.04)]),
 A("radiant_nova", M, 4, 20, [hit(2.6, 0.4, element="light")]),
 P("echo", M, 4, [trig("echo", 0.02, 0.02)]),
 P("glass_cannon", M, 4, [stat("pct_damage", 0.03), stat("pct_armour", -0.04)]),
]
order = {
 F: ["heavy_swing","toughness","heavy_swing","toughness","heavy_swing","rend","second_wind","riposte","rend","riposte","war_cry","brutality","war_cry","brutality","rend","executioner","war_cry","executioner","executioner","toughness","toughness","toughness","iron_skin","iron_skin","iron_skin","iron_skin","iron_skin","riposte","riposte","riposte","brutality","brutality","brutality","second_wind","second_wind","second_wind","second_wind","stand_firm","stand_firm","stand_firm","stand_firm","stand_firm","bloodlust","bloodlust","bloodlust","bloodlust","bloodlust","unbreakable","unbreakable","unbreakable","unbreakable","unbreakable"],
}
def expand(cls, prefs):
    seen = {}
    out = []
    for p in prefs: out.append(f"{cls}_{p}")
    return out
order_full = {F: expand(F, order[F])}
order_full[R] = expand(R, ["aimed_shot","quick_hands","aimed_shot","keen_eye","aimed_shot","evasive","venom_arrow","evasive","venom_arrow","evasive","volley","deadeye","volley","wind_step","venom_arrow","piercing_arrow","volley","piercing_arrow","piercing_arrow"] + ["quick_hands"]*4 + ["keen_eye"]*4 + ["hunters_mark"]*5 + ["evasive"]*2 + ["deadeye"]*4 + ["wind_step"]*4 + ["momentum"]*5 + ["steady_aim"]*5)
order_full[M] = expand(M, ["firebolt","arcane_ward","firebolt","arcane_ward","firebolt","frost_lance","mana_shield","attunement","frost_lance","mana_shield","thornbind","overload","thornbind","elemental_lore","frost_lance","radiant_nova","thornbind","radiant_nova","radiant_nova"] + ["attunement"]*4 + ["arcane_ward"]*3 + ["focus"]*5 + ["mana_shield"]*3 + ["overload"]*4 + ["elemental_lore"]*4 + ["echo"]*5 + ["glass_cannon"]*5)
for c in (F,R,M):
    ranks = {}
    for i in order_full[c]: ranks[i] = ranks.get(i,0)+1
    for n in nodes:
        if n["class"]==c: assert ranks.get(n["id"],0)==n["max_rank"], (n["id"], ranks.get(n["id"]))
doc = {"_comment": "M6 skill trees (M6-1..M6-3, spec Docs/specs/2026-11-20-m6-skills-design.md). Authority: Tools/BalanceSim/skills_config.json; copy byte-identical to Assets/_Game/Data/. Values: rank 1, plus per_rank x (rank-1). Percent = share of a hit (2.5 = 250%).", "nodes": nodes, "suggested_order": order_full}
s = json.dumps(doc, indent=2, ensure_ascii=False) + "\n"
# Usage: python3 Tools/BalanceSim/make_skills.py Tools/BalanceSim/skills_config.json Assets/_Game/Data/skills_config.json
for p in sys.argv[1:]: open(p, "w").write(s)
