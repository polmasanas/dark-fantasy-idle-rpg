# CLAUDE.md: project memory for every Claude Code session

Read this first. It is loaded automatically in every session (cloud or on the owner's PC), so all sessions share the same context. Keep it short and current: update it when a milestone, rule or workflow changes.

## The project
Dark fantasy, flat-cartoon 2D **idle RPG for Android** (portrait), Unity + C#, built by one developer (the owner) with Claude acting as the whole studio (design, balance, code, QA, docs). Working title only; the final name, company and Android package ID are placeholders until M12 (D-042).

**Source of truth, in this order:** the owner's decisions in `Docs/DECISIONS.md` → `Docs/GDD.md` → the other docs. Never contradict an approved decision silently.

## Status
| Milestone | State |
|---|---|
| M0 Design foundation | Done (merged, PR #1) |
| M1 Core skeleton | Done (merged, PR #2) |
| M2 Playable loop | Done (merged, PR #3). Owner playtest changed R2-2: a failed boss returns after a new full wave of kills |
| **M3 Loot & inventory** | **Next** (Jira DFIRPG-4). Removes the dev-only placeholder armour |
| M3–M12 | Planned in `Docs/DEVELOPMENT_ROADMAP.md`; one Jira epic each (project key `DFIRPG`, epic key = milestone number + 1, e.g. M3 = DFIRPG-4) |

## Where things live
| Path | What |
|---|---|
| `Docs/` | GDD, TECHNICAL_ARCHITECTURE, GAME_BALANCE, DATA_DICTIONARY, SAVE_SYSTEM, DEVELOPMENT_ROADMAP, **DECISIONS** (D-*, R2-*), TESTING_STRATEGY, UNITY_SETUP, M2_SCENE_SETUP, `plans/` (one implementation plan per milestone) |
| `Assets/_Game/Core/` | **Game.Core**: all game rules in pure C# (no UnityEngine): RNG, config, formulas, stats, combat, characters, world, session, events |
| `Assets/_Game/Presentation/` | **Game.Presentation**: thin Unity layer (bootstrap, views, HUD, dev panel). Draws only; no rules |
| `Assets/_Game/Editor/` | **Game.Editor**: editor tools (e.g. `DFIRPG → Set Up M2 Scene`) |
| `Assets/_Game/Data/balance_config.json` | Copy Unity loads; must stay byte-identical to `Tools/BalanceSim/balance_config.json` (a test enforces it) |
| `Assets/Tests/EditMode/Core/` | Core tests (run in Unity's Test Runner and with `dotnet test`) |
| `Core.Tests.Dotnet/` | .NET project that compiles Core + tests outside Unity |
| `Tools/BalanceSim/` | Python reference simulation, **`balance_config.json` (authority for all numbers)**, golden values, Google Sheets exporter |
| `Tools/BalanceSim.CSharp/` | C# combat runner on the real engine |
| `.github/workflows/core-tests.yml` | CI: tests, golden freshness, simulation smoke runs |

## Commands
```bash
dotnet test Core.Tests.Dotnet                      # all core tests (needs .NET 8 SDK)
python3 Tools/BalanceSim/balance_sim.py             # 30-day balance simulation -> results/
python3 Tools/BalanceSim/seeds.py 7                 # multi-seed check
python3 Tools/BalanceSim/balance_sim.py --golden Tools/BalanceSim/results/golden_values.json   # after an approved formula/config change
dotnet run --project Tools/BalanceSim.CSharp        # C# combat table
```

## Hard rules
- **Owner decides design.** Missing or conflicting rule → explain the problem, give 2–3 options with a recommendation, wait for the owner. The owner likes **one decision at a time**, each with a short options table and a recommended choice.
- **Everything configurable and data-driven** (D-032): numbers in `balance_config.json`, content as data; no magic numbers in code.
- **Game.Core never references UnityEngine**, `System.Random` or wall-clock time; randomness only via `IRng` (PCG32, deterministic).
- **C# 9 only** (Unity 6 compiler): no file-scoped namespaces, record structs, global usings. Warnings are errors in the .NET projects.
- **Test first** for all Core code (watch it fail, then pass). Unity-layer code is verified by the owner in the editor or via the Unity connector.
- **Balance changes:** edit `Tools/BalanceSim/balance_config.json` → copy to `Assets/_Game/Data/` → re-run sims → regenerate golden values → update `Docs/GAME_BALANCE.md` → log in `Docs/DECISIONS.md`.
- **Config keys are append-only** at the end of a section or in a new section: the Google Sheets workbook ("Dark Fantasy Idle RPG – Balance", owner's Drive) references Config cells by row. Its locale is **es_ES**: formulas use `;` as argument separator.
- **Never** let the game equip items automatically (R2-18); normal enemies never kill the player (R2-1c); gems never buy power (D-002).
- **Git:** work on a milestone branch, never commit to `main` directly; merge through a pull request when the owner approves. Unity's `*.csproj` are ignored except `Core.Tests.Dotnet/` and `Tools/BalanceSim.CSharp/`. Don't commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- **Milestone done** = feature works, integrated, tests pass, edge cases checked, docs updated, no critical regressions, owner approved.

## Owner's environment (Windows)
- Repo at `D:\Dev\dark-fantasy-idle-rpg`; Git Bash for commands.
- **Unity 6000.3.29f1** (pinned, D-040), project from the Universal 2D template (URP 2D, Input System), portrait only.
- Windows **Smart App Control is off** (it blocked Unity's compiler). If a Unity tool is "blocked by an Application Control policy", that is the cause.
- Unity Hub installs from an `.msix`; use Windows App Installer or `Add-AppxPackage`, not third-party MSIX tools.

## Temporary, dev-only things (remove when their milestone lands)
- `CharacterState.PlaceholderArmourItemLevel` and the dev panel's **Gear +10**: stand-ins for loot until M3 (without armour the region 1 boss is unbeatable, by design).
- Placeholder visuals (coloured boxes) and names ("Region N"): replaced by Creative Direction / art (suggested before M5).
- Placeholder company and package ID `com.dfirpg.placeholder`: replace in M12 before any Play Store upload.
