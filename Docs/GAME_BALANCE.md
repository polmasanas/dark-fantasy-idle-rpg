# GAME_BALANCE.md — Numerical Design

> **Status:** v0.2. Structural rules approved on 2026-11-19 (DECISIONS.md). Individual constants remain tunable, and every tuning change is logged.
> **Source of truth for numbers:** `Tools/BalanceSim/balance_config.json`. This document explains it.
> **Reference implementation:** `Tools/BalanceSim/balance_sim.py`. The C# core must reproduce these formulas exactly; golden-value tests enforce it (see TESTING_STRATEGY.md §5).

---

## 1. Design principles

1. **Three axes of power, each with a different shape.**
   | Axis | Source | Growth shape | Why |
   |---|---|---|---|
   | Exponential | Item level (gear base stats) | `×1.07` per item level | Keeps pace with exponential enemies. Resets on prestige. |
   | Linear | Attributes (levels, manual points, scrolls, gear attributes) | `+x%` per point | Meaningful early, never explodes. |
   | Multiplicative meta | Artifacts (Artifact Dust) | `×1.10` per artifact level, exponential cost | The only thing that beats the wall. Survives prestige. |
2. **Enemies outgrow gear.** Enemy HP grows `×1.10` per level and gear grows `×1.07`, so each region is ~1.3× harder than gear alone can follow. That gap creates the **wall** that makes prestige necessary.
3. **Prestige feedback must stay below 1.** Dust grows with depth, and artifacts convert dust into power with diminishing returns (exponential cost). With the chosen values, each prestige pushes further, by a shrinking amount. That gives logarithmic long-term progression and no runaway (see §9.3: with dust growth `×1.8` the active player reached region 83 with 10²¹ dust in 30 days).
4. **No magic numbers in code.** Every constant here lives in `BalanceConfig`, a ScriptableObject that mirrors the JSON.
5. **Doubles everywhere.** The largest value planned is about 1e45 at region 100. `double` holds up to 1e308, which reaches about region 700. Technical risk T-03 covers a big-number type if content ever goes past that.

---

## 2. World structure

| Symbol | Meaning | Value |
|---|---|---|
| `S` | Subzones per region | 5 |
| `Δ` | Levels per subzone | 2 |
| `z` | Global subzone index `= (r−1)·S + s` | — |
| `L_sub(r,s)` | Enemy level of a subzone | `(z−1)·Δ + 1` |
| `L_boss(r)` | Boss level | `r·S·Δ` (so region 1 → 10, region 10 → 100) |
| `K(r)` | Kills to unlock the next subzone or boss | `min(100, 10 + 5·(r−1))` → 10 … 100 (spec: 10–100) |

---

## 3. Enemies

| Formula | Definition | Config |
|---|---|---|
| HP | `30 · 1.10^(L−1)` | `enemy.hp_base`, `hp_growth` |
| Attack | `4 · 1.09^(L−1)` per hit, one hit every 2.0 s | `atk_base`, `atk_growth`, `attack_interval_s` |
| Boss | HP ×12, attack ×2, **60 s time limit** (enrage = fail). **Region 1 boss HP ×4** and a first-run training boss (3 × normal HP, cannot kill) for onboarding (ON-1) | `boss_*`, `onboarding.*` |
| Variance | Each spawn gets HP ×U(0.9, 1.1) | `variance_mult_*` |

Family, element and archetype modifiers (GDD §4) apply on top of these numbers. They average out to 1.0 across a region's mix, so the simulation ignores them.

---

## 4. Character

### 4.1 Attribute points (the 75 / 25 rule)

Each level grants **4 points: 3 automatic** (75%), spread by the class growth template, and **1 manual** (25%).

| Class | Auto template per level (3 points) |
|---|---|
| Fighter | STR 1.5 · VIT 1.0 · DEF 0.25 · COUNTER 0.25 |
| Archer | DEX 1.5 · VIT 0.75 · DEF 0.5 · CRIT_CH 0.25 |
| Mage | INT 1.5 · VIT 0.75 · DEF 0.25 · CRIT_DMG 0.5 |

Fractional growth is tracked as an exact integer cycle over 4 levels (12 points), so saves never hold fractions. An **auto-allocate** preset can spend the manual point automatically (D-014).

### 4.2 Derived stats

| Stat | Formula |
|---|---|
| Primary attribute | Fighter STR, Archer DEX, Mage INT |
| Damage multiplier | `(1 + 0.015·Primary) · (1 + Σ%dmg) · ArtifactMight` |
| Hit damage | `max(3, WeaponDamage) · DamageMultiplier` |
| Max HP | `(100 + Σ gear HP) · (1 + 0.02·VIT) · (1 + Σ%hp) · ArtifactBulwark` |
| Armour | `Σ gear armour · (1 + 0.01·DEF)` |
| Damage reduction | `min(0.75, Armour / (Armour + 40·1.07^(L_enemy−1)))` (relative to the enemy's level) |
| Crit chance | `min(1.00, 0.05 + 0.002·CritPoints)` (cap 100%, R2-21) |
| Crit damage | `1.5 + 0.005·CritPoints` (+0.01 per manual CRIT_DMG point) |
| Hit chance | `clamp(0.92 · Acc/(Acc + Eva/4) …)`. **Simulation uses a flat 0.92**; full formula in GDD §4.4 |
| Attack interval (R2-1b) | class base (Fighter 3.0 s, Archer 2.2 s, Mage 3.4 s) `/(1 + Σ%attack speed)`, floor 0.35 s. Damage per hit × `base/1.6`, so early TTK equals the old 1.6 s model. Haste artifact: +5% attack speed/level, max 40, cost `50 · 1.7^level` |
| Counter (D-033) | `min(0.40, 0.004·CounterPoints)` chance per enemy hit taken → immediate extra attack at 60% damage |
| Catch-up XP (D-024) | +50% XP while below the highest character's level |
| Secondary attributes | DEX: +0.5% accuracy and +0.2% attack speed per point (all classes). INT: +1% elemental damage per point. Luck: see §6.3 |

### 4.3 Experience

| Formula | Definition |
|---|---|
| XP to next level | `3 · 1.06^(l−1) · (10 + 0.5·l)` (about 10 + l/2 same-level kills) |
| XP per kill | `3 · 1.06^(L−1)`. Boss ×10. Over-level penalty: −5% per level above enemy+5, floor 10% |

**Result:** player level tracks enemy level (level ~50 at region 5, ~290 at region 27).

---

## 5. Combat resolution (reference model)

The game engine simulates every swing as discrete events (GDD §4.5). For balancing, the simulation uses the expected value:

```
swings = ceil(EnemyHP / AvgHit) / HitChance,  AvgHit = Hit·(1 + CC·(CD−1))
TTK    = swings · Interval
TTD    = PlayerHP / (EnemyAtk · (1 − DR) / EnemyInterval)
Normal enemy: never kills the player (R2-1c); Win ⇔ TTK ≤ 30 s escape timer, else no reward;
          5 escapes in a row → retreat one subzone.
Boss: Win ⇔ TTK < TTD and TTK ≤ 60 s.   +0.6 s between fights.
Counter: TTK /= 1 + (Counter·0.6·AvgHit·Hit/EnemyInterval) / (AvgHit·Hit/Interval)
```

**Validation vs 4 000 stochastic fights:** error 0% to −16%. The largest error is at an average hit ≈ 35% of enemy HP, where crits change the hit count. The real engine has no such error, because it simulates every swing.

HP fully restores between normal fights (decision D-018, proposed).

---

## 6. Equipment & loot

### 6.1 Base stats by slot (×1.07^(iLvl−1) × rarity multiplier × upgrade multiplier)

| Slot | Base at item level 1 |
|---|---|
| Main hand | 10 weapon damage |
| Off-hand | 35% of main-hand damage (or a shield: armour; GDD §6.2) |
| Armour (×6) | 6 armour + 12 HP |
| Jewellery (×3) | crit points `(2 + 0.08·iLvl)`, which scale **linearly** |
| Accessories (×6) | +1.5% damage base (special effects per item, GDD §6.3), no item-level scaling |

### 6.2 Rarity

| Rarity | Base-stat mult (D-003) | Random attributes | Max upgrade | Drop weight | Salvage materials |
|---|---:|---:|---:|---:|---:|
| Common | 1.00 | 0 | +5 | 60% | 1 |
| Uncommon | 1.20 | 0 | +10 | 25% | 2 |
| Rare | 1.45 | 1 | +15 | 10% | 4 |
| Epic | 1.75 | 1 | +20 | 4% | 8 |
| Legendary | 2.10 | 2 | +25 | 0.9% | 16 |
| Mythical | 2.50 | 3 | +30 | 0.1% | 32 |

A random attribute is worth +6% (damage, HP or another stat) and adds into its category's sum.

### 6.3 Drops

| Item | Value |
|---|---|
| Item drop chance per normal kill | 8% |
| Slot weights | main hand ×3, every other slot ×1 (reduces weapon-drought variance) |
| Boss drop | First kill of each boss **per run**: 1 guaranteed item, rarity ≥ Rare (M3-2). Later kills: 1 item at normal rarity odds |
| Item level | = enemy level |
| Luck | Rarity weights above Common get ×(1 + Luck/(Luck+200)), i.e. up to ×2, with diminishing returns |
| Permanent scroll | 0.02% per kill (+1 to an attribute, permanent). Observed: 20–26 in 30 days for active players |

### 6.4 Upgrades, crafting, salvage

| Formula | Definition |
|---|---|
| Upgrade | +5% base stats per level |
| Upgrade cost (gold) | `12 · 1.07^(iLvl−1) · 1.2^n` (n = current upgrade level) |
| Salvage | Materials per rarity (above) + 50% of gold spent on its upgrades |
| Crafting | `20 + 0.5·iLvl` materials. Chosen slot; item type and rarity random; item level = highest unlocked subzone level |
| Enchanting (D-027, not yet simulated) | materials `10` + gold `50 · 1.07^(iLvl−1) · 1.5^enchantCount` (escalates per enchant on the same item) |
| Subzone mastery (D-036) | 1 star per 1 000 kills in a subzone (max 5): +1% gold and +1% item drop chance per star, account-wide, kept on prestige |
| Gold per kill | `2 · 1.07^(L−1)`, boss ×15 |

---

## 6b. Classes, triangle, elements and families (M5)

| Rule | Value | Config |
|---|---|---|
| Class triangle (D-017) | advantage ×1.20 dealt / ×0.85 taken; disadvantage ×0.85 / ×1.20; Fighter > Caster, Archer > Brute, Mage > Marksman; no style = ×1 | `triangle.*` |
| Elements (D-019) | strong ×1.5, weak ×0.75; resistances clamped to −0.5…0.75 | `elements.*` |
| Elemental gear lines (M5-1) | "adds 8–16% of each hit" as Fire / Nature / Water / Light / Dark (main hand, off hand, rings, amulet); Mage class line also gives +15% elemental damage | `items.attributes.adds_*`, `item_rules.class_bonus_pct_elemental` |
| Class weapons | Bow 9.3 base + 8% attack speed; Staff 10 base + 10% elemental damage; Sword 10 (unchanged) | `items.types.main_hand` |
| Beasts (Drowned Fen) | evasion 125 (hit chance ≈ 0.70 instead of 0.92); mostly brutes | `families.beasts` |
| Elementals (Frost Pass 1–4) | Fire 25% / Nature 25% / Water 50%, −75% to their own element, elemental lines ×3 on drops; mostly marksmen | `families.elementals` |
| Wizards (Frost Pass 5) | Light, −75% Light, elemental lines ×3; casters | `families.wizards` |
| Undead (Vael Wastes) | Dark; mostly casters | `families.undead` |

**Tuning (simulation, M5):** effective damage per second at an equal reference build (level = boss level, Common class weapon and armour, no elemental lines):

| Region | Family | Best hero | Best vs worst |
|---:|---|---|---:|
| 2 | Humanoids | (nearly even) | +8% |
| 3 | Beasts | Archer | +27% |
| 4 | Elementals + Wizards | Mage | +30% |
| 5 | Undead | Fighter | +24% |

Played start to finish by one hero, the three classes reach region 5 within 1% of each other (active and casual profiles): bosses at session boundaries gate progress more than class speed. Over 30 days the profiles land close to the M4 results (active ≈ region 38–42, casual ≈ 27), slightly slower because families now push back.

## 6c. Skills (M6)

| Rule | Value | Config |
|---|---|---|
| Points | 1 per 5 levels; unspent = earned − ranks | `combat.skill_point_every_levels` |
| Tiers | 4 tiers of 3 nodes per class; tier t opens at (t−1) × 5 points spent | `skill_rules.tier_points` |
| Ranks (M6-1) | actives 3, passives 5; value = rank 1 + per_rank × (rank − 1) | `skills_config.json` |
| Actives (R2-9, M6-2) | 3 slots by priority; a ready skill replaces the next attack; cooldown × (1 − 5% per extra rank) × (1 − cooldown reduction, max 50%); timers run between fights and offline | `skill_rules.cooldown_reduction_cap` |
| Damage over time | total = percent × the hit at cast (no crit), ticks every 1 s, recast refreshes | `skill_rules.dot_tick_s` |
| Hero dodge | capped at 30% | `skill_rules.hero_dodge_cap` |
| Respec | 40 × 1.07^(level − 1) gold (provisional) | `skill_rules.respec_*` |

The node values live in `Tools/BalanceSim/skills_config.json`, generated by `Tools/BalanceSim/make_skills.py` (edit the numbers there and run it with both output paths).

**Tuning (simulation, M6):** effective damage per second gained at the M5 reference build, points spent by the suggested order, measured with the real engine (`results/CSHARP_COMBAT.md`; the Python model agrees within about 5% of the damage multiplier, up to 9% for the Mage against undead):

| Reference | Points | Fighter | Archer | Mage |
|---|---:|---:|---:|---:|
| Region 2 | 4 | +34% | +48% | +37% |
| Region 3 | 6 | +58% | +58% | +63% |
| Region 4 | 8 | +55% | +51% | +48% |
| Region 5 | 10 | +59% | +63% | +129% |
| Region 5, full tree | 52 | +75% | +153% | +260% |

Slow attackers gain the most from cooldown skills (one 180% swing replaces a 3 s attack). The Mage's jump in region 5 comes from elements: its normal hits are Physical (×0.75 against Dark) while its skills are Fire, Water, Nature and Light (Light ×1.5 against Dark). The Fighter's late tree is mostly defensive, so its damage gain stays lower but it wins bosses the others lose.

**30 days, seed 12345, before → after skills** (best boss ever): active 40 → 48, casual 26 → 33, offline-focused 21 → 25, farmer 25 → 32, optimiser 41 → 50, never-prestige 19 → 24. Over 7 seeds (`results/SEEDS.md`, median best boss): active 38 → 47, casual 27 → 33, offline-focused 21 → 24, farmer 25 → 31, optimiser 40 → 49, never-prestige 19 → 22. Open decision M6-B1 (pacing, class spread).

## 7. Offline

| Parameter | Value |
|---|---|
| Efficiency | 50% (offline time counts at half speed) |
| Cap | 12 h real time (= 6 h simulated). Artifact "Hourglass" can raise it (GDD §11), max 24 h |
| Report threshold | Absences under 2 min are credited silently (`offline_rules.report_min_seconds`) |
| Clock safety | Clock moved back = no credit; in the same boot, credit never exceeds the device uptime that passed + 60 s (`offline_rules.clock_tolerance_s`) |
| Simulation | Fight by fight with the real engine (M4-3); 12 h away (6 h simulated, about 1,600–1,900 fights) takes 10–30 ms on a desktop CPU; phone target < 200 ms, checked on device (M4-4) |
| Loot | Auto-salvage applies as items drop; the rest is held (and saved) until the player taps **Collect**, where the full-bag rule applies. Never equipped automatically (R2-18). Shown in the offline report |

---

## 8. Prestige & artifacts

| Formula | Definition |
|---|---|
| Unlock | Defeat the region 4 boss |
| Dust per prestige | `floor(10 · 1.5^(R_best − 4))`, where R_best is the highest region boss defeated this run. **×3 on the first 3 prestiges** (D-037) |
| Artifact effect | ×1.10 per level (multiplicative) |
| Artifact cost | Might / Bulwark: `5 · 1.28^lvl`. Greed / Wisdom: `8 · 1.25^lvl` |

| Region boss | 4 | 5 | 6 | 8 | 10 | 15 | 20 | 30 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Dust | 10 | 15 | 22 | 50 | 113 | 864 | 6 568 | 378 767 |

---

## 9. Simulation results (30 days, 7 seeds, `results/SEEDS.md`; includes round-2 rules: slow class attacks, Haste, no death in normal fights + escape timer, 100% crit cap, extra drops above 100%)

| Profile | Behaviour | Prestiges (median) | Best region boss: min / median / max | Day 7 best (median) |
|---|---|---:|---:|---:|
| **active** | 4 × 45 min/day, continuous upgrades | 23 | 38 / **40** / 40 | 19 |
| **optimiser** | active + balanced stat points | 25 | 39 / **41** / 42 | 18 |
| **casual** | 3 × 10 min/day | 16 | 25 / **26** / 27 | 12 |
| **farmer** | casual, farms 1 region back for 30 min after a failed boss | 16 | 23 / **25** / 25 | 10 |
| **offline-focused** | 2 × 3 min/day, 12 h gaps | 12 | 20 / **21** / 22 | 11 |
| **never prestige** | active, never resets | 0 | 17 / **18** / 19 | 16 |

**M3 item model (current table):** the simulation now uses the game's item model: 11 slots (no accessories yet, M3-1), three trade-off types per slot, the 12-attribute pool with rarity-biased rolls, class lines, Luck, gold find and drop chance. Compared with the previous model (which counted 6 accessory slots as +1.5% damage each), active players reach region 40 instead of 43 and casual 26 instead of 27.

**Boss-retry change (R2-2, M2 playtest):** a failed boss now returns after a new wave of K kills instead of every 60 s. Stuck players no longer spend up to half their time in hopeless 60 s boss fights that pay nothing, so everyone progresses faster (active 39 → 43, casual 23 → 27). First region-25 boss kill (end of launch content, R2-22): active about day 11, casual about day 24–26, offline-focused not within 30 days. Casual is slightly ahead of the M10 target ("no earlier than about day 30"); retune in M10 (B-10).

**Round-2 effect:** Haste is the largest new power source (active players own about 20 levels = +100% attack speed by day 30), which moved active players from region 30 to about 39. Casual and offline players are unchanged. The isolation runs: without Haste, active players reach 32; with Haste at the first proposed price (10 × 1.3^level), 45.

### 9.1 What works
- **Prestige clearly beats never prestiging:** 30 vs 16 regions at day 30 for the same play time. Never-prestige stalls by day 4–5.
- **More play means more progress**, but casual players still reach about 77% of active depth.
- **Long-term growth is logarithmic, not runaway.**
- **Mastery closes most of the farming gap** (farmer 21 vs casual 23; was 16 vs 19), so B-3 is largely solved.
- **The rarity distribution matches the configured weights.** 12 h offline gaps stay within the cap.

### 9.2 Findings & tuning backlog
| # | Finding | Status / next step |
|---|---|---|
| B-1 | Early prestiges felt grindy | **Addressed** by D-037 (×3 dust). Active prestigers now overtake never-prestigers on day 7–8 (see B-7) |
| B-2 | Gold surplus late (27% spent) | **Designed** (D-027 escalating enchants). Simulate in M8; target 70–90% spent |
| B-3 | Old zones weaker than frontier | **Addressed** by D-036 mastery (+ zone-exclusive loot, not simulated) |
| B-4 | Quick combat estimate off by up to 16% | Accepted; the C# simulation (D-031) uses the real engine |
| B-5 | Not simulated yet: pets, dungeons and tokens, enchanting, multi-character catch-up (skills added in M6, classes and elements in M5) | Added as systems are built (M7–M9) |
| B-11 | **Skills speed up progress** (M6-B1): +7–9 regions at day 30 for active players, casual reaches region 33 (beyond the 25 launch regions) | Owner decision M6-B1; retune with prestige (M7) and content (M10) |
| B-6 | **Mastery stacks fast:** +58% to +141% gold and drop chance after 30 days (active), growing with every new region | Review after M10 (P-002): cap the total or use 0.5% per star |
| B-8 | (Partly addressed: prestige share threshold re-tested at 25% and 50%; 50% gives better results for casual players and is kept.) In seed 12345 the active profile stops at region 30 from day 19: the simulated policy only prestiges when the new dust is ≥ 50% of all dust earned, which becomes rare. Real players would keep resetting | Improve the simulated prestige policy (time-to-next-region based, like P-001) in M7 |
| B-10 | After the boss-retry change, casual players finish the 25 launch regions around day 24–26, slightly before the M10 target of about day 30 | Retune in M10 with the real region content |
| B-9 | **Launch content runs out fast.** First region-10 boss kill: active ≈ day 2, casual ≈ day 7, offline-focused ≈ day 10 (5 seeds). With 10 launch regions, most players reach the end of content in the first week or two | **Resolved by R2-22:** launch with about 25 regions (region 25 ≈ day 15–20 active, beyond day 30 casual) |
| B-7 | **Simulated players prestige too early** (regions 5–6, before their natural wall at about 12–15), so the first days look slower than never prestiging | Policy artefact, not a reward problem. Proposed P-001 "recommended prestige" hint in the UI |

### 9.3 Tuning history
| Iteration | Change | Outcome |
|---|---|---|
| 1 | Enemy HP ×1.075/level | No wall: never-prestige reached region 137 in 7 days. |
| 2 | HP ×1.09, no starter weapon | Runs stuck at boss 1 (weapon is 1/17 of drops). Added starter weapon (D-015). |
| 3 | HP ×1.10, dust ×1.8/region | Runaway: active reached region 83 with 10²¹ dust. |
| 4 | Dust ×1.6, plus crafting | Stable, but active reached region 55 in 30 days, too fast for content production. |
| 5 | Dust ×1.5, core artifact cost ×1.28 | Active 25, casual 19 regions at day 30. |
| 6 | **+ Counter, mastery, ×3 first-prestige dust** (design review) | Current: active 30, casual 23, never 16. |

### 9.4 Google Sheets view (D-031)
The workbook **"Dark Fantasy Idle RPG – Balance"** (owner's Google Drive) has tabs About, Config, Curves, Prestige, Rarity, Sandbox and Simulation. Every number on Curves, Prestige and Rarity is a live formula over Config. Config is regenerated with `python3 Tools/BalanceSim/export_sheets.py`. Its locale is es_ES, so formulas use `;` as the argument separator.

### 9.5 How to re-run
```bash
cd Tools/BalanceSim && python3 balance_sim.py && python3 seeds.py 7
```
