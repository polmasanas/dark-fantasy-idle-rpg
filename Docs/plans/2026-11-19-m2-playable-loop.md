# M2 Playable Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Press Play in Unity and watch a Fighter auto-fight through region 1's five subzones and its boss, gaining XP, levels, attribute points (manual + auto-allocate) and gold, with the boss toggle, escape timer and retreat rules working.

**Architecture:** All rules live in Game.Core as plain C#, tested with `dotnet test`: characters and progression, world state, enemy factory, a `GameSession` that resolves a fight (`PrepareNextFight`) and later applies it (`Apply`), and a synchronous event bus. A thin `Game.Presentation` assembly (Unity) replays each fight's event log in real time with placeholder UI Toolkit shapes and a HUD, then calls `Apply`.

**Tech Stack:** as M1 (C# 9, .NET 8 tests, NUnit, Newtonsoft); Unity 6000.3.29f1, UI Toolkit runtime.

**Spec:** `Docs/GDD.md` §3, §4.1, §4.6, §5, §8; `Docs/DECISIONS.md` D-014, D-016, D-024, D-033, D-034, R2-1, R2-1c, R2-2; owner-approved M2 design (chat, 2026-11-19): coloured-shape placeholders, UI Toolkit HUD, placeholder names, dev speed buttons, event-log replay.

## Global Constraints
- Game.Core stays free of UnityEngine; all numbers from `BalanceConfig`.
- C# 9; namespaces `DarkFantasy.Core.*`, `DarkFantasy.Presentation`.
- 4 points per level: 3 automatic from a 4-level class pattern (12 points), 1 manual. Fighter pattern per 4 levels: STR 6, VIT 4, DEF 1, COUNTER 1.
- Normal fights never kill; 5 escapes in a row → retreat one subzone, push again after 60 s game time. Boss loss → retry after 60 s game time (toggle ON).
- Kills to advance `K(r) = min(100, 10 + 5(r−1))` per subzone; boss needs K kills in subzone 5.
- No loot, saves or offline in M2. Fighter only (class data exists for all three).

## Review Focus
1. Large XP grants (offline-sized) level up many times in one call, each level paying its points → `AddXp_ManyLevelsAtOnce_PaysAllPoints`.
2. Auto-allocate with zero unspent points or an all-zero preset does nothing and never throws → `AutoAllocate_NoPoints_NoChange`.
3. Toggle switched OFF while the boss is available: farming continues in subzone 5 and no boss fight starts → `BossToggleOff_NeverFightsBoss`.
4. Retreat from subzone 1 (nowhere lower): stays in subzone 1 without errors → `Retreat_FromFirstSubzone_StaysPut`.
5. The Unity copy of the config drifts from `Tools/BalanceSim/balance_config.json` → `ConfigCopy_MatchesToolsConfig`.

---

### Task 1: Class data in config + Unity config copy
**Files:** Modify `Tools/BalanceSim/balance_config.json` (append section `classes`), `Assets/_Game/Core/Config/BalanceConfig.cs`, `BalanceConfigLoader.cs`; create `Assets/_Game/Data/balance_config.json` (copy); tests in `Assets/Tests/EditMode/Core/Config/`.
**Produces:** `BalanceConfig.Classes : Dictionary<ClassId, ClassConfig>`; `ClassConfig { AttributeId Primary; AttributeId[][] GrowthPattern /* 4 rows × 3 */; }`; `enum AttributeId { Str, Dex, Int, Vit, Def, CritChance, CritDamage, Luck, Counter }` (in `DarkFantasy.Core.Characters`); `BalanceConfig.AllocationPresets : Dictionary<string, Dictionary<AttributeId,double>>` with "primary", "balanced", "tank".
- Patterns: Fighter `[[Str,Str,Vit],[Str,Vit,Def],[Str,Str,Vit],[Str,Vit,Counter]]`; Archer `[[Dex,Dex,Vit],[Dex,Def,Vit],[Dex,Dex,Def],[Dex,Vit,CritChance]]`; Mage `[[Int,Int,Vit],[Int,CritDamage,Vit],[Int,Int,Def],[Int,CritDamage,Vit]]`.
- Presets: primary `{Primary:1}` (key `"primary"` resolves to the class primary); balanced `{Primary:0.5, Vit:0.25, CritChance:0.25}`; tank `{Vit:0.5, Def:0.25, Counter:0.25}`.
- [ ] Tests: `Classes_PatternsHave12PointsIn4Rows`, `Fighter_PatternTotals_6Str4Vit1Def1Counter`, `Presets_Loaded`, `ConfigCopy_MatchesToolsConfig` (byte-equal after normalising line endings).

### Task 2: Characters and progression
**Files:** `Assets/_Game/Core/Characters/CharacterState.cs`, `ProgressionSystem.cs`, `CharacterStatsBuilder.cs`; tests `.../Characters/`.
**Produces:** `sealed class CharacterState { ClassId Class; int Level=1; double Xp; Dictionary<AttributeId,int> AutoPoints, ManualPoints; int UnspentPoints; string Preset="primary"; double WeaponItemLevel=1; }`; `sealed class ProgressionSystem(BalanceConfig)` with `int AddXp(CharacterState c, double xp)` (returns levels gained), `bool SpendPoint(CharacterState c, AttributeId a)`, `int AutoAllocate(CharacterState c)`, `int Total(CharacterState c, AttributeId a)`; `sealed class CharacterStatsBuilder(BalanceConfig)` with `StatInputs Build(CharacterState c)` (weapon damage = WeaponDmgBase × ItemGrowth^(WeaponItemLevel−1); Common; no armour in M2).
- Auto-allocate: repeatedly give the point to the attribute with the largest (target share × (manual total + 1) − current manual points); ties by enum order.
- [ ] Tests: `AddXp_LevelsUpAtThreshold`, `AddXp_ManyLevelsAtOnce_PaysAllPoints` (level 1→50 in one call: auto 147, manual 49), `AutoPoints_FollowFighterPattern` (after level 5: STR 6, VIT 4, DEF 1, COUNTER 1), `SpendPoint_RequiresUnspent`, `AutoAllocate_Balanced_SplitsByShares` (8 points → Str 4, Vit 2, CritChance 2), `AutoAllocate_NoPoints_NoChange`, `Build_MapsAttributesToStatInputs`.

### Task 3: World state, enemies and encounter rules
**Files:** `Assets/_Game/Core/World/WorldState.cs`, `WorldSystem.cs`, `EnemyFactory.cs`; tests `.../World/`.
**Produces:** `sealed class WorldState { int Region=1; int UnlockedSubzone=1; int CurrentSubzone=1; int[] Kills (per subzone 1..5); bool FightBossWhenAvailable=true; int HighestBossDefeated; double BossRetryAt; double PushRetryAt; int ConsecutiveEscapes; }`; `struct Encounter { int Region, Subzone, Level; bool IsBoss; }`; `sealed class WorldSystem(BalanceConfig)` with `Encounter NextEncounter(WorldState w, double now)`, `WorldChange OnFightFinished(WorldState w, Encounter e, FightOutcome o, double now)`; `enum WorldChange { None, SubzoneUnlocked, BossDefeated, Retreated, BossFailed, Escaped }`; `sealed class EnemyFactory(BalanceConfig)` with `EnemySnapshot Create(Encounter e, IRng rng)`.
- Region N+1 after a boss win: subzone 1, kills reset (regions beyond hand-made content use formula-only placeholders).
- [ ] Tests: `Kills_UnlockNextSubzone`, `BossAvailable_AfterKInLastSubzone`, `BossToggleOff_NeverFightsBoss`, `BossLoss_RetriesAfter60s`, `BossWin_AdvancesRegion`, `FiveEscapes_Retreat_ThenPushAfter60s`, `Retreat_FromFirstSubzone_StaysPut`, `Win_ResetsEscapeCounter`, `EnemyFactory_UsesFormulasAndVariance`.

### Task 4: GameSession and event bus
**Files:** `Assets/_Game/Core/Events/EventBus.cs`, `GameEvents.cs`; `Assets/_Game/Core/Session/GameState.cs`, `GameSession.cs`, `PreparedFight.cs`; tests `.../Session/`.
**Produces:** `sealed class EventBus { void Subscribe<T>(Action<T>); void Unsubscribe<T>(Action<T>); void Publish<T>(T e); }`; events (classes): `FightResolved{PreparedFight}`, `EnemyKilled{Encounter, double Xp, double Gold}`, `LevelUp{int Level}`, `WorldChanged{WorldChange Change, WorldState World}`; `sealed class GameState { CharacterState Character; WorldState World; double Gold; double GameTime; }`; `sealed class PreparedFight { Encounter Encounter; EnemySnapshot Enemy; CombatStats Player; FightResult Result; }`; `sealed class GameSession(BalanceConfig, GameState, IRng, EventBus)` with `PreparedFight PrepareNextFight()`, `void Apply(PreparedFight f)` (rewards on Won; time += Duration + EnemyRespawnS; world rules), `int Advance(double seconds)` (headless: prepare+apply until time used; returns fights).
- [ ] Tests: `Apply_Won_GrantsXpAndGold`, `Apply_Escaped_GrantsNothing`, `Apply_AdvancesGameTime`, `Prepare_DoesNotChangeState`, `Advance_SameSeed_SameState`, `Advance_FreshFighter_ReachesRegion1Boss` (≤ 4 h game time), `Events_PublishedOnKillAndLevelUp`.

### Task 5: Unity presentation (owner verifies in the editor)
**Files:** `Assets/_Game/Presentation/Game.Presentation.asmdef`, `GameBootstrap.cs`, `FightReplayer.cs`, `CombatView.cs`, `HudView.cs`, `DebugPanel.cs`, `UiFactory.cs`; `Docs/M2_SCENE_SETUP.md`.
- Bootstrap (on a GameObject with `UIDocument`): loads `TextAsset` config, builds `GameSession` (seed from a serialized field, default random), builds views, runs `FightReplayer` in `Update` (game speed × `Time.deltaTime`), calls `Apply` when a replay ends.
- CombatView: Fighter box (blue), enemy box (red / dark red bigger for boss), HP bars, floating damage numbers (crit bigger, "Miss", "Counter!"), status line ("Escaped!", "Retreating…").
- HUD: region/subzone, kills "k / K", level + XP bar, gold, unspent points with + buttons per attribute, preset dropdown + Auto button, boss toggle.
- Debug panel (`UNITY_EDITOR || DEVELOPMENT_BUILD`): speed ×1/×5/×20, weapon item level +10, +5 levels.
- [ ] `dotnet build` cannot compile Unity code; review the code against Unity 6 UI Toolkit API; owner compiles and plays.

### Task 6: Docs, CI, finish
- [ ] Update TESTING_STRATEGY, TECHNICAL_ARCHITECTURE (Presentation assembly), roadmap, README; push branch; owner runs `Docs/M2_SCENE_SETUP.md`.
