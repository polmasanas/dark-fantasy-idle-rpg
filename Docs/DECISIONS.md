# DECISIONS.md — Decision Log

> Each entry: ID · date · decision · reason / notes. Status: **APPROVED** (by the project owner), **PROPOSED**, **SUPERSEDED**.
> Rule: no mechanic changes silently. Every change to an approved decision gets a new entry that references the old one.
> All numbers mentioned here live in `Tools/BalanceSim/balance_config.json` and stay configurable (D-032).

## Approved — 2026-11-19 (design review session)

### Project & technology
| ID | Decision | Notes |
|---|---|---|
| D-005 | GitHub repo `dark-fantasy-idle-rpg`, Git LFS for binaries, UnityYAMLMerge for scenes and prefabs | Jira project `DFIRPG` |
| D-010 | **Unity 6 LTS + URP with 2D Renderer** | 2D lights for dark mood; Built-in is maintenance-only |
| D-011 | **UI Toolkit for menus and management screens; sprites (+ uGUI overlays) for the combat view** | Data-heavy screens, list virtualisation |
| D-012 | **Game.Core is pure C# (no UnityEngine)**, tested with Unity Test Framework and `dotnet test` | Logic testable outside the editor |
| D-013 | **Event-driven deterministic combat; seeded PCG32 RNG stored in the save** | Same state + seed ⇒ same results; fast offline simulation |
| D-030 | **Save codec is swappable:** JSON (gzip) during development; a binary codec (e.g. MessagePack) added at release (M12). Header identifies the codec, and the game reads both formats forever. Migrations work on a format-neutral data tree. String enums, atomic write, 3 backups, autosave 30 s + on pause + after critical actions | Binary is obfuscation, not anti-cheat |
| D-031 | **Three balance tools maintained in parallel:** Python simulation, Google Sheets (generated view + Sandbox tab), C# simulation runner on the real Game.Core (from M1). `balance_config.json` is the single authority; golden tests keep Python and C# identical | Owner wants different visualisations |
| D-032 | **Data-driven extensibility:** attributes, class growth templates, formula constants, skills, affixes, enchants, pet effects, item types, regions and families are data. New *kinds* of mechanics need code once; variations are data. Saves reference everything by stable string ID | Owner requirement: "everything can be changed or added later" |

### Monetisation
| ID | Decision | Notes |
|---|---|---|
| D-001 | Gem **speedups allowed**: "pay to progress faster, never further" | Free and paying players can reach identical rewards |
| D-002 | Gems never buy equipment, stats or permanent power; enchanting and crafting never use gems | Master spec |

### Characters & attributes
| ID | Decision | Notes |
|---|---|---|
| D-014 | **4 points per level: 3 automatic (class template) + 1 manual** = 75/25, plus an **auto-allocate button** following a player-chosen preset. Manual points: gold respec, free on prestige | |
| D-033 | **New 9th attribute: Counter.** When hit, chance to counter-attack immediately for 60% damage (no delay to the next attack). +0.4% per point, cap 40%. Available to all classes (manual points, gear); automatic only for the Fighter | Fighter signature stat |
| D-034 | **Class templates per level:** Fighter STR 1.5 · VIT 1 · DEF 0.25 · Counter 0.25. Archer DEX 1.5 · VIT 0.75 · DEF 0.5 · Crit Chance 0.25. Mage INT 1.5 · VIT 0.75 · DEF 0.25 · Crit Damage 0.5 | Fractions via 4-level integer cycle |
| D-024 | **Only the active character earns XP; catch-up bonus +50% XP** while below the highest character's level | Configurable |
| D-025 | **Item level requirement:** character level ≥ item level − 5 | |

### Combat
| ID | Decision | Notes |
|---|---|---|
| D-017 | **Class triangle via enemy archetypes** Brute / Marksman / Caster. Advantage: ×1.20 dealt, ×0.85 taken; disadvantage: the reverse | Mage > Marksman, Archer > Brute, Fighter > Caster |
| D-019 | **Elements:** Strong ×1.5, Weak ×0.75; stacks with per-enemy resistances (−50%…+75%) | |
| D-035 | **Light/Dark matrix exactly as the spec table:** same-element Weak, Light ⇄ Dark Strong, Physical Weak vs both | Confirmed intended (Q-1) |
| D-018 | **HP fully restores between normal fights** | |
| D-006 | **Bosses can be fought offline** under the "Fight boss when available" toggle | |

### World & loot
| ID | Decision | Notes |
|---|---|---|
| D-016 | **5 subzones per region, 2 levels each (10 per region); kills 10 + 5/region, cap 100; 10 regions at launch** | Per-region overrides allowed |
| D-015 | **Starter class weapon every run + main-hand drop weight ×3** | |
| D-003 | **Rarity scales base stats** (Common 1.0 … Mythical 2.5) | Distinguishes Common from Uncommon |
| D-004 | An item or pet can be **equipped by one character at a time** | Confirmation prompt when moving |
| D-026 | **Crafting:** choose the **slot**; **item type** (from that slot's pool, e.g. 3 helmet types) and **rarity** are random; item level = highest unlocked subzone; cost 20 + 0.5 × item level materials | Open detail: regional subsets of item types |
| D-027 | **Enchanting:** one socket on Rare+ items, rerollable, costs materials + gold. **Each re-enchant on the same item multiplies its next gold cost (×1.5)**: the late-game gold sink | Counter stored per item |
| D-036 | **Old zones stay valuable:** zone-exclusive loot + **subzone mastery** (a star per 1 000 kills, max 5; +1% gold and +1% drop chance per star, account-wide, kept on prestige) | Watch: stacks to +58–141% in 30 days (GAME_BALANCE B-6) |

### Prestige & meta
| ID | Decision | Notes |
|---|---|---|
| D-021 | **Prestige unlocks at the region 4 boss; dust = 10 × 1.5^(R − 4)** | |
| D-037 | **×3 dust on the first 3 prestiges** | Fixes early-grind (Q-7) |
| D-022 | **Artifacts multiplicative ×1.10 per level, exponential cost.** v1 list: Might, Bulwark, Greed, Wisdom, Hourglass, Fortune, Swiftness, Sigils | |
| D-028 | **Skill branches unlock via Sigil artifacts** bought with dust: per class 15 / 100 / 1 000 | Unlocks permanent; points reset |
| D-023 | **Prestige keeps:** artifacts, scrolls, cosmetics, prestige-exclusive gear, pets (+ Pet Essence & Shards), skill-branch unlocks, gems, achievements, statistics, mastery. **Resets:** characters, zone progress, normal gear and inventory, gold, materials, **dungeon tokens**, dungeon unlocks | Owner: tokens reset because they drive gear progression. Token prices must be reachable within one run |
| D-038 | **Prestige-exclusive equipment:** item level = wearer's level; **extremely rare** (~0.05% per region boss kill, from prestige 3); **strong but situational** effects (vs family, boss fights, low HP…); modest base stats (×1.2), so it never fully replaces normal drops | Q-4 |
| D-020 | **Offline: 50% efficiency, 12 h cap (24 h with Hourglass)**; loot held until return; clock rollback ⇒ no credit | |

### Dungeons
| ID | Decision | Notes |
|---|---|---|
| D-029 | **Dungeons:** one per 2 regions (5 in v1), 5 waves + boss (~3–5 min); **unlimited runs, no keys or stamina**, balanced to be less efficient than zones for XP and gold; tokens per dungeon; lootbox = 1 token; choose outcome = ceil(1/p) tokens | |
| D-039 | **Dungeons run offline** at offline efficiency, if selected before leaving | Q-3 |

## Approved — project setup (M1)
| ID | Decision | Notes |
|---|---|---|
| D-040 | **Unity version pinned: 6000.3.29f1 (Unity 6.3 LTS)**, via `ProjectSettings/ProjectVersion.txt`. Upgrades are deliberate, logged decisions | Supersedes the generic "Unity 6 LTS" in D-010 |
| D-041 | Project created from the **Universal 2D** template (URP 2D renderer, Input System, 2D animation packages). Android: IL2CPP, ARM64, min SDK 25 (template default). **Portrait only**, auto-rotation off; version 0.1.0 | Master spec: portrait orientation |

## Approved — 2026-11-19 (design review round 2: open items)
Referenced in the GDD as **R2-n**. Numbers live in the `combat` section of `balance_config.json`.

| ID | Decision |
|---|---|
| R2-1 | One enemy at a time; next enemy after 0.6 s |
| R2-1b | Attack interval = class base ÷ (1 + attack speed %), floor 0.35 s. Bases: Fighter 3.0 s, Archer 2.2 s, Mage 3.4 s (damage per hit scaled by base ÷ 1.6). **Haste** artifact: +5% attack speed per level, max 40 (cost 50 × 1.7^level, tuned in simulation) |
| R2-1c | **No death against normal enemies** (owner rule); HP matters in region boss fights and dungeons only. Escape timer 30 s (no rewards); 5 escapes in a row → retreat one subzone, retry after 60 s. Replaces the earlier "defeat in a normal fight" proposal |
| R2-1c+ | **Dungeons are survival runs** (owner rule): all enemies deal damage, HP carries across waves, no healing between waves |
| R2-1d | Dungeon death keeps cleared-wave rewards; boss chest only on full clear; **auto-repeat never stops automatically** (owner); failures shown prominently in reports |
| R2-2 | Region boss: 60 s time limit, auto-retry every 60 s |
| R2-3 | Repeat boss fights need K/2 kills; boss loot table only |
| R2-3b | Data-driven milestone rewards: account-first (once ever) and run-first (once per run) on bosses, dungeons and prestige counts |
| R2-4 | No level cap |
| R2-5 | Accessory slots unlock at first-ever kills of bosses 2/4/6/8/10/12, account-wide (all reachable at launch after R2-22) |
| R2-6 | Any class equips any item; class bonus lines only work for their class (shown greyed out otherwise); ~30% of drops are class items |
| R2-7 | Accessories: no item-level scaling, unique effects, rarity raises the effect, zone-exclusive drops, reset on prestige |
| R2-8 | 1 skill point per 5 levels; gold respec mid-run, free on prestige; optional auto-spend preset |
| R2-9 | 3 active skill slots, auto-cast by priority, real-second cooldowns, cooldown reduction cap 50%, optional slot conditions later |
| R2-10 | Hand-made headline regions + editor-generated connector regions, shipped as fixed data |
| R2-11 | One signature trait per enemy family (fully data-driven); new stat: armour penetration (gear and skills only) |
| R2-12 | Pets: Essence for levels, per-pet Soul Shards for 5 stars (every 10 levels, max 50), 10 Shards summon an unowned pet, duplicates → Shards. **Effective pet level = min(pet level, wearer's level)** (owner) |
| R2-13 | Pet active effects from Epic+; Legendary stronger; Mythical adds a second passive; cleave carries ≤ 50% of overkill |
| R2-14 | Prestige confirmation: summary, reset/kept lists, warnings, then hold-to-confirm (**setting: Hold / Tap**, owner); opens the artifact shop |
| R2-15 | Prestige button shows numbers only (dust now, dust at next boss, estimated time). Supersedes proposal P-001's verdict |
| R2-16 | Mastery stays +1% gold/drop per star, uncapped (owner: future content scales harder). Supersedes P-002. Drop chance above 100% gives extra drops |
| R2-17 | Time Flask speedups (1 h / 4 h / 8 h) at offline efficiency; daily limit 24 h, configurable; **unlimited in development builds** (owner) |
| R2-18 | Offline loot held and reported; **the game never equips items automatically** (owner); player-tapped "Equip all upgrades" button; auto-salvage filter applies |
| R2-19 | Clock safety: rollback = no credit, forward jumps capped, repeated-jump detection via uptime clock, optional online time check |
| R2-20 | Item-type pools: global pool + 1–3 regional themed types per slot; crafting includes themed types of regions unlocked this run (resolves Q-8) |
| R2-22 | **Launch with about 25 regions:** 10 hand-made + about 15 editor-generated connector regions (resolves Q-9). Dungeons stay at 5 for launch (one per 2 hand-made regions); connector regions reuse existing dungeons |
| R2-21 | Caps: hit chance 30–99%, **crit chance cap 100%** (owner; was 75%), crit damage uncapped (base 150%), damage reduction cap 75%, resist −50%…+75%, Luck `L/(L+200)`; fixed damage order (GDD §4.2); inventory 200 + 50 per gold expansion, no maximum |

## Proposed (new, awaiting approval)
| ID | Proposal | Doc |
|---|---|---|
| — | None open | — |

## Superseded
_None yet._
