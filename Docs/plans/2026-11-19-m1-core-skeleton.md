# M1 Core Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A pure C# game core (RNG, balance config, formulas, stats, event-driven combat) inside the Unity folder layout, tested with `dotnet test`, checked against the Python reference by golden tests, with a C# simulation runner and CI.

**Architecture:** `Assets/_Game/Core` holds plain C# with an asmdef that has `noEngineReferences: true`. Tests live in `Assets/Tests/EditMode/Core` (Unity runs them later through its own asmdef). A .NET test project in `Core.Tests.Dotnet/` compiles both folders directly so they run here and in CI. Python exports golden values; C# tests load them.

**Tech Stack:** C# 9 (Unity 6 compatible, enforced with `<LangVersion>9.0</LangVersion>`), .NET 8 SDK for tests, NUnit 3, Newtonsoft.Json 13 (Unity package `com.unity.nuget.newtonsoft-json` later), Python 3 reference sim, GitHub Actions.

**Spec:** `Docs/GDD.md`, `Docs/GAME_BALANCE.md`, `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/DECISIONS.md` (D-012, D-013, D-031, D-033, R2-1b, R2-1c, R2-21), `Tools/BalanceSim/balance_config.json`.

## Global Constraints
- Core never references `UnityEngine`, `System.Random` or `DateTime.Now`; all randomness goes through `IRng` (PCG32, state serialisable as two `ulong`).
- C# 9 only (no file-scoped namespaces, no record structs, no global usings). Namespace root `DarkFantasy.Core`.
- Every balance number comes from `BalanceConfig` (loaded from `balance_config.json`); no literals in formulas except true constants.
- Golden tests: C# formula outputs equal Python within 1e-9 relative.
- Same seed + same inputs ⇒ identical combat event log.

## Review Focus
1. Huge values (region 100, ~1e43 HP): formulas stay finite, no overflow to `Infinity`/`NaN` → test `Formulas_StayFinite_AtLevel5000`.
2. Zero or missing attack speed / stats: interval never below 0.35 s floor, never divides by zero → test `AttackInterval_RespectsFloor_AndHandlesZeroSpeed`.
3. Crit chance from huge crit points: clamps at exactly 1.0 (R2-21) → test `CritChance_ClampsAtOne`.
4. Boss fight where both time limit and death would happen in the same instant: deterministic rule (death checked before limit) → test `Boss_DeathAndTimeoutSameInstant_IsLoss`.
5. Config JSON missing a key: loader throws a clear `BalanceConfigException` naming the key → test `Loader_MissingKey_ThrowsNamedError`.

---

### Task 1: Project scaffold, RNG and dotnet test harness
**Files:** Create `Assets/_Game/Core/Game.Core.asmdef`, `Assets/_Game/Core/Random/IRng.cs`, `Assets/_Game/Core/Random/Pcg32.cs`, `Assets/Tests/EditMode/Core/Game.Core.Tests.asmdef`, `Assets/Tests/EditMode/Core/Random/Pcg32Tests.cs`, `Core.Tests.Dotnet/Core.Tests.Dotnet.csproj`, `Directory.Build.props` (none: keep in csproj).
**Interfaces — Produces:** `interface IRng { uint NextUInt(); double NextDouble(); /* [0,1) */ double Range(double min, double max); bool Chance(double p); }`; `sealed class Pcg32 : IRng { Pcg32(ulong seed, ulong stream = 54); ulong State {get;} ulong Increment {get;} static Pcg32 FromState(ulong state, ulong inc); }`.
- [ ] Tests: `SameSeed_SameSequence`, `FromState_ResumesSequence`, `NextDouble_InUnitInterval` (100k samples), `Chance_Frequency_WithinTolerance` (p=0.25, 200k samples, ±0.005), `KnownVector` (seed 42 stream 54 → first `NextUInt` equals PCG32 reference value computed in the test from the published algorithm).
- [ ] Implement PCG32 XSH-RR (multiplier 6364136223846793005).
- [ ] `dotnet test Core.Tests.Dotnet` passes. Commit.

### Task 2: BalanceConfig + loader
**Files:** `Assets/_Game/Core/Config/BalanceConfig.cs`, `Assets/_Game/Core/Config/BalanceConfigLoader.cs`, test `Assets/Tests/EditMode/Core/Config/BalanceConfigLoaderTests.cs`.
**Produces:** `sealed class BalanceConfig` with nested classes `World, Enemy, Player, Xp, Gear, Economy, Mastery, Prestige, Combat` mirroring JSON names in PascalCase; `static BalanceConfig BalanceConfigLoader.FromJson(string json)`; `class BalanceConfigException : Exception`.
- [ ] Tests: `Loader_ReadsRepoConfig` (loads `Tools/BalanceSim/balance_config.json`, asserts `Enemy.HpGrowth == 1.10`, `Combat.BaseIntervalFighter == 3.0`, `Player.CritChanceCap == 1.0`, artifacts dictionary has `might` cost growth 1.28), `Loader_MissingKey_ThrowsNamedError`.
- [ ] Implement with Newtonsoft `JObject`, required-key helper that throws with the dotted key path. Commit.

### Task 3: Formulas + golden export
**Files:** `Assets/_Game/Core/Formulas/Formulas.cs`, `Tools/BalanceSim/balance_sim.py` (add `--golden PATH`), `Tools/BalanceSim/results/golden_values.json`, test `Assets/Tests/EditMode/Core/Formulas/FormulaGoldenTests.cs`.
**Produces:** `sealed class Formulas(BalanceConfig c)` with `int KillsToAdvance(int region)`, `int SubzoneLevel(int region,int sub)`, `int BossLevel(int region)`, `double EnemyHp(int lvl,bool boss)`, `double EnemyAtk(int lvl,bool boss)`, `double XpToNext(int level)`, `double EnemyXp(int enemyLvl,int playerLvl,bool boss)`, `double ArmourK(int enemyLvl)`, `double GoldDrop(int lvl,bool boss)`, `double UpgradeCost(int ilvl,int upgrade)`, `long DustForRun(int highestBossRegion,int prestigesDone)` (×3 for first 3, D-037), `double ArtifactCost(string id,int level)`, `double ArtifactMult(string id,int level)` (haste additive).
- [ ] Python `--golden` writes a JSON list of `{fn, args, value}` for levels 1,2,10,50,100,250,500 and regions 1..30, every function above.
- [ ] Tests: `Golden_AllMatchPython` (1e-9 relative), `Formulas_StayFinite_AtLevel5000`.
- [ ] Commit.

### Task 4: Stats
**Files:** `Assets/_Game/Core/Stats/StatBlock.cs`, `Assets/_Game/Core/Stats/StatCalculator.cs`, test `.../Stats/StatCalculatorTests.cs`.
**Produces:** `sealed class StatInputs { ClassId Class; int Level; double Primary, Vit, Def, CritPoints, CounterPoints; double WeaponDamage, GearHp, GearArmour, PctDamage, PctHp, AttackSpeedPct; int MightLevel, BulwarkLevel, HasteLevel; }`, `enum ClassId { Fighter, Archer, Mage }`, `sealed class CombatStats { double HitDamage, MaxHp, Armour, AttackInterval, HitChance, CritChance, CritDamage, CounterChance; }`, `CombatStats StatCalculator.Compute(StatInputs i)`.
- [ ] Tests: `AttackInterval_ClassBases` (3.0/2.2/3.4 at 0% speed), `AttackInterval_RespectsFloor_AndHandlesZeroSpeed` (+10000% → 0.35), `Haste_Adds5PercentPerLevel`, `HitDamage_ScaledByClassInterval` (Fighter ×3.0/1.6), `CritChance_ClampsAtOne`, `Counter_ClampsAt40Percent`, `Hp_UsesVitAndBulwark`.
- [ ] Commit.

### Task 5: Event-driven combat engine
**Files:** `Assets/_Game/Core/Combat/Combatant.cs`, `CombatEvent.cs`, `FightResult.cs`, `CombatEngine.cs`, test `.../Combat/CombatEngineTests.cs`.
**Produces:** `sealed class EnemySnapshot { double Hp, Attack, AttackInterval, Armour, Evasion; bool IsBoss; }`; `enum FightOutcome { Won, Escaped, Died, TimedOut }`; `enum CombatEventType { PlayerHit, PlayerMiss, PlayerCrit, Counter, EnemyHit, EnemyDied, PlayerDied, Escaped, TimedOut }`; `readonly struct CombatEvent { double Time; CombatEventType Type; double Amount; }`; `sealed class FightResult { FightOutcome Outcome; double Duration; IReadOnlyList<CombatEvent> Events; }`; `FightResult CombatEngine.Resolve(CombatStats player, EnemySnapshot enemy, IRng rng, bool recordEvents)`.
- Rules: priority queue on time; ties: player action before enemy. Normal fights: player HP floor 1 (R2-1c), escape at `Combat.EscapeTimerS`. Boss: death (HP ≤ 0) or `Enemy.BossTimeLimitS`. Counter: on each enemy hit, `Chance(CounterChance)` → immediate extra hit at `Player.CounterDamage` × hit (can crit, cannot counter). Armour damage reduction applies to all damage the player takes (it only changes outcomes in boss fights, since normal fights cannot kill).
- [ ] Tests: `SameSeed_IdenticalLog`, `Normal_NeverDies` (enemy 1e9 attack → outcome ≠ Died), `Normal_EscapesAfterTimer` (tiny damage → Escaped at 30 s), `Boss_TimesOutAt60`, `Boss_DiesWhenOutdamaged`, `Boss_DeathAndTimeoutSameInstant_IsLoss`, `Counter_TriggersOnlyOnEnemyHits`, `CritRate_MatchesChance` (10k fights statistical), `MeanTtk_MatchesPythonModelWithin20Percent`.
- [ ] Commit.

### Task 6: C# simulation runner + CI + docs
**Files:** `Tools/BalanceSim.CSharp/BalanceSim.CSharp.csproj`, `Tools/BalanceSim.CSharp/Program.cs`, `.github/workflows/core-tests.yml`, `Docs/TESTING_STRATEGY.md`, `Docs/TECHNICAL_ARCHITECTURE.md`, `README.md`.
- [ ] Runner (M1 scope): for regions 1..30, a reference build (Common weapon at boss level, level = boss level, no artifacts) fights 2 000 normal enemies and 500 bosses with the real `CombatEngine`; prints a Markdown table (mean TTK, escape %, boss win %) to `Tools/BalanceSim/results/CSHARP_COMBAT.md`. Full player-profile simulation is added as loot/prestige systems arrive (M3, M7).
- [ ] CI: on push/PR → setup .NET 8, `dotnet test Core.Tests.Dotnet`, `python3 Tools/BalanceSim/balance_sim.py --days 3`, golden file freshness check (`--golden` to temp, `diff`).
- [ ] Docs updated; Commit; push branch `m1/core-skeleton`.
