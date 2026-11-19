# TESTING_STRATEGY.md

> **Status:** v0.1 DRAFT.

## 1. Principles
- Most logic lives in **Game.Core (pure C#)**, so most tests are fast unit tests that run in Unity **and** with `dotnet test` (CI, cloud sessions).
- Determinism: every random system takes an `IRng`. Tests use fixed seeds, and statistical tests use large samples with explicit tolerances.
- **Definition of Done** (master prompt): the feature works, integrates, tests pass, edge cases are checked, docs are updated, and no critical regressions remain.

## 2. Test pyramid

| Layer | Tooling | Runs | Examples |
|---|---|---|---|
| Unit (Core) | NUnit, `dotnet test` + Unity EditMode | every push (CI) | damage pipeline, XP curve, dust formula, item ownership rule |
| Golden / balance | NUnit vs `Tools/BalanceSim` output | every push | C# formulas == Python formulas for levels 1…1000 (1e-9 relative) |
| Statistical | NUnit, 1M samples | nightly / pre-milestone | rarity distribution (chi-square), 0.01% drops (binomial CI), crit rates |
| Property / fuzz | NUnit with random command sequences | pre-milestone | no negative currency, no duplicate equipped item, no NaN or Inf in stats |
| Integration | Unity EditMode | every push | definitions convert and validate, save → load round-trip |
| PlayMode smoke | Unity PlayMode | pre-milestone | boot → fight → kill → loot → save |
| Device / perf | manual + profiler | each milestone from M4 | FPS, memory, battery mode, offline sim time on a low-end device |

## 3. Critical areas & mandatory tests

| Area | Must prove |
|---|---|
| Combat | Each multiplier applies once, in the documented order; class triangle and element matrix match the GDD tables exactly (table-driven tests over all 36 element pairs and 9 class pairs); crit and accuracy rates within tolerance; same seed ⇒ identical event log; animations on or off ⇒ identical results |
| Progression | 3 + 1 points per level; level-ups over a large XP grant are handled in one go; no level skipped |
| Loot | Rarity distribution; random attribute count per rarity; item level = enemy level; 0.01% drops reachable and not more frequent than configured |
| Inventory | One owner per item (D-004); locked items can't be salvaged; full inventory never loses items silently |
| Economy | Spending more than the balance fails; every currency change carries a reason; no negative balances |
| Prestige | Run state fully replaced; account state byte-identical except dust, prestige count and stats; dust paid once; cannot prestige below the unlock |
| Offline | Credited time = min(elapsed, cap) × efficiency; clock rollback ⇒ 0; result equals running the same fights online (same seed); the reward ceiling holds for any input time (including `long.MaxValue`) |
| Save | Round-trip equality; every historical fixture migrates; corrupted file ⇒ backup used; newer schema refused |
| Numbers | Formulas stay finite up to region 700; formatter handles 1e3…1e300 |

## 4. Exploit checklist (QA, every milestone)
Clock forward/back, app kill mid-save, rapid character switching during a boss fight, equipping the same item from two characters via fast taps, prestige double-tap, import of edited saves, offline timer with the device asleep for 30 days, inventory overflow during offline, negative/huge numbers in an imported save.

## 5. Golden tests vs balance simulation
In M1, `Tools/BalanceSim` gains a `--golden` export to `golden_values.json` (formula outputs for sample inputs). `Game.Core.Tests/GoldenFormulaTests` load it and compare. When a balance change is approved, the config and golden file are regenerated in the same commit.

## 6. Bug reports
Template: **Title · Severity · Build/commit · Steps to reproduce · Expected · Actual · Save file / seed · Frequency**.

| Severity | Definition | Milestone gate |
|---|---|---|
| Critical | Crash, save loss or corruption, progression-blocking, economy exploit | Blocks |
| High | Wrong rewards or calculation, a feature unusable | Blocks |
| Medium | Workaround exists, UX problems | Allowed, ticketed |
| Low | Cosmetic | Allowed |

## 7. CI
GitHub Actions: `dotnet test Core.Tests.Dotnet` + `python3 Tools/BalanceSim/balance_sim.py --days 3` (smoke) on every push. Unity tests and Android build via GameCI from M4 (needs a Unity licence secret).
