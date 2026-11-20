# M5 Three Classes & World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Archer and Mage playable alongside the Fighter, with the class triangle, elements from gear, four enemy families in regions 2–5, hero switching and the class unlock, saved in schema v2 and balanced by the simulation.

**Architecture:** Rules stay in Game.Core: new `Combat/Elements.cs` and `Combat/Triangle.cs` (pure, table-driven, values from `balance_config.json`), family/region data in two new config sections read by `EnemyFactory`, elemental gear lines through `ItemStats` → `StatInputs` → `CombatStats`, and `GameState` holding three heroes plus an active index. Saves move to schema v2 through the M4 migration chain. Look data (bodies, bosses, names) stays in `look_config.json`; Unity only draws.

**Tech Stack:** C# 9 (Unity 6000.3.29f1), NUnit via `dotnet test Core.Tests.Dotnet`, Python 3 balance simulation, cairosvg art pipeline, UI Toolkit.

**Spec:** `Docs/specs/2026-11-20-m5-three-classes-world-design.md` (decisions M5-1…M5-5 in `Docs/DECISIONS.md`).

## Global Constraints

- Game.Core never references UnityEngine, `System.Random` or wall-clock time; randomness only via `IRng`.
- C# 9 only; warnings are errors in the .NET projects. Test first for all Core code.
- Config keys are **append-only**: new keys at the end of a section or in new sections at the end of `balance_config.json` (the Sheets Config tab references rows); copy to `Assets/_Game/Data/` byte-identical; append the new scalar rows to the Sheets Config tab (es_ES, `;` separators).
- Triangle (D-017): advantage ×1.20 dealt / ×0.85 taken; disadvantage ×0.85 dealt / ×1.20 taken; Fighter > Caster, Archer > Brute, Mage > Marksman; no archetype = ×1.0 both ways.
- Elements (D-019, D-035): strong ×1.5, weak ×0.75; Fire>Nature>Water>Fire; Light and Dark beat each other and resist themselves (×0.75); Physical ×0.75 against Light and Dark targets; resistances clamped to −0.5…0.75.
- Region 1 is neutral (no archetype, no element, no trait); the ON-1 tuning and region 1 golden values must not change.
- Normal enemies never kill (R2-1c); nothing equips automatically (R2-18); one wearer per item (D-004); only the active hero earns XP, +50% catch-up (D-024).
- Switching (M5-3): instant during normal fights (fight dropped, no rewards), refused during any boss fight including the training boss. Unlock (M5-4): Archer and Mage on the account's first region 1 boss kill; survives prestige.
- Save schema becomes **2**; `Assets/Tests/SaveFixtures/v1.dfrs` must keep loading; add `v2.dfrs` and never regenerate either.
- New Unity files need `.meta` files committed by the owner after import.

## Review Focus

1. **Switching while the training boss or a region boss is up** → refused, active hero unchanged, no fight dropped. Test `Switch_DuringBossFight_Refused` (Task 4) — the session must know a boss fight is in progress even between `PrepareNextFight` and `Apply`.
2. **Equipping an item worn by another hero via "Equip upgrades"** → never happens; manual equip moves it only after confirmation. Tests `EquipUpgrades_NeverTakesOtherHeroesItems`, `Equip_ItemWornByOtherHero_RequiresConfirmation` (Task 4).
3. **Loading an old v1 save of a player already past region 1** → heroes unlocked, Fighter keeps everything, Archer/Mage at level 1 with starter weapons. Test `Migration_V1PastRegion1_UnlocksHeroes` (Task 5).
4. **Offline time after switching** → fought by the hero active at save time, XP only to that hero. Test `SimulateOffline_UsesActiveHero` (Task 4).
5. **A hit with several "adds element" lines against a resisting enemy** → each component modified separately, total never negative, Physical part unaffected by elemental resist. Test `MultiComponent_HitAgainstElemental_PerComponentMultipliers` (Task 1).

---

### Task 1: Elements and triangle in combat (Core)

**Files:**
- Create: `Assets/_Game/Core/Combat/Elements.cs`, `Assets/_Game/Core/Combat/Triangle.cs`
- Modify: `Assets/_Game/Core/Combat/CombatTypes.cs` (EnemySnapshot), `Assets/_Game/Core/Stats/StatInputs.cs` (CombatStats), `Assets/_Game/Core/Combat/CombatEngine.cs`, `Assets/_Game/Core/Config/BalanceConfig.cs`, `BalanceConfigLoader.cs`, both `balance_config.json`
- Test: `Assets/Tests/EditMode/Core/Combat/ElementTests.cs`, `TriangleTests.cs`

**Interfaces:**
- Produces:
  - `enum Element { Physical, Fire, Nature, Water, Light, Dark }`; `enum Archetype { None, Brute, Marksman, Caster }`
  - `sealed class ElementTable { ElementTable(BalanceConfig c); double Multiplier(Element attack, Element target); }`
  - `sealed class TriangleTable { TriangleTable(BalanceConfig c); double DealtBy(ClassId hero, Archetype enemy); double TakenBy(ClassId hero, Archetype enemy); }`
  - `EnemySnapshot`: `Element Element = Physical; Archetype Archetype = None; double[] Resist = new double[6]` (index = (int)Element)
  - `CombatStats`: `ClassId Class; double[] ElementShare = new double[6]` (share of each hit added as that element; index 0 unused); `double PctElemental`
  - `CombatEngine` per hit: components = Physical (HitDamage) + `HitDamage × ElementShare[e] × (1 + PctElemental)` for each e>0; each × `ElementTable.Multiplier(e, enemy.Element)` × `(1 − clamp(Resist[e], −0.5, 0.75))`; sum × `TriangleTable.DealtBy` × crit; enemy hits on the player × `TakenBy`. Fight events keep one total per hit; add `CombatEvent.Element` = largest component's element (for damage-number colour).
- Config (new sections appended): `elements: { strong_mult: 1.5, weak_mult: 0.75, beats: {fire: nature, nature: water, water: fire, light: dark, dark: light}, resists_self: [light, dark], physical_weak_vs: [light, dark], resist_min: -0.5, resist_max: 0.75 }`; `triangle: { advantage_dealt: 1.2, advantage_taken: 0.85, disadvantage_dealt: 0.85, disadvantage_taken: 1.2, class_beats: {fighter: caster, archer: brute, mage: marksman}, class_as: {fighter: brute, archer: marksman, mage: caster} }` (disadvantage = the enemy's archetype is the `class_as` of the class that beats yours).

- [ ] **Step 1: Write failing tests**: `Matrix_MatchesGddTable` (TestCase per cell of GDD §4.4, 36 cases); `Triangle_FighterVsCaster_Advantage` (1.2 / 0.85), `Triangle_FighterVsMarksman_Disadvantage` (0.85 / 1.2), `Triangle_NoArchetype_Neutral` (1 / 1), one case per class; `MultiComponent_HitAgainstElemental_PerComponentMultipliers` (HitDamage 100, ElementShare[Water]=0.5, PctElemental 0, enemy Element Water with Resist[Water]=0.75, no crit, hit chance forced to 1 by Accuracy 10⁹ → first PlayerHit amount = 100 + 50×0.75×0.25 = 109.375); `NeutralEnemy_DamageUnchanged` (same fight as before the change, same event amounts).
- [ ] **Step 2: Run** `dotnet test Core.Tests.Dotnet` → compile errors (types missing).
- [ ] **Step 3: Implement** the types, config loading (named errors like other sections), and the engine changes.
- [ ] **Step 4: Run** `dotnet test Core.Tests.Dotnet` → all pass, golden tests unchanged.
- [ ] **Step 5: Commit** `M5 task 1: elements and class triangle in combat`

### Task 2: Families and region data (Core)

**Files:**
- Modify: `Assets/_Game/Core/World/EnemyFactory.cs`, `WorldState.cs` (Encounter gains `string Family`, `Archetype Archetype`), `WorldSystem.cs`, `BalanceConfig.cs`, `BalanceConfigLoader.cs`, both `balance_config.json`
- Test: `Assets/Tests/EditMode/Core/World/FamilyTests.cs`

**Interfaces:**
- Consumes: `Element`, `Archetype`, `EnemySnapshot.Resist` (Task 1).
- Produces:
  - Config `families: { training: {element: null, evasion: 0, resist: {}, archetypes: [none,none,none,none,none], boss_archetype: none}, humanoids: {…, archetypes: [brute, marksman, caster, marksman, brute]}, beasts: {evasion: 125, archetypes: [brute, brute, marksman, marksman, caster]}, elementals: {element: water, resist: {water: 0.75}, archetypes: [brute, marksman, caster, brute, caster]}, undead: {element: dark, archetypes: [brute, marksman, caster, brute, caster]} }` with per-family `boss_archetype` (humanoids brute, beasts brute, elementals caster, undead caster); `regions: { subzone_families: [[training×5],[humanoids×5],[beasts×5],[elementals×5],[undead×5]], fallback_family: humanoids }` (M5-5: per-subzone list).
  - `Encounter.Family` and `Encounter.Archetype` filled by `WorldSystem.NextEncounter` (archetype = `archetypes[subzone−1]`, bosses `boss_archetype`, training boss `none`).
  - `EnemyFactory.Create` sets `Element`, `Resist`, `Archetype` and adds the family `evasion`.
  - The order of each family's archetypes matches its body order in `look_config.json` kits (Task 7 keeps them aligned; a test there checks counts).

- [ ] **Step 1: Write failing tests**: `Region1_IsNeutral` (all subzones and both bosses: Element Physical, Archetype None, Evasion unchanged); `Region3_Beasts_HaveEvasion` (evasion 125 more than region 1's formula value at equal level); `Region4_Elementals_WaterWithResist` (Element Water, Resist[Water] 0.75); `Region5_Undead_Dark`; `Region2_ArchetypesFollowSubzone` (subzones 1–5 → brute, marksman, caster, marksman, brute); `RegionBeyondList_UsesFallbackFamily` (region 9 → humanoids); `Loader_UnknownFamily_ThrowsNamedError` (`regions.subzone_families[1][0]`).
- [ ] **Step 2: Run** → fail. **Step 3: Implement.** **Step 4: Run** `dotnet test Core.Tests.Dotnet` → pass (region 1 golden rows unchanged).
- [ ] **Step 5: Commit** `M5 task 2: enemy families and region data`

### Task 3: Class weapons and elemental gear lines (Core)

**Files:**
- Modify: `Assets/_Game/Core/Items/ItemEnums.cs` (GearStat), `ItemStats.cs`, `LootSystem.cs` (starter helper), `Assets/_Game/Core/Characters/CharacterStatsBuilder.cs`, `Assets/_Game/Core/Stats/StatInputs.cs`, `StatCalculator.cs`, `Assets/_Game/Core/Config/ItemsConfig.cs` / loader, both `balance_config.json`
- Test: `Assets/Tests/EditMode/Core/Items/ElementalGearTests.cs`

**Interfaces:**
- Consumes: `CombatStats.ElementShare`, `PctElemental` (Task 1).
- Produces:
  - `GearStat` appended: `AddsFire, AddsNature, AddsWater, AddsLight, AddsDark, PctElemental` (stat keys `adds_fire` … `pct_elemental`).
  - `StatInputs.ElementShare double[6]`, `StatInputs.PctElemental`; `StatCalculator.Compute` copies them and sets `CombatStats.Class`.
  - `items.types.main_hand` appended: `bow` ("Bow": `weapon_damage` base 8.5 exponential + `attack_speed` 0.08 flat) and `staff` ("Staff": `weapon_damage` base 9.0 exponential + `pct_elemental` 0.10 flat).
  - `items.attributes` appended: `adds_fire`, `adds_nature`, `adds_water`, `adds_light`, `adds_dark` (min 0.08, max 0.16, kinds main_hand, off_hand, ring, amulet) and `pct_elemental` (min 0.10, max 0.20, same kinds, **class line: mage only**, rolled only when the item's ClassBonus is Mage).
  - `static string StarterWeaponType(ClassId c)` in `LootSystem` → sword / bow / staff (data: new key `items.starter_types: {fighter: sword, archer: bow, mage: staff}`).

- [ ] **Step 1: Write failing tests**: `Bow_HasAttackSpeedLine`, `Staff_HasElementalLine`; `AddsFire_FlowsToCombatStats` (item with adds_fire 0.12 equipped → `CombatStats.ElementShare[Fire] == 0.12`); `PctElemental_OnlyRollsOnMageClassItems` (10 000 rolls with ClassBonus Fighter never contain it; with Mage some do); `StarterWeapon_PerClass`; `Config_AttributesAppendedAtEnd` (the first 12 attribute ids unchanged and in order).
- [ ] **Step 2–4:** run → fail, implement, run → pass.
- [ ] **Step 5: Commit** `M5 task 3: bow, staff and elemental gear lines`

### Task 4: Three heroes, switching, unlock (Core)

**Files:**
- Modify: `Assets/_Game/Core/Session/GameState.cs`, `GameSession.cs`, `Assets/_Game/Core/Characters/ProgressionSystem.cs` (catch-up), `Assets/_Game/Core/Items/Inventory.cs` (Equip across heroes), `Assets/_Game/Core/Events/GameEvents.cs`
- Test: `Assets/Tests/EditMode/Core/Session/HeroesTests.cs`

**Interfaces:**
- Consumes: `CombatStats.Class` (Task 1), `StarterWeaponType` (Task 3).
- Produces:
  - `GameState.Heroes : List<CharacterState>` (index = (int)ClassId, always 3); `GameState.ActiveHero : int`; `GameState.Character => Heroes[ActiveHero]` (kept as the read path for existing code); `Characters => Heroes`; `AccountState.HeroesUnlocked : bool`; `GameState.NewRun()` creates all three (Fighter active).
  - `enum SwitchResult { Switched, AlreadyActive, Locked, BossFight }`; `GameSession.SwitchTo(ClassId c) : SwitchResult`; `GameSession.InBossFight : bool` (true from `PrepareNextFight` of a boss/training encounter until its `Apply`).
  - `event HeroesUnlocked` and `event HeroSwitched(ClassId)` on the bus.
  - On the first region-1 boss win: `HeroesUnlocked = true`, starter weapons created and equipped on Archer and Mage, event published.
  - XP: `GrantXp` applies `1 + catchup_xp_bonus` when the active hero's level < highest hero level.
  - `Inventory.Equip` with an item worn by another hero returns `EquipResult.NeedsConfirmation` unless `force: true`; `EquipUpgrades` skips items worn by any hero.
- [ ] **Step 1: Write failing tests**: `NewRun_ThreeHeroes_FighterActive_Locked`; `Switch_BeforeUnlock_Locked`; `Region1BossKill_UnlocksHeroes_WithStarterWeapons`; `Switch_DuringBossFight_Refused` (prepare a boss fight, switch → BossFight, active unchanged); `Switch_DuringNormalFight_DropsIt` (prepared normal fight; after switch the replayer-facing `PrepareNextFight` uses the new class; kills unchanged); `OnlyActiveHeroEarnsXp_WithCatchUp` (Archer level 1 vs Fighter 10 → 1.5× XP); `EquipUpgrades_NeverTakesOtherHeroesItems`; `Equip_ItemWornByOtherHero_RequiresConfirmation`; `SimulateOffline_UsesActiveHero`; `Unlock_IsAccountWide` (the flag lives on Account, which prestige keeps, not on World).
- [ ] **Step 2–4:** run → fail, implement, run → pass (all earlier tests, including save round trips, still green with the v1 mapper reading slot 0).
- [ ] **Step 5: Commit** `M5 task 4: three heroes, switching and the class unlock`

### Task 5: Save schema v2 (Core)

**Files:**
- Modify: `Assets/_Game/Core/Save/StateMapper.cs`, `SaveSerializer.cs` (`CurrentSchemaVersion = 2`, `Migrations.All` gains `V1ToV2`)
- Create: `Assets/_Game/Core/Save/Migrations/V1ToV2.cs`, `Assets/Tests/SaveFixtures/v2.dfrs`
- Test: `Assets/Tests/EditMode/Core/Save/MigrationV2Tests.cs`, extend `SaveFixtureTests.cs`

**Interfaces:**
- Payload v2: `heroes: [fighter, archer, mage]` (same character object shape as v1 `character`), `activeHero: 0..2`, `account.heroesUnlocked`.
- `V1ToV2 : IMigration { From = 1 }`: `character` → `heroes[0]`; Archer/Mage level-1 objects with empty equipment; `activeHero = 0`; `heroesUnlocked = world.highestBossDefeated >= 1`. Starter weapons for unlocked heroes are created by `GameSession`'s starter-kit step on load (it already rebuilds derived data).
- [ ] **Step 1: Write failing tests**: `V1Fixture_StillLoads_AsFighter` (level 31, region 3, items 40, pending 5 as before, Heroes.Count 3, ActiveHero 0, HeroesUnlocked true because region 3 means boss 1 beaten); `Migration_V1PastRegion1_UnlocksHeroes` (session on the migrated state: Archer and Mage each wear a Common starter weapon); `Migration_V1BeforeRegion1Boss_Locked`; `RoundTrip_V2_KeepsActiveHeroAndUnlock`; `V2Fixture_Loads` (generate `v2.dfrs` once with a temporary generator test, commit, delete the generator, then pin its values).
- [ ] **Step 2–4:** run → fail, implement, run → pass.
- [ ] **Step 5: Commit** `M5 task 5: save schema v2 with three heroes`

### Task 6: Balance simulation for three classes

**Files:**
- Modify: `Tools/BalanceSim/balance_sim.py`, `Tools/BalanceSim/seeds.py` (if profiles change), `Tools/BalanceSim.CSharp/Program.cs`, `Tools/BalanceSim/results/golden_values.json` (regenerated), `Docs/GAME_BALANCE.md`, `Docs/DECISIONS.md`
- Sheets: append the new scalar Config rows (es_ES)

**Interfaces:**
- Consumes: config sections from Tasks 1–3.
- Python: three heroes with their class intervals, templates and starter weapons; triangle, element matrix, family traits and elemental lines mirrored; each profile switches to the best hero per region by simulated clear speed after the unlock.
- [ ] **Step 1:** Extend the Python sim; run `python3 Tools/BalanceSim/balance_sim.py` and `seeds.py 7`. **Expected:** report shows per-class time to region 5.
- [ ] **Step 2:** Tune `families.*.evasion`, `adds_*` ranges and bow/staff bases until: the right hero clears regions 3–5 about 25–40% faster than the wrong one, and the three classes reach region 5 within ±25% of each other. Record final values in `GAME_BALANCE.md` (new §: classes, triangle, elements, families) and a balance entry in DECISIONS.
- [ ] **Step 3:** Regenerate golden values (`--golden …`), run `dotnet test Core.Tests.Dotnet` → pass; `dotnet run --project Tools/BalanceSim.CSharp` shows the three classes.
- [ ] **Step 4:** Append new rows to the Sheets Config tab (after row 161) via the exporter output.
- [ ] **Step 5: Commit** `M5 task 6: balance simulation for three classes`

### Task 7: Families' art and look data

**Files:**
- Create: `Tools/ArtBuild/make_creatures.py` (beasts, elementals, undead with rig parts `body`, `head`, `legs`, `tail`, `wings`, `core`, `weapon`), `Art/Source/<body>.svg` for the 15 new bodies and 3 bosses
- Modify: `Tools/ArtBuild/make_figures.py` (undead people: skeleton archer, wight, grave priest, ghoul can reuse the human anatomy with thin/tilted options), `Assets/_Game/Data/look_config.json` (kits `beasts`, `elementals`, `undead`; regions 3–5 kit and boss: Grisel the Fen Mother `fen_mother`, the Frozen Oath `frozen_oath`, Dorran Vael the Hollow Lord `hollow_lord`; motions for new part ids), `Assets/_Game/Resources/Art/*` (rebuilt)
- Test: extend `Assets/Tests/EditMode/Core/Look/LookAssetTests.cs`: `KitOrder_MatchesFamilyArchetypes` (each kit used by a region has as many bodies as that family's `archetypes`)

**Interfaces:**
- Body names (kit order = archetype order from Task 2): beasts `fen_wolf, bog_boar, marsh_stalker, spitting_toad, wisp_moth`; elementals `ice_brute, shard_thrower, frost_wisp, sleet_golem, rime_caller`; undead `ghoul, skeleton_archer, wight, bone_hound, grave_priest`.
- [ ] **Step 1:** Write the failing `KitOrder_MatchesFamilyArchetypes` and update kits/regions in look_config → asset tests fail (PNGs missing).
- [ ] **Step 2:** Write the generators, run `make_figures.py`, `make_creatures.py`, `build_art.py` → `dotnet test` passes; `build_art.py --check` passes.
- [ ] **Step 3:** Render a poses contact sheet (idle / attack / hit) and publish it for the owner.
- [ ] **Step 4: Commit** `M5 task 7: beasts, elementals and undead art`

### Task 8: Unity: hero bar, switching, unlock, element colours

**Files:**
- Create: `Assets/_Game/Presentation/HeroBarView.cs`
- Modify: `HudView.cs` (active hero's stats), `GameBootstrap.cs` (switch flow: `_replayer.Abandon()` + `_combat.Reset` on `Switched`; unlock toast), `CombatView.cs` (hero silhouette follows the active class via `EnemyLooks.ForClass`; damage numbers tinted by `CombatEvent.Element` using `LookConfig.ElementColors`), `InventoryView.cs` (shows which hero wears an item; confirmation dialog via `Ui.Modal` before moving it), `Resources/UI/IronEmber.uss` (hero buttons: active ember border, locked padlock look, disabled during boss)
- [ ] **Step 1:** Hero bar in the top panel: three buttons (class silhouette in class colour + level), states Active / Available / Locked ("Beat the region 1 boss to unlock") / BossFight (greyed); tap → `SwitchTo`.
- [ ] **Step 2:** Unlock moment: status line "The Archer and the Mage join you", buttons pulse once.
- [ ] **Step 3:** Syntax-check the Unity layer as in CD; owner checks in the playtest.
- [ ] **Step 4: Commit** `M5 task 8: hero bar, switching and element colours`

### Task 9: Docs, playtest guide, Jira

**Files:**
- Create: `Docs/M5_PLAYTEST.md`
- Modify: `Docs/GDD.md` (§3, §4.3–4.4 implemented notes, §8.2 families in regions 2–5), `Docs/SAVE_SYSTEM.md` (schema v2), `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/DEVELOPMENT_ROADMAP.md`, `CLAUDE.md` (status, commands for `make_creatures.py`), `Docs/CREATIVE_DIRECTION.md` (families' looks)
- Jira: tasks under DFIRPG-6 for each plan task; move DFIRPG-6 to In Progress at start, Done after merge.
- [ ] **Step 1:** Write the docs; **Step 2:** `dotnet test Core.Tests.Dotnet`, `build_art.py --check` → pass, CI green; **Step 3: Commit** `M5 task 9: docs and playtest guide`; ask the owner for the playtest, then PR and merge (after `.meta` files).
