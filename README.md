# Dark Fantasy Idle RPG (working title)

Mobile idle RPG for Android, built in Unity (C#). Dark fantasy, flat cartoon 2D, side-view automatic combat.

## Repository layout
| Path | Contents |
|---|---|
| `Docs/` | GDD, technical architecture, balance, data dictionary, save system, roadmap, decisions, testing strategy |
| `Tools/BalanceSim/` | Python balance simulation (reference formulas + player-behaviour simulations) |
| `Assets/_Game/Core/` | Game logic in pure C# (no Unity dependency): RNG, balance config, formulas, stats, combat engine |
| `Assets/Tests/EditMode/Core/` | Core tests (run in Unity's Test Runner and with `dotnet test`) |
| `Core.Tests.Dotnet/` | .NET test project that runs the core tests outside Unity |
| `Tools/BalanceSim.CSharp/` | C# combat simulation runner using the real core |
| `.github/workflows/` | CI: core tests, golden-value freshness, simulation smoke runs |

## Status
M0 (design) and **M1 (core skeleton)** are complete. Unity version: **6000.3.29f1**. See `Docs/DEVELOPMENT_ROADMAP.md`.

## Setup
Git LFS is required: `git lfs install` before cloning or committing art.
Opening the project in Unity for the first time: follow `Docs/UNITY_SETUP.md`.

## Running the tests without Unity
```bash
dotnet test Core.Tests.Dotnet
```
Requires the .NET 8 SDK. The same tests run inside Unity's Test Runner (EditMode).
