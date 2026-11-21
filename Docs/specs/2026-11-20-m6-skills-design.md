# M6 design: skills

> **Status:** approved by the owner, 2026-11-21. Decisions M6-1…M6-3 were taken one at a time in conversation; the tree contents (§3.3) are Claude's proposal, accepted by the owner ("seems fine"). Items marked **[Claude's call]** were not asked and can be overruled. Implements roadmap M6 (Jira DFIRPG-7).

## 1. Intent
Pillar 2 of the GDD is **build ownership**: auto-play must never mean no decisions. M6 gives each hero a **Common skill tree**: points earned by levelling go into actives that fire on their own and passives that shape the build. The player decides *what to raise first* and *which 3 actives to bring, in which order*.

**Success looks like:** in the editor, a level-25 Fighter puts points into Heavy Swing and Toughness, sees Heavy Swing fire every few fights with its own damage number, swaps War Cry into the loadout before a boss, and resets the tree for gold. A Mage brings Firebolt to a Nature-element Frost Pass elemental and notices the difference. The simulation shows how much faster each class progresses with skills.

**Goals**
- A Common tree of 12 nodes (4 tiers × 3) for each of the 3 classes, with ranks.
- Active skills: 3 slots per hero, auto-cast by the player's priority order, cooldowns in real seconds that keep running between fights.
- Passives: permanent stat bonuses and conditional combat effects.
- New combat mechanics needed by the trees: damage over time, timed buffs, enemy slow, heal triggers, hero dodge.
- Skill data in its own file and Sheets tab; respec for gold; auto-spend preset.
- Save schema 3 with a v2→v3 migration.
- Balance simulation (Python and C#) covers skills.

**Non-goals (later):** the Offence / Defence / Utility branches and Sigils (M7), free respec on prestige (M7, because prestige doesn't exist yet), slot conditions (R2-9 "later"), skill art beyond placeholder effects, armour penetration (with Constructs).
**Anti-goals:** one skill so strong that every build takes it; actives that only matter against bosses; a points screen that needs a guide; anything that equips or spends without the player when the preset is off.

## 2. Decisions
| ID | Decision |
|---|---|
| R2-8 (existing) | 1 skill point per 5 levels; gold respec mid-run, free on prestige; optional auto-spend preset |
| R2-9 (existing) | 3 active slots, auto-cast by priority, real-second cooldowns, cooldown reduction cap 50% |
| GDD §7 (existing) | ~12 nodes per branch in 4 tiers; a tier needs 5 points spent in the branch; effects only from data |
| **M6-1** | **Nodes have ranks** (option A): actives up to 3 ranks, passives up to 5; a Common tree holds 52 points, so points are never wasted before M7 adds more branches |
| **M6-2** | **Cooldowns keep running between fights** (option A): a ready skill fires on the hero's next attack in whatever fight; the same rule applies offline |
| **M6-3** | **Skill data lives in its own file** `skills_config.json` (option A), one row per node, mirrored byte-identical into Unity and shown as a new "Skills" tab in the Sheets workbook |

## 3. Rules in detail

### 3.1 Points and ranks
- Points earned = `floor(level / skill_point_every_levels)` (config, 5). Points spent = sum of ranks. **Unspent = earned − spent** (derived, never stored) **[Claude's call]**.
- A node in tier *t* (1–4) can take a rank once the hero has spent at least `(t−1) × tier_points` (config, 5) points in the tree. So tiers open at 0 / 5 / 10 / 15 points.
- Each node has `max_rank`: actives 3, passives 5 (data, per node).
- A rank costs 1 point; ranks go up one at a time.
- **Respec** resets every rank on the active hero and empties its loadout. The cost is `respec_base × respec_growth^(level−1)` gold **[Claude's call; first values from the simulation, aiming at about one region's worth of gold]**. It is refused if the purse can't pay it. A free respec on prestige arrives in M7.
- **Auto-spend preset** (R2-8): a per-hero "Spend skill points automatically" checkbox, like M4-2. When on, each new point goes to the next rank in the class's suggested order (a list in data). When the auto-spender raises an active to its first rank and a slot is free, it also slots that active. The checkbox is off by default and never acts while it's off (anti-goal).

### 3.2 Actives, loadout and cooldowns
- Each hero has **3 slots** (`active_skill_slots`, config). Slot order is the **priority**: when several actives are ready, the highest slot fires first. Only actives with rank ≥ 1 can be slotted.
- **Firing (M6-2):** at each hero attack, the engine checks the slots in order. The first ready active whose condition holds (e.g. Executioner needs the enemy below 30% HP) **replaces that attack** **[Claude's call]**. Its cooldown starts, and the next normal attack comes one attack interval later. A buff skill (War Cry) also uses up the attack.
- **Cooldown** = `base_cooldown × (1 − rank_cdr × (rank−1)) × (1 − cooldown_reduction)`. Cooldown reduction comes from passives (Wind Step, Focus) and is capped at `cooldown_reduction_cap` (config, 0.5).
- **Timers keep running (M6-2):** each hero's remaining cooldowns live in the game state. Fight time and the gaps between fights (`between_fight_s`, `enemy_respawn_s`) count down every timer. Offline catch-up uses the same path. Timers are per hero. An inactive hero's timers don't run, which doesn't matter in play because they start ready **[Claude's call]**.
- **Not saved** **[Claude's call]**: after a load every skill is ready. This avoids a save field and is at most one free cast.
- **Changing the loadout** or **switching hero** never resets a timer. A newly slotted skill starts ready.

### 3.3 The three Common trees
The table shows the rank-1 value and the per-rank growth (all numbers are first values to be tuned in the simulation and live in `skills_config.json`).

**Fighter** (durable melee, Counter)
| Tier | Node | Type | Effect (rank 1 → per rank) |
|---|---|---|---|
| 1 | Heavy Swing | active | hit for 250% (+50%/rank), 8 s (−5%/rank) |
| 1 | Toughness | passive | +6% max HP (+6%) |
| 1 | Iron Skin | passive | +8% armour (+8%) |
| 2 | Rend | active | hit 120% + Physical bleed of 100% (+40%) over 4 s, 10 s |
| 2 | Second Wind | passive | once per fight, below 30% HP heal 15% max HP (+5%) |
| 2 | Riposte | passive | +3% Counter chance (+3%) (counts toward the Counter cap, D-033) |
| 3 | War Cry | active | +30% damage (+10%) for 6 s, 20 s |
| 3 | Brutality | passive | +10% crit damage (+10%) |
| 3 | Stand Firm | passive | −5% damage taken from bosses (−5%) |
| 4 | Executioner | active | hit for 500% (+100%) when the enemy is below 30% HP, 15 s |
| 4 | Bloodlust | passive | heal 1% of damage dealt (+1%) |
| 4 | Unbreakable | passive | once per boss fight, a killing blow leaves 1 HP and heals 5% (+5%) |

**Archer** (fast and precise)
| Tier | Node | Type | Effect |
|---|---|---|---|
| 1 | Aimed Shot | active | hit for 220% (+40%), always crits, 7 s |
| 1 | Quick Hands | passive | +4% attack speed (+4%) |
| 1 | Keen Eye | passive | +2% crit chance (+2%) |
| 2 | Venom Arrow | active | Nature damage of 150% (+50%) over 5 s, 10 s |
| 2 | Evasive | passive | 3% (+3%) chance to dodge an enemy hit; hero dodge capped at `hero_dodge_cap` (config) |
| 2 | Hunter's Mark | passive | +8% damage (+8%) vs Beasts and Brute-archetype enemies |
| 3 | Volley | active | 5 quick hits of 60% (+10%) each, 16 s |
| 3 | Deadeye | passive | +10% crit damage (+10%) |
| 3 | Wind Step | passive | −4% cooldowns (−4%) |
| 4 | Piercing Arrow | active | hit for 400% (+80%) that cannot miss, 18 s |
| 4 | Momentum | passive | each hit +1% attack speed (+0.5%), up to 10 stacks, reset each fight |
| 4 | Steady Aim | passive | +6% damage (+6%) while above 50% HP |

**Mage** (elemental burst; each active has its own element, so the loadout picks the elements)
| Tier | Node | Type | Effect |
|---|---|---|---|
| 1 | Firebolt | active | **Fire** hit for 260% (+50%), 8 s |
| 1 | Attunement | passive | +6% elemental damage (+6%) (stacks with the Mage class line, M5-1) |
| 1 | Arcane Ward | passive | +5% max HP (+5%) |
| 2 | Frost Lance | active | **Water** hit for 200% (+40%); enemy attacks 30% slower for 4 s, 10 s |
| 2 | Focus | passive | −4% cooldowns (−4%) |
| 2 | Mana Shield | passive | −3% damage taken (−3%) |
| 3 | Thornbind | active | **Nature** damage of 220% (+60%) over 6 s, 12 s |
| 3 | Overload | passive | +12% crit damage on skills (+12%) |
| 3 | Elemental Lore | passive | +8% damage (+8%) vs Elementals and Wizards |
| 4 | Radiant Nova | active | **Light** hit for 450% (+90%), 20 s |
| 4 | Echo | passive | 4% chance (+4%) that an active fires twice (no second cooldown) |
| 4 | Glass Cannon | passive | +8% damage (+8%) and −4% armour (−4%) |

### 3.4 How skill damage works [Claude's call]
- A **skill hit** is a normal hit (GDD §4.2 components, element matrix, resistance, triangle, crit) with its damage scaled by the skill's percentage. If the skill has an element, that element **replaces the Physical component** (the gear's "adds" components still apply). The Mage's +% elemental applies to it.
- Skill hits roll hit chance and crit like normal hits, unless the skill says otherwise (Aimed Shot always crits, Piercing Arrow cannot miss). A skill hit can trigger nothing else (no Counter).
- **Damage over time:** total = skill percentage × the hit amount computed at cast (no crit) **[Claude's call]**, dealt in equal ticks every `dot_tick_s` (config, 1 s). Recasting the same skill refreshes it instead of stacking. Ticks can kill. A DoT ends with the fight.
- **Timed buffs and the enemy slow** last their duration in fight time and end with the fight.
- **Echo** repeats the whole skill once, right after the first cast.

### 3.5 Effect types (D-032)
Each node is a list of effect components; adding a skill that combines existing types is data only.
| Type | Parameters | Used by |
|---|---|---|
| `hit` | percent, element?, hits, always_crit, cannot_miss, condition? | Heavy Swing, Aimed Shot, Volley, Firebolt, Executioner… |
| `dot` | percent, element, duration | Rend, Venom Arrow, Thornbind |
| `buff` | stat, value, duration | War Cry |
| `enemy_slow` | value, duration | Frost Lance |
| `stat` | stat, value (permanent, feeds the stat builder) | Toughness, Iron Skin, Keen Eye, Glass Cannon… |
| `conditional_damage` | value, condition (vs family / archetype / boss / own HP above x) | Hunter's Mark, Steady Aim, Elemental Lore, Stand Firm |
| `trigger` | kind (heal_below, cheat_death, lifesteal, momentum, echo) + values | Second Wind, Unbreakable, Bloodlust, Momentum, Echo |

New combat stats: hero dodge chance, cooldown reduction, skill crit damage, lifesteal, damage taken reduction (separate from armour). They are added to the stat builder; gear doesn't roll them in M6.

## 4. Approaches considered
| Approach | Verdict |
|---|---|
| **Own file `skills_config.json` + Sheets "Skills" tab (M6-3)** | **Chosen.** One readable row per node; new skills are new rows |
| A `skills` section at the end of `balance_config.json` | Rejected: ~200 extra rows in an already long Config tab |
| Skills in code | Rejected by D-032 |

**Combat:** the engine gets a skill runner inside `CombatEngine.Resolve`, fed by a per-hero skill state (ranks → loadout, cooldowns) and returning the updated cooldowns. There's one sensible way here: fights are already resolved event by event and replayed for display, so skills become more events on the same timeline (DoT ticks and buff ends join the hero and enemy attacks as "next event" sources).

## 5. Data
- **`Tools/BalanceSim/skills_config.json`** is the authority, copied byte-identical to `Assets/_Game/Data/skills_config.json`; a test enforces it, as with the balance file. Shape: `{ "nodes": [ { "id", "class", "tier", "type": "active|passive", "max_rank", "cooldown_s", "rank_cdr", "effects": [ { "kind", …values, "per_rank": {…} } ] } ], "suggested_order": { "fighter": [...], … } }`. Stable string IDs (D-032).
- **Names and descriptions** shown to the player live in `look_config.json` under `skills` (text, not balance) **[Claude's call]**.
- **New section `skill_rules` at the end of `balance_config.json`:** `tier_points`, `cooldown_reduction_cap`, `respec_base`, `respec_growth`, `dot_tick_s`, `hero_dodge_cap`. Appended, so no Config row moves. The existing `skill_point_every_levels` and `active_skill_slots` stay where they are.
- **Sheets:** a new "Skills" tab with one row per node and its rank values, plus the new Config rows.

## 6. Save
- **Schema 3:** each hero gains `skills: { nodeId: rank }`, `loadout: [nodeId|null ×3]`, `autoSpendSkills: bool`. Migration v2→v3 adds them empty. Ranks are saved by node ID.
- **Fixtures:** `v1.dfrs` and `v2.dfrs` keep loading forever (through the chain); a new `v3.dfrs` is added and never regenerated.
- **Unknown node IDs** in a save (e.g. a node removed in a later version) are dropped and their points refunded, because unspent points are derived **[Claude's call]**.

## 7. Unity
A **Skills** screen opened from a new button next to Bag, for the active hero:
```
┌──────────────── Skills · Fighter ── 3 points ┐
│ Loadout (priority ↓)  [1 Heavy Swing][2 Rend][3  —  ] │
│ Tier 1 ● Heavy Swing 2/3  ● Toughness 1/5  ○ Iron Skin 0/5 │
│ Tier 2 ● Rend 1/3         ○ Second Wind     ○ Riposte       │
│ Tier 3 🔒 10 points spent                                      │
│ Tier 4 🔒 15 points spent                                      │
│ [ ] Spend points automatically      [Reset tree · 1 240 g] │
└────────────────────────────────────────────────┘
```
- Tapping a node shows its description, current and next rank, and "+1 rank". Tapping an active also offers "Put in slot 1/2/3". Slots can be reordered (move up/down).
- Combat view: a skill cast shows the skill name briefly above the hero and the damage number in its element colour; DoT ticks show small numbers; a buff shows a small icon on the HUD while active. Placeholder art only (simple flashes).
- HUD: a dot on the Skills button when points are unspent.
- The offline report adds nothing new.

## 8. Balance and simulation
- **Python simulation:** reads `skills_config.json`. Profiles spend points by the suggested order and use the first 3 actives. Actives are modelled as average contributions (damage per cooldown, buff uptime, DoT totals), passives as stats or conditional multipliers.
- **C# runner:** plays the real engine with skills and reports per-class damage per second with and without the tree. It cross-checks the Python averages within a tolerance (5%) **[Claude's call]**.
- **Targets [Claude's call]:** with points spent by the suggested order, skills add about **+20–30% effective damage per second at region 5** (10 points) and about **+50–70% with a full tree**; the three classes stay within the M5 band (reach region 5 within ±25% of each other). The 30-day reach of each profile goes up; the report shows the change and the owner judges it at the playtest. Golden values are regenerated.
- **Performance (roadmap acceptance):** a fight with three actives, DoTs and buffs resolves fast enough that 12 hours offline still simulates in under the M4 budget; a test measures it.

## 9. Testing (test first, Core)
- Loading: both skill files parse; the Unity copy is byte-identical; every node ID referenced in `suggested_order` and `look_config` exists.
- Points: earned/spent/unspent, tier gates, max ranks, respec cost and refusal.
- Engine: each effect type, table-driven; priority order; condition skip (Executioner); cooldowns carried across fights and the gap time; cooldown reduction cap; DoT refresh and kill by tick; Echo; dodge cap; Unbreakable once per boss fight; normal enemies still never kill (R2-1c). **No skills = identical results to M5** (determinism and golden safety).
- Auto-spend: follows the order, slots new actives into free slots, does nothing when off; works offline.
- Save: v3 round-trip, v2→v3 migration, v1 and v2 fixtures still load, unknown node IDs refunded.

## 10. Left to the builder
Exact numbers (through the simulation), the engine's internal structure (keeping exact M5 results when no skill is used), UI placement details, test names, the order of implementation tasks.
