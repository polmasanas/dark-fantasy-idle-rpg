# DEVELOPMENT_ROADMAP.md

> **Status:** v0.2. Jira project: `DFIRPG` (one epic per milestone). Each milestone is small, playable or testable, and gated by its acceptance criteria and the Definition of Done.

## MVP scope (v1.0)
3 classes · Common branch + 3 specialised branches per class · **about 25 regions** (10 hand-made + about 15 generated connectors; 125 subzones, R2-22) · 7 enemy families · 5 dungeons · 12 pets · 8 artifact types · crafting, enchanting, upgrading, salvaging · prestige · offline progression · saves (local, export/import, cloud backup) · achievements · cosmetics shop (gems) · Android only · English only (localisation-ready strings).
**Not in v1:** online features, trading, events, guilds, iOS.

## Milestones

| # | Name | Scope | Acceptance criteria |
|---|---|---|---|
| **M0** | Design foundation | 8 docs, Python balance simulation, Google Sheets balance view, decisions log | ✅ Decisions approved 2026-11-19. Remaining: owner sign-off of docs v0.2, merge to main |
| **M1** | Core skeleton *(done: pure C# core, tests, CI, Unity 6.3 project; awaiting merge)* | Unity project + asmdefs, Game.Core with RNG, BalanceConfig (imported from JSON), StatCalculator, CombatEngine (basic attacks, crits, accuracy, armour, counter), **C# simulation runner** (D-031), `dotnet test` CI | Golden tests: Python == C#; deterministic fight logs; CI green |
| **M2** | Playable fight loop *(done: merged; owner playtest approved)* | One region (5 subzones + boss), Fighter only, XP, levels, auto + manual points, gold, boss toggle, minimal combat view + HUD | Can play region 1 to its boss in the editor; animations on/off give identical results |
| **M3** | Loot & inventory *(done: merged; owner playtest approved, onboarding ON-1 added)* | Item generation, rarities, random attributes, 17 slots, inventory UI, equip / salvage / upgrade, shared inventory + one-owner rule, starter weapon | Statistical loot tests; D-004 tests; inventory UI usable at 360 × 640 dp |
| **M4** | Save & offline *(done: merged; owner playtest approved; device checks open in DFIRPG-14, M4-4)* | Save system (autosave, atomic write, backups, migrations framework), offline simulation + report, Android build on device | Kill-during-save test; offline exploit checklist; offline sim of 12 h in < 200 ms on test device |
| **M5** | Three classes & world | Archer + Mage, class triangle, elements, families, regions 1–5, character switching | Table-driven triangle and element tests; balance simulation updated with classes |
| **M6** | Skills | Common branches for 3 classes, active skill loadout, passives | Skill effects data-driven; perf ok |
| **M7** | Prestige & artifacts | Dust, artifacts, reset rules, Sigils + specialised branches | Prestige diff test; simulation re-run with real values |
| **M8** | Crafting & enchanting | Crafting, enchant sockets, gold sinks | Gold spend ratio 70–90% in the simulation (B-2) |
| **M9** | Pets & dungeons | 12 pets, 5 dungeons, tokens, chests, choose-outcome | Token pricing = ceil(1/p) tests; offline dungeons (D-039); token prices reachable within one run (D-023) |
| **M10** | Content to region 25 | Hand-made regions 6–10 with bosses, accessories, scrolls; **region generator** (biome × family × palette templates, editor tool); generated connector regions 11–25 reviewed and shipped as data | Simulation: casual players reach the end of content no earlier than about day 30; full playthrough to region 25; generator output deterministic |
| **M11** | Meta & polish | Achievements, onboarding/tutorial, settings, battery mode, cloud backup, export/import | Tutorial completion in internal test; cloud conflict UI |
| **M12** | Monetisation & release prep | Gem shop (cosmetics, Time Flasks), IAP, **binary save codec** (D-030), **final company name and Android package ID** (replace placeholder, D-042), store listing, closed test track | No paid power (checklist); package ID is no longer `com.dfirpg.placeholder`; closed test with ≥ 10 testers; crash-free ≥ 99.5% |

## Jira structure
- Project key `DFIRPG`. Epic per milestone (`M1 Core skeleton` …). Stories per system. Tasks for code / tests / docs.
- Labels: `design`, `balance`, `core`, `ui`, `art`, `qa`, `tech-debt`. Bug severity field matching TESTING_STRATEGY §6.
- Epics created in Jira on 2026-11-19: M0 = `DFIRPG-1`, M1 = `DFIRPG-2` … M12 = `DFIRPG-13` (key = milestone number + 1).

## Post-launch content cadence (planning assumption)
One content update every 6–8 weeks: +3–5 regions, 1 dungeon, 2–3 pets, new accessories, 1 artifact. Simulation target: an active player gains about 1 region per 2–3 days at month 2+, which fits this cadence.
