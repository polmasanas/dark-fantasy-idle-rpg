# M5 design: three classes and the world

> **Status:** approved by the owner and implemented (merged), 2026-11-20. Decisions M5-1…M5-5 were taken one at a time in conversation; items marked **[Claude's call]** were not asked and can be overruled. Implements roadmap M5 (Jira DFIRPG-6).

## 1. Intent
The Fighter gets two companions, the **Archer** and the **Mage**, and the world gets **real enemy families** with elements and fighting styles. The point is a decision the player keeps making: *which hero do I bring here?* The class triangle and the elements make that choice matter region by region, while each hero grows separately (own level, points and gear) from one shared inventory and purse.

**Success looks like:** in the editor, a player beats the recruit, unlocks the Archer and Mage, switches freely between normal fights, notices that the Archer melts the Fen's beasts less well than expected (they dodge) and that Light gear cuts through the Vael Wastes' undead; the balance simulation shows all three classes progressing at comparable speed.

**Goals**
- Archer and Mage playable, with their own state, class weapons and identities (fast and precise; elemental burst).
- Class triangle (D-017) and elements (D-019, D-035) live in combat.
- Enemy families with traits (R2-11) for regions 2–5, one family per region.
- Hero switching and the class unlock.
- Saves move to schema v2 without losing v1 progress.
- Balance simulation (Python and C#) covers the three classes, the triangle and elements.

**Non-goals (later milestones):** skills and skill trees (M6), Constructs / Demons / Dragons and armour penetration (with regions 6–10), mixed-family regions, prestige (M7), pets and dungeons (M9).
**Anti-goals:** one hero that is best everywhere; switching that wipes progress; a Mage that plays like a Fighter with a different picture; a save that fails to load after the update.

## 2. Decisions (owner)
| ID | Decision |
|---|---|
| M5-1 | **Elemental damage comes from new gear lines** (option A): "adds X% of your hit as Fire / Nature / Water / Light / Dark damage", plus a Mage-only "+% elemental damage" class line. Keeps the rule "elements come only from gear and skills" |
| M5-2 | **One enemy family per region for now** (option A): 2 Ashen March = Humanoids · 3 Drowned Fen = Beasts · 4 Frost Pass = Elementals (Water) · 5 Vael Wastes = Undead (Dark). **Later regions will mix families** so that no single hero is always the obvious pick (owner's intent; design it in, build it later) |
| M5-3 | **Switching: instant during normal fights, locked during boss fights** (option A). The normal fight in progress is dropped (no rewards); the button is greyed out while a boss fight runs. Resolves the GDD's "free and instant" vs "disabled mid-fight" conflict |
| M5-4 | **The Archer and Mage unlock when the account first beats the region 1 boss** (Bram Hollis, the recruit duel). Account-wide, survives prestige. Amends the GDD's "all available from the start". New accounts start as the Fighter |
| M5-6 | Elementals are Fire / Nature / Water (rolled per enemy, never Light or Dark) and drop elemental gear lines 3× more often; **Wizards** are an Elemental sub-family, Light, in the Frost Pass's last subzone (owner, plan review) |
| M5-5 | Recorded intent from M5-2: region data must allow several families per region (subzone-level family), even though M5 uses one per region |

## 3. Rules in detail

### 3.1 Heroes and switching
- `GameState` holds **three character states** (Fighter, Archer, Mage) and the **active index**; everything that read "the character" reads the active one. Inventory, wallet, world progress and account flags are shared (GDD §3, D-004).
- **Locked heroes** (before M5-4's unlock) exist in the state at level 1 but cannot be selected; the switch button shows a padlock and "Beat the region 1 boss to unlock".
- **Switching** (M5-3): allowed when the unlock is done and the current fight is not a boss fight (training boss included). The replay of the dropped normal fight is abandoned like an offline catch-up (`FightReplayer.Abandon`); the world's kill counters do not change. Rewards of the dropped fight are lost.
- **XP:** only the active hero earns XP; +50% while below the highest hero's level (D-024, `player.catchup_xp_bonus` already in config).
- **Gear:** one wearer per item (D-004); equipping an item worn by another hero asks for confirmation, then moves it. Item level requirement `level ≥ item level − 5` (D-025) per hero. "Equip upgrades" works on the active hero only and never takes items worn by another hero **[Claude's call]**.
- **Auto-allocate** (M4-2) settings are per hero (they already live in the character state).
- **Offline:** time away is fought by the hero that was active when the game closed.
- **Starter weapons (D-015):** each run equips a Common **sword** on the Fighter, **bow** on the Archer, **staff** on the Mage; the Archer's and Mage's are created when they unlock.

### 3.2 Class weapons and elemental lines (M5-1) [Claude's call on the numbers; tuned by the simulation]
- New main-hand item types: **bow** (weapon damage, slightly lower base, + attack speed line) and **staff** (weapon damage + a fixed "+% elemental damage" line). Appended to `items.types.main_hand`; any class can equip any item (R2-6).
- New attribute lines in the pool (main hand, off hand, rings, amulets): `adds_fire`, `adds_nature`, `adds_water`, `adds_light`, `adds_dark`: "adds 8–16% of each hit as that element", scaling like other attributes; and a Mage class line `pct_elemental` (+10–20% to all elemental components). Appended at the end of the attributes list (Sheets rows stay put).
- A hit becomes components (GDD §4.2): Physical plus one component per "adds" element. Each component gets the element matrix, the target's resistance, the triangle and crit, then they are summed.

### 3.3 Triangle (D-017)
- Every enemy has an **archetype**: Brute, Marksman or Caster. Fighter > Caster, Archer > Brute, Mage > Marksman. Advantage ×1.20 damage dealt / ×0.85 taken; disadvantage the reverse. Values in config.
- Archetype comes from the enemy body in data (each of the 5 bodies of a family has one archetype), so the look matches the style. Bosses have one archetype each.
- **Region 1 is neutral** (no archetype on practice targets, the drillmaster or the recruit) **[Claude's call]**: only the Fighter exists there before the unlock, and the ON-1 tuning must not shift. Archetype is therefore optional in data (none = ×1.0 both ways).

### 3.4 Families and regions (M5-2)
| Region | Family | Element | Trait (R2-11, data) | Bodies (5) [Claude's call] | Boss [Claude's call, rename freely] |
|---|---|---|---|---|---|
| 1 Training Grounds | (Training look, Humanoid stats) | — | none | practice targets (existing), **no archetype** | Bram Hollis, First of the Recruits (**no archetype**) |
| 2 Ashen March | Humanoids | none | mixed archetypes | deserter (Brute), brute (Brute), spearman (Marksman), archer (Marksman), cultist (Caster) — existing | Oskar Venn, Captain of Deserters (Brute) |
| 3 Drowned Fen | Beasts | none | **evasion** (harder to hit) | fen wolf (Brute), bog boar (Brute), marsh stalker (Marksman), spitting toad (Marksman), wisp-moth (Caster) | Grisel, the Fen Mother (Brute) |
| 4 Frost Pass | Elementals (subzones 1–4) + **Wizards** (subzone 5) (M5-6) | Elementals: **Fire, Nature or Water**, rolled per enemy (Frost Pass weights Water); Wizards: **Light** | −75% damage of their own element; **elemental gear lines 3× more likely** on their drops | ice brute (Brute), shard thrower (Marksman), frost wisp (Caster), sleet golem (Brute), rime caller (Caster); wizards: battle magus (Brute), lancer adept (Marksman), lightcaller (Caster), warding monk (Brute), hierophant (Caster) | the Frozen Oath (Caster, element rolled like other elementals) |
| 5 Vael Wastes | Undead | **Dark** | Dark element (weak to Light, resists Dark) | ghoul (Brute), skeleton archer (Marksman), wight (Caster), bone hound (Brute), grave priest (Caster) | Dorran Vael, the Hollow Lord (Caster) |

- Trait magnitudes (evasion value, resistances) live in a new `families` config section; first values come from the balance simulation, aiming at "the right hero clears about 25–40% faster" **[Claude's call]**.
- Regions 6+ keep using the fallback (Humanoids) until M10 content; region data gets a per-subzone family field (M5-5) even though M5 fills one family per region.
- Look data: `look_config.json` kits gain `beasts`, `elementals`, `undead`; regions 3–5 point at them; boss names/bodies updated.

### 3.5 Unlock moment (M5-4)
When the region 1 boss falls for the first time on the account: the status line announces "The Archer and the Mage join you", the hero buttons unlock with a short highlight, and both heroes receive their starter weapons.

## 4. Approaches considered
| Approach | Verdict |
|---|---|
| **State holds all three heroes + active index; save schema v2 with a v1→v2 migration** | **Chosen.** Every system reads the active hero through one property; the shared parts stay shared; the existing migration chain (M4) converts old saves |
| Keep one "current character" and park the others in a side list, swapping on switch | Rejected: every system would have to know about the swap; easy to lose or duplicate gear on a swap |
| Separate save per hero | Rejected: shared inventory, wallet and world would be split across files |

## 5. Save
- **Schema v2:** `characters: [fighter, archer, mage]`, `activeCharacter: 0..2`, `account.heroesUnlocked: bool`. Migration v1→v2 moves the old `character` into slot 0 (Fighter), creates level-1 Archer and Mage, sets `heroesUnlocked = highestBossDefeated ≥ 1`.
- `v1.dfrs` keeps loading (through the migration) forever; a new `v2.dfrs` fixture is added and never regenerated.

## 6. Art (CD pipeline)
- People already come from `make_figures.py`; new bodies for **Beasts** (four-legged and winged shapes), **Elementals** (shapeless cores with a bright element core) and **Undead** (thin, tilted, broken outlines) need a creature generator (`make_creatures.py`) with rig parts (`body`, `head`, `legs`, `tail`, `core`…) and motions added to `look_config.json`.
- The Archer and Mage already exist as silhouettes; they get their class-coloured glows from look data.

## 7. Balance and simulation
- Python simulation and C# runner learn three classes, the triangle, elements, family traits and switching; profiles choose the best hero per region.
- **Acceptance (roadmap M5):** table-driven triangle and element tests; the simulation updated with classes; all three classes reach region 5 within ±25% of each other's time **[Claude's call on the band]**; golden values regenerated; GAME_BALANCE updated.

## 8. Unity
- Top bar: three hero buttons (portrait silhouettes in class colour; active highlighted; padlock when locked; greyed during boss fights).
- HUD shows the active hero's level, XP and points; Bag shows who wears what and asks before moving an item between heroes.
- Combat view: the hero silhouette follows the active class; elemental hits show their element colour on damage numbers.

## 9. Left to the builder
Exact numbers (through the simulation), creature shapes, UI placement details, test names, the order of implementation tasks.
