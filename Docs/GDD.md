# GDD.md — Game Design Document

> **Status:** v0.1 DRAFT for review. **Working title:** *TBD* (Creative Direction is a later deliverable).
> **Authority:** The Master Studio Prompt plus this GDD once approved. Numbers live in GAME_BALANCE.md.
> **Tags:** each rule is marked **[SPEC]** (from the master prompt), **[APPROVED]** (your decisions), or **[PROPOSED]** (pending your approval; DECISIONS.md lists them).

---

## 1. Vision

A dark fantasy idle RPG for Android: three heroes take turns fighting through an endless, darkening world. Progress comes from loot, builds and permanent prestige power, never from paying.

### 1.1 Pillars
1. **Always progressing.** Something gets stronger every session, online or offline.
2. **Build ownership.** Attribute points, skill trees, elements and gear choices matter. Auto-play never means no decisions.
3. **The wall is a friend.** Walls are readable ("you need 2× damage"), and prestige is the satisfying way through.
4. **Fair and respectful.** Gems buy time and looks, never power. No forced ads. Fully playable offline.
5. **Built to last.** Data-driven content: new regions, families, pets and artifacts come from data, not code changes.

### 1.2 Anti-goals
- Pay-to-win, energy or stamina gates, forced ads.
- Combat that needs manual input to be efficient.
- A final endpoint, or numbers that overflow and break.
- Systems that only exist to slow the player down without a choice attached.

---

## 2. Core loops

```mermaid
flowchart LR
  subgraph Seconds
    F[Auto-fight enemy] --> R[Gold · XP · drops]
  end
  subgraph Minutes
    R --> E[Equip / salvage / upgrade / craft]
    E --> P[Push next subzone → boss]
    P --> F
  end
  subgraph Hours-Days
    P --> W{Wall}
    W -->|farm, dungeons, pets| E
    W -->|prestige| D[Artifact Dust → artifacts]
    D --> F
  end
```

| Loop | Length | Player decisions |
|---|---|---|
| Combat | 1–5 s | none (auto) |
| Management | 1–10 min per session | gear, upgrades, stat points, skills, active character, zone, boss toggle |
| Region push | 15 min – 2 h | when to fight the boss, where to farm, which character |
| Dungeon | 3–5 min a run | which dungeon, spend or save tokens |
| Prestige | 1–3 days (early) → longer | when to reset, which artifacts to buy |

---

## 3. Characters

**Purpose:** three play styles with independent growth; only the active character fights.

**Rules**
- [SPEC] Fighter, Archer and Mage are all available from the start. Exactly **one active character**; switching is free and instant outside boss fights.
- [SPEC] Each character has its own level, XP, attributes, skill points, skill trees and equipped items.
- [PROPOSED] **Only the active character earns XP.** Inactive characters do not level. *Alternative:* inactive characters get 10% XP. Rejected as default, because it erodes the meaning of choosing.
- [APPROVED D-004] Items and pets are in a **shared inventory**. An item can be equipped on **only one character at a time**. Equipping it elsewhere unequips it from the previous owner, after a confirmation prompt.
- [PROPOSED] Item level requirement: `character level ≥ item level − 5`. This stops a level 300 Fighter's gear from carrying a level 1 Mage.
- [SPEC] Class triangle: **Mage > Archer > Fighter > Mage** (§4.3).

| Class | Primary | Role | Combat identity |
|---|---|---|---|
| Fighter | STR | Durable melee | High HP and armour, cleave and bleed skills, slower attacks |
| Archer | DEX | Fast, precise | High attack speed, crit and accuracy, multi-shot |
| Mage | INT | Burst caster | Elemental damage %, spells with cooldowns, low HP |

**Edge cases:** switching mid-fight is disabled (button greyed out during the current fight). An inactive character keeps its gear, and gear equipped on an inactive character counts as "in use".
**Acceptance:** two characters cannot reference the same item instance. Switching preserves every character's state exactly.

---

## 4. Combat

**Purpose:** deterministic, fully automatic fights that are independent of animation.

### 4.1 Rules
- [SPEC] Combat logic runs in the core simulation. Animations only *display* events. With animations off, the same results come out.
- [PROPOSED] **Event-driven resolution:** each combatant schedules its next action at `t + interval`. The engine processes events in time order, which avoids per-frame ticking. Online, events are released to the UI in real time; offline, they are processed instantly.
- [PROPOSED] One enemy at a time. The next enemy spawns 0.6 s after a kill.
- [PROPOSED] **HP fully restores between normal fights.** There is no healing economy in v1. Bosses and dungeon bosses are fought at full HP.
- [PROPOSED] **Defeat in a normal fight:** the character retreats one subzone and continues farming, and the "push" resumes automatically after 60 s. No death penalty beyond lost time.

### 4.2 Damage pipeline [PROPOSED]
Each hit is split into **components** (Physical plus any elemental damage from gear or skills). Each component is multiplied separately:

```
component = base
          × (1 + Σ%damage + Σ%element[e] + Σ%vsFamily[f])     (additive within a pool)
          × ElementMatrix[e][enemyElement]                        (§4.4)
          × (1 − EnemyResist[e])                                  (−0.5 … 0.75)
          × ClassTriangle                                         (§4.3)
          × Crit (if crit: CritDamage)
total = Σ components × (1 − DR)   (DR from target armour vs attacker level)
```
Order matters for testing: it is fixed and documented in code (`DamageCalculator`).

### 4.3 Class triangle [PROPOSED numbers]
Each enemy has a **combat archetype** that maps it onto the triangle: *Brute* (Fighter-like), *Marksman* (Archer-like), *Caster* (Mage-like). Bosses have one archetype.

| | vs advantaged target | vs disadvantaged target |
|---|---|---|
| Damage dealt | ×1.20 | ×0.85 |
| Damage taken from it | ×0.85 | ×1.20 |

Mage beats Marksmen, Archer beats Brutes, Fighter beats Casters. This makes the **active character choice** a real decision per region.

### 4.4 Elements [SPEC table, PROPOSED multipliers]
Strong = **×1.5**, Weak = **×0.75**, Neutral = ×1.0. Rows are attack elements, columns are the target's element.

| Attack ↓ / Target → | Fire | Nature | Water | Light | Dark | Physical (no element) |
|---|---|---|---|---|---|---|
| Fire | 1 | **1.5** | 0.75 | 1 | 1 | 1 |
| Nature | 0.75 | 1 | **1.5** | 1 | 1 | 1 |
| Water | **1.5** | 0.75 | 1 | 1 | 1 | 1 |
| Light | 1 | 1 | 1 | 0.75 | **1.5** | 1 |
| Dark | 1 | 1 | 1 | **1.5** | 0.75 | 1 |
| Physical | 1 | 1 | 1 | 0.75 | 0.75 | 1 |

> **Spec ambiguity flagged (Q-1):** the spec table has Water strong against Fire and Fire weak against Water, which is consistent. But "Light vs Light: Weak" and "Dark vs Dark: Weak" mean same-element attacks are resisted, while **Light vs Dark: Strong** and **Dark vs Light: Strong**. I implemented it exactly as written. Please confirm this is intended (it makes Light and Dark mutual counters).

Enemies have exactly one element (or none = Physical). Elemental damage comes only from gear and skills [SPEC].

### 4.5 Accuracy, crit, speed [PROPOSED]
- `HitChance = clamp(0.92 · Acc / (Acc + Eva/4), 0.30, 0.99)`. Accuracy comes from DEX and gear; evasion is an enemy stat that rises in "agile" families (Beasts).
- Crit: chance cap 75%, base damage 150%.
- Attack interval floor 0.4 s, so attack speed can't run away.

### 4.6 Boss encounters
- [SPEC] Available after K(r) kills in the region's last subzone (GAME_BALANCE §2).
- [SPEC] Toggle **"Fight boss when available"** (default ON). When OFF, the character farms the last subzone indefinitely.
- [PROPOSED] **60 s time limit** (enrage). Failure returns the character to farming, with automatic retry every 60 s while the toggle is ON.
- [APPROVED D-006] Bosses can be fought **offline** under the same rules.
- [SPEC] Defeated bosses are **repeatable**. [PROPOSED] Repeat fights need K(r)/2 kills in that region's last subzone; boss loot table only (no repeat unlock rewards).

**Acceptance (combat):** same seed + same state ⇒ identical event log. Toggling animations doesn't change any reward. All multipliers have unit tests at their boundaries.

---

## 5. Attributes & levelling

**Rules**
- [SPEC] Attributes: STR, DEX, INT, VIT, DEF, Critical Chance, Critical Damage, Luck. The architecture allows adding more (data-driven `AttributeDefinition`).
- [SPEC/PROPOSED] **75 / 25:** 4 points per level, **3 automatic** by class template and **1 manual** (GAME_BALANCE §4.1).
- [PROPOSED] Manual points can be respecced for gold (cost scales with level). They reset for free on prestige.
- [PROPOSED] No level cap. XP curve in GAME_BALANCE §4.3.

| Attribute | Effect |
|---|---|
| STR / DEX / INT | +1.5% damage if it is the class primary. DEX also: +0.5% accuracy, +0.2% attack speed. INT also: +1% elemental damage |
| VIT | +2% max HP |
| DEF | +1% armour |
| Crit Chance | +0.2% crit chance |
| Crit Damage | +1% crit damage |
| Luck | Rarity weight bonus `Luck/(Luck+200)` (diminishing), +0.5% gold |

**Acceptance:** a level-up grants exactly 3 automatic + 1 manual point. Totals over any level range match the closed-form formula.

---

## 6. Equipment

### 6.1 Rules
- [SPEC] Slots: main hand, off-hand, helmet, chest, legs, boots, armguards, gloves, 2 rings, amulet, pet, 6 accessories.
- [PROPOSED] Accessory slots unlock at region bosses 2, 4, 6, 8, 10 and 12 (a drip of new power and decisions).
- [SPEC] Fixed base stats that scale with item level. [APPROVED D-003] **Rarity also scales base stats** (Common 1.0 → Mythical 2.5).
- [SPEC] Random attributes per rarity: 0/0/1/1/2/3.
- [SPEC] Generic vs class-specific items. [PROPOSED] Class items carry a bonus line (e.g. "+15% Cleave damage — Fighter only") that is inactive on other classes, shown greyed out.
- [PROPOSED] **Starter kit:** each class starts every run with a Common level 1 weapon. Without one, simulated runs got stuck at boss 1 (GAME_BALANCE §9.3).

### 6.2 Item model
`ItemInstance { id, definitionId, itemLevel, rarity, upgradeLevel, randomAffixes[], enchant?, locked, equippedBy? }`. Definitions (ScriptableObjects) say which slot, base stat lines and class bonus an item has. The off-hand can be a weapon (35% damage) or a shield (armour + block chance).

### 6.3 Accessories [PROPOSED]
Six utility slots with unique effects, e.g. *Rope* (+5% gold), *Silver Cross* (+20% damage vs Undead), *Clockwork Gear* (+3% attack speed), *Lucky Dice* (+10 Luck). There is no item-level scaling, so accessories are a horizontal, collection-driven layer.

### 6.4 Upgrading — gold, +5% base stats per level, max by rarity (GAME_BALANCE §6.4).
### 6.5 Salvaging — gives materials by rarity + 50% of gold spent on upgrades. A bulk-salvage filter ("all Common/Uncommon not better than equipped") and item locking protect valuables.
### 6.6 Crafting [PROPOSED] — spend materials to create an item for **a chosen slot** at the highest unlocked subzone level, with rarity rolled normally. This is the tool against bad luck.
### 6.7 Enchanting [PROPOSED]
Each Rare+ item has **one enchant socket**, separate from its random attributes. Enchanting rolls an enchant from the item's slot table and costs materials + gold (the main gold sink, finding B-2). Re-enchanting replaces it. Items keep their enchant when upgraded.

**Edge cases:** the inventory is full (200 slots, expandable with gold) → new drops are auto-salvaged by the filter and appear in the report, never silently lost. A locked item cannot be salvaged.
**Acceptance:** item generation is deterministic per seed. Rarity frequency over 1M rolls stays within statistical tolerance (chi-square p > 0.01).

---

## 7. Skill trees

**Rules**
- [SPEC] Per class: **Common branch** (available) + **Offence / Defence / Utility** (unlocked by prestige), with class-adapted skills.
- [SPEC] Skills are passive effects or automatic combat abilities.
- [PROPOSED] **Skill points:** 1 per 5 levels. They reset with levels on prestige; tree *unlocks* are permanent.
- [PROPOSED] **Branch unlock (Q-2, pending):** Branches unlock with an artifact called **Sigil**, bought with Artifact Dust. The first branch for a class costs 15 dust (affordable right after prestige 1–2), the second 100, the third 1 000. Unlocks are per class.
  *Alternatives:* (a) unlock at prestige counts 1/3/6. Simpler, but no choice. (b) per-character dust. Too slow with 3 characters.
- [PROPOSED] **Active skills:** up to 3 equipped, fired automatically on cooldown (priority order set by the player). Example Fighter Common branch: *Heavy Swing* (active, 250% damage, 8 s cooldown), *Toughness* (passive, +HP%), *Second Wind* (passive, heal 20% when below 30%, once per fight).
- Each branch has about 12 nodes in 4 tiers. A tier needs 5 points spent in the branch.

**Acceptance:** skill effects come only from data (`SkillDefinition` + effect components). Unlocks survive prestige; points don't.

---

## 8. World

### 8.1 Structure
- [SPEC] Regions → **5 subzones** [PROPOSED count] → region boss. Region N+1 unlocks when boss N is defeated.
- [SPEC] Each subzone has an enemy family, element mix, archetype mix, level and loot table.
- [PROPOSED] Region definitions are data. Region 1–10 are hand-authored for v1. Region 11+ can be **generated from templates** (family × element × biome palette), so content can reach hundreds of zones without new art for each one.

### 8.2 Enemy families [SPEC]
Humanoids, Beasts, Undead, Demons, Elementals, Constructs, Dragons. Data-driven, so new families can be added. Family traits [PROPOSED]: Beasts have evasion, Constructs armour, Undead are Dark, Elementals have an element, Demons have high damage, Dragons high HP, and Humanoids are mixed archetypes.

### 8.3 Kill requirements — `K(r) = min(100, 10 + 5(r−1))` per subzone.
### 8.4 Rare drops — loot tables support probabilities down to 0.01% (doubles; RNG tested, §TESTING).
### 8.5 Regions 1–10 for v1 — themes come from Creative Direction (pending).
### 8.6 Farming old zones [OPEN, finding B-3]
The simulation shows that frontier farming beats old-zone farming. Proposed fixes, so old zones keep value:
1. **Zone-exclusive loot:** family accessories and scrolls only drop in specific subzones.
2. **Subzone mastery:** 1 000 kills in a subzone permanently gives +1% gold and drop rate (account-wide, survives prestige).

---

## 9. Pets

- [SPEC] Collectible, start at level 1, have rarities, give passive bonuses, are upgraded with dungeon-only items, and get active effects at higher rarity. The collection is shared.
- [PROPOSED] One equipped per character; a pet can be equipped by one character at a time (same rule as D-004).
- [PROPOSED] **Pet Soul Shards** (per pet) and **Pet Essence** (generic) drop only in dungeons. Level-up costs Essence. Every 10 levels needs Shards ("star" breakpoints).
- [PROPOSED] Active effects from Epic+: e.g. 10% chance to attack twice, cleave 30% of overkill to the next enemy, +5% crit chance, +10% item drop rate.
- v1: 12 pets (2 Common, 3 Uncommon, 3 Rare, 2 Epic, 1 Legendary, 1 Mythical).
- Pets survive prestige [PROPOSED: yes, the collection is account-level].

---

## 10. Dungeons

- [SPEC] Repeatable, slower or less efficient than zones, with better potential rewards. Tokens are **specific to each dungeon**.
- [PROPOSED] One dungeon unlocks every 2 regions (v1: 5 dungeons). A run = 5 waves + dungeon boss, about 3–5 minutes. No keys or stamina (pillar 4).
- Rewards: tokens (always), Pet Essence and Shards, items at Rare+ rarity floor.
- [SPEC] Token uses:
  - **Lootbox:** costs 1 token, rolls the dungeon's chest table.
  - **Choose outcome:** price = `ceil(1 / p)` tokens, where p is the item's chest probability. For example, a 1% item costs 100 tokens [SPEC example]. Fairness factor 1.0, configurable.
- [OPEN Q-3] Can dungeons auto-repeat **offline**? Proposed: yes, at offline efficiency, but only when the player selects a dungeon before leaving.
- Dungeon difficulty is pinned to its unlock region. A "Heroic" tier (×4 HP, better tables) unlocks later.

---

## 11. Prestige & artifacts

**Rules**
- [SPEC] Prestige resets **all three characters** (levels, XP, attributes, skill points), normal zone progress, normal equipment and its upgrades.
- [SPEC] It keeps artifacts, permanent scrolls, cosmetics and prestige-exclusive equipment. [PROPOSED] It also keeps the pet collection, skill-tree unlocks, dungeon tokens, gems, achievements and subzone mastery.
- [PROPOSED] Gold and materials reset (they are run currencies).
- [PROPOSED] Unlocks after defeating the region 4 boss. Dust formula in GAME_BALANCE §8.
- [PROPOSED] **Confirmation screen** lists exactly what resets and what's kept, plus the dust gain and the gain ratio vs current dust.

**v1 artifacts [PROPOSED]**
| Artifact | Effect per level |
|---|---|
| Might | ×1.10 damage |
| Bulwark | ×1.10 HP |
| Greed | ×1.10 gold |
| Wisdom | ×1.10 XP |
| Hourglass | +1 h offline cap (max +12) |
| Fortune | +2% item drop chance (additive, max +50%) |
| Sigils (×3 per class) | Unlock a skill branch (§7) |
| Swiftness | Start new runs with region N unlocked (N = level × 2, max = 50% of best) |

**Prestige-exclusive equipment [OPEN Q-4]:** how does it stay relevant at any level? Proposal: it uses **item level = character level** (it scales with you). It drops only after prestige 3+, from region bosses, at 0.5%.

**Acceptance:** after prestige, a diff of the save matches the keep/reset table exactly (automated test). Dust is paid exactly once per prestige.

---

## 12. Economy

| Currency | Earned | Spent | Prestige |
|---|---|---|---|
| Gold | kills, bosses | upgrades, enchanting, respec, inventory expansion | resets |
| Materials | salvage, bosses | crafting, enchanting | resets |
| Gems | achievements, events, purchase | cosmetics, speedups | kept |
| Artifact Dust | prestige | artifacts | kept |
| Dungeon Tokens (×dungeon) | dungeon runs | that dungeon's chests | kept |

[APPROVED D-001] **Gems** never buy equipment, stats or permanent power. **Speedups** are "pay to progress faster, never further" (D-001). [PROPOSED] A speedup = **Time Flask**: instantly simulates N hours at offline efficiency (it doesn't bypass the offline cap mechanics). Earnable for free through achievements.

---

## 13. Offline progression

- [SPEC] Kills, gold, drops, materials, XP, boss kills [D-006], elapsed time. Capped, less efficient than active play.
- [PROPOSED] 50% efficiency, 12 h cap (Hourglass up to 24 h). Uses the **same engine** as active play, so there's one code path.
- [PROPOSED] Loot is held during offline time. On return, the **offline report** shows time away, time credited, kills, bosses, levels, gold, materials, items by rarity, auto-salvaged count, scrolls found. Then "Collect".
- [PROPOSED] **Clock safety:** if the device clock is earlier than the last save, credit 0 and resync. Clock jumps beyond the cap are clamped by design.

---

## 14. Retention & onboarding (summary; full plan by Product)
- A guided first 10 minutes: fight → equip → boss → second character → stat point → offline teaser.
- Achievements (gems), daily login streak (gems, no power), collection log (pets, accessories, scrolls).
- Features unlock gradually: crafting at region 2, dungeons at region 2, enchanting at region 3, prestige at region 4.

---

## 15. Open questions (need your decision)

| ID | Question | Recommendation |
|---|---|---|
| Q-1 | Light/Dark matrix semantics (§4.4) | Keep as written |
| Q-2 | Skill branch unlock method (§7) | Sigil artifacts bought with dust |
| Q-3 | Dungeons offline (§10) | Yes, at offline efficiency, if preselected |
| Q-4 | Prestige-exclusive gear scaling (§11) | Item level = character level |
| Q-5 | Should inactive characters earn XP (§3)? | No |
| Q-6 | Farming old zones (§8.6, B-3) | Zone-exclusive loot + subzone mastery |
| Q-7 | Early-prestige tuning (B-1) | 3× dust on prestiges 1–3 |
