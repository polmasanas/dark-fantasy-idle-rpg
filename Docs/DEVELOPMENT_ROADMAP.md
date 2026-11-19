# DEVELOPMENT_ROADMAP.md

> **Status:** v0.1 DRAFT. Each milestone is small, playable or testable, and gated by its acceptance criteria and the Definition of Done. Milestones become Jira epics once approved.

## MVP scope (v1.0)
3 classes · Common branch + 3 specialised branches per class · **10 regions** (50 subzones) · 7 enemy families · 5 dungeons · 12 pets · 8 artifact types · crafting, enchanting, upgrading, salvaging · prestige · offline progression · saves (local, export/import, cloud backup) · achievements · cosmetics shop (gems) · Android only · English only (localisation-ready strings).
**Not in v1:** online features, trading, events, guilds, iOS.

## Milestones

| # | Name | Scope | Acceptance criteria |
|---|---|---|---|
| **M0** | Design foundation *(this branch)* | 8 docs, balance simulation, decisions log | Owner reviews; open questions Q-1…Q-7 answered; D-010…D-031 approved or changed |
| **M1** | Core skeleton | Unity project + asmdefs, Game.Core with RNG, BalanceConfig, StatCalculator, CombatEngine (basic attacks, crits, accuracy, armour), `dotnet test` CI | Golden tests match Python; deterministic fight logs; CI green |
| **M2** | Playable fight loop | One region (5 subzones + boss), Fighter only, XP, levels, auto + manual points, gold, boss toggle, minimal combat view + HUD | Can play region 1 to its boss in the editor; animations on/off give identical results |
| **M3** | Loot & inventory | Item generation, rarities, random attributes, 17 slots, inventory UI, equip / salvage / upgrade, shared inventory + one-owner rule, starter weapon | Statistical loot tests; D-004 tests; inventory UI usable at 360 × 640 dp |
| **M4** | Save & offline | Save system (autosave, atomic write, backups, migrations framework), offline simulation + report, Android build on device | Kill-during-save test; offline exploit checklist; offline sim of 12 h in < 200 ms on test device |
| **M5** | Three classes & world | Archer + Mage, class triangle, elements, families, regions 1–5, character switching | Table-driven triangle and element tests; balance simulation updated with classes |
| **M6** | Skills | Common branches for 3 classes, active skill loadout, passives | Skill effects data-driven; perf ok |
| **M7** | Prestige & artifacts | Dust, artifacts, reset rules, Sigils + specialised branches | Prestige diff test; simulation re-run with real values |
| **M8** | Crafting & enchanting | Crafting, enchant sockets, gold sinks | Gold spend ratio 70–90% in the simulation (B-2) |
| **M9** | Pets & dungeons | 12 pets, 5 dungeons, tokens, chests, choose-outcome | Token pricing = ceil(1/p) tests; offline dungeon decision implemented (Q-3) |
| **M10** | Content to region 10 | Regions 6–10, bosses, accessories, scrolls | Simulation: target pacing reached; full playthrough to region 10 |
| **M11** | Meta & polish | Achievements, onboarding/tutorial, settings, battery mode, cloud backup, export/import | Tutorial completion in internal test; cloud conflict UI |
| **M12** | Monetisation & release prep | Gem shop (cosmetics, Time Flasks), IAP, store listing, closed test track | No paid power (checklist); closed test with ≥ 10 testers; crash-free ≥ 99.5% |

## Jira structure (proposed)
- Project key `DFI`. Epic per milestone (`M1 Core skeleton` …). Stories per system. Tasks for code / tests / docs.
- Labels: `design`, `balance`, `core`, `ui`, `art`, `qa`, `tech-debt`. Bug severity field matching TESTING_STRATEGY §6.
- I can create these via the Atlassian connection once you tell me which Jira site/project to use.

## Post-launch content cadence (planning assumption)
One content update every 6–8 weeks: +3–5 regions, 1 dungeon, 2–3 pets, new accessories, 1 artifact. Simulation target: an active player gains about 1 region per 2–3 days at month 2+, which fits this cadence.
