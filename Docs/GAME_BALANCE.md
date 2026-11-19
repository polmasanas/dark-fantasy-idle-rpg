# GAME_BALANCE.md — Numerical Design

> **Status:** v0.1 DRAFT. Every value below is a **proposal pending approval**.
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
| Boss | HP ×12, attack ×2, **60 s time limit** (enrage = fail) | `boss_*` |
| Variance | Each spawn gets HP ×U(0.9, 1.1) | `variance_mult_*` |

Family, element and archetype modifiers (GDD §4) apply on top of these numbers. They average out to 1.0 across a region's mix, so the simulation ignores them.

---

## 4. Character

### 4.1 Attribute points (the 75 / 25 rule)

Each level grants **4 points: 3 automatic** (75%), spread by the class growth template, and **1 manual** (25%).

| Class | Auto template per level (3 points) |
|---|---|
| Fighter | STR 1.5 · VIT 1.0 · DEF 0.5 |
| Archer | DEX 1.5 · VIT 0.75 · DEF 0.5 · CRIT_CH 0.25 |
| Mage | INT 1.5 · VIT 0.75 · DEF 0.25 · CRIT_DMG 0.5 |

Fractional growth is tracked as an exact integer cycle over 4 levels (12 points), so saves never hold fractions.

### 4.2 Derived stats

| Stat | Formula |
|---|---|
| Primary attribute | Fighter STR, Archer DEX, Mage INT |
| Damage multiplier | `(1 + 0.015·Primary) · (1 + Σ%dmg) · ArtifactMight` |
| Hit damage | `max(3, WeaponDamage) · DamageMultiplier` |
| Max HP | `(100 + Σ gear HP) · (1 + 0.02·VIT) · (1 + Σ%hp) · ArtifactBulwark` |
| Armour | `Σ gear armour · (1 + 0.01·DEF)` |
| Damage reduction | `min(0.75, Armour / (Armour + 40·1.07^(L_enemy−1)))` (relative to the enemy's level) |
| Crit chance | `min(0.75, 0.05 + 0.002·CritPoints)` |
| Crit damage | `1.5 + 0.005·CritPoints` (+0.01 per manual CRIT_DMG point) |
| Hit chance | `clamp(0.92 · Acc/(Acc + Eva/4) …)`. **Simulation uses a flat 0.92**; full formula in GDD §4.4 |
| Attack interval | 1.6 s base, `/(1 + Σ%attack speed)`, floor 0.4 s |
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
Win ⇔ TTK < TTD (and TTK ≤ 60 s for bosses).   +0.6 s between fights.
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
| Boss drop | 1 guaranteed item, rarity ≥ Rare |
| Item level | = enemy level |
| Luck | Rarity weights above Common get ×(1 + Luck/(Luck+200)), i.e. up to ×2, with diminishing returns |
| Permanent scroll | 0.02% per kill (+1 to an attribute, permanent). Observed: 20–26 in 30 days for active players |

### 6.4 Upgrades, crafting, salvage

| Formula | Definition |
|---|---|
| Upgrade | +5% base stats per level |
| Upgrade cost (gold) | `12 · 1.07^(iLvl−1) · 1.2^n` (n = current upgrade level) |
| Salvage | Materials per rarity (above) + 50% of gold spent on its upgrades |
| Crafting | `20 + 0.5·iLvl` materials. Chosen slot, item level = highest unlocked subzone level, rarity rolled normally |
| Gold per kill | `2 · 1.07^(L−1)`, boss ×15 |

---

## 7. Offline

| Parameter | Value |
|---|---|
| Efficiency | 50% (offline time counts at half speed) |
| Cap | 12 h real time (= 6 h simulated). Artifact "Hourglass" can raise it (GDD §11), max 24 h |
| Loot | Held until return, then auto-equip and salvage rules apply. Shown in the offline report |

---

## 8. Prestige & artifacts

| Formula | Definition |
|---|---|
| Unlock | Defeat the region 4 boss |
| Dust per prestige | `floor(10 · 1.5^(R_best − 4))`, where R_best is the highest region boss defeated this run |
| Artifact effect | ×1.10 per level (multiplicative) |
| Artifact cost | Might / Bulwark: `5 · 1.28^lvl`. Greed / Wisdom: `8 · 1.25^lvl` |

| Region boss | 4 | 5 | 6 | 8 | 10 | 15 | 20 | 30 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Dust | 10 | 15 | 22 | 50 | 113 | 864 | 6 568 | 378 767 |

---

## 9. Simulation results (30 days, 7 seeds, `results/SEEDS.md`)

| Profile | Behaviour | Prestiges (median) | Best region boss: min / median / max | Day 7 best (median) |
|---|---|---:|---:|---:|
| **active** | 4 × 45 min/day, continuous upgrades | 17 | 22 / **25** / 31 | 14 |
| **optimiser** | active + balanced stat points | 18 | 20 / **26** / 28 | 11 |
| **casual** | 3 × 10 min/day | 15 | 18 / **19** / 21 | 8 |
| **farmer** | casual, farms 1 region back when stuck | 14 | 16 / **16** / 17 | 6 |
| **offline-focused** | 2 × 3 min/day, 12 h gaps | 11 | 14 / **15** / 16 | 6 |
| **never prestige** | active, never resets | 0 | 13 / **15** / 17 | 13 |

### 9.1 What works
- **Prestige clearly beats never prestiging** by day 30 (25 vs 15 regions for the same play time). The wall is real: never-prestige gains only 2 regions between day 7 and day 30.
- **Ranking is sensible:** more play time leads to more progress, but casual players still reach 75% of active depth (an idle-friendly gap).
- **Long-term growth is logarithmic**, not runaway. Region 10 by about day 4, region 20 by about day 10 (active).
- **The rarity distribution matches the configured weights** (Mythical 0.11–0.18% observed vs 0.1% configured; the extra comes from bosses' Rare+ floor).
- **Offline cap:** 12 h gaps never exceeded it. Offline-focused players progress at about 60% of casual.

### 9.2 Problems found (tuning backlog: not yet fixed, needs your input)
| # | Finding | Proposed fix |
|---|---|---|
| B-1 | **Early prestiges feel grindy.** Casual players prestige at boss 4 three times for +10 dust each. Never-prestige leads until about day 7. | First-prestige bonus (e.g. 3× dust on prestiges 1–3), or cheaper first artifact levels (cost base 3). Re-simulate. |
| B-2 | **Gold surplus late.** Active players spend only 27% of gold, because upgrades hit their rarity cap. | Add enchanting gold costs (GDD §6.7) and gold costs for skill-tree respec. Target 70–90% spent. |
| B-3 | **Farming earlier zones is weaker** than farming the frontier (farmer 16 vs casual 19). That conflicts with "previous zones remain worth farming". | Zone-exclusive drops (scrolls, family-specific accessories, dungeon keys) and a "mastery" bonus per cleared subzone. Needs design approval (GDD §8.6). |
| B-4 | **The quick combat estimate is off** by up to 16% at mid-ratios. | Acceptable for balancing. The C# engine simulates every swing; we re-check pacing with the C# simulation in M2. |
| B-5 | **The simulation doesn't model** pets, skills, dungeons, class or element modifiers, enchanting, or multi-character play. | Add them to the simulation as each system is designed (roadmap M3–M6). |

### 9.3 Tuning history
| Iteration | Change | Outcome |
|---|---|---|
| 1 | Enemy HP ×1.075/level | No wall: never-prestige reached region 137 in 7 days. |
| 2 | HP ×1.09, no starter weapon | Runs stuck at boss 1 (weapon is 1/17 of drops). Added starter weapon (D-015). |
| 3 | HP ×1.10, dust ×1.8/region | Runaway: active reached region 83 with 10²¹ dust. |
| 4 | Dust ×1.6, plus crafting | Stable, but active reached region 55 in 30 days, too fast for content production. |
| 5 | **Dust ×1.5, core artifact cost ×1.28** | Current values. |

### 9.4 How to re-run
```bash
cd Tools/BalanceSim && python3 balance_sim.py && python3 seeds.py 7
```
