# M4 Save & Offline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The game remembers everything between sessions and rewards time away: autosaves with backups and version migrations, a fight-by-fight offline simulation (50%, 12 h cap, clock-safe), a welcome-back report with Collect, the auto-allocate toggle and Custom preset, and a guide for the first Android device test.

**Architecture:** Game.Core gets a save model (state ↔ format-neutral JSON tree with string enums), a swappable codec with header, checksum and gzip, a migration chain, a file-system-abstracted save store (atomic write, 3 backups, fallback), an offline calculator with clock safety, and an offline simulation that holds loot until Collect. RNG state moves into `GameState` so a loaded game continues exactly. Unity adds a save service (persistentDataPath, autosave, pause/quit), the report panel and settings.

**Spec:** `Docs/SAVE_SYSTEM.md`, `Docs/DECISIONS.md` D-020, D-030, R2-18, R2-19, **M4-1…M4-4**, `Docs/GDD.md` §13.

## Global Constraints
- Core stays free of UnityEngine, wall-clock reads and `System.Random`: time comes in through an `IClock` (UTC now + monotonic uptime), files through an `IFileStore`.
- Save envelope: magic "DFRS", codec id, schema version, game version, saved UTC, playtime, SHA-256 checksum of the payload; payload gzip-compressed JSON. Enums as strings. A newer schema is refused, never downgraded.
- Atomic write: temp file → rename; rotate `bak1..bak3` before replacing; load falls back to the newest valid backup.
- Offline: credited = min(elapsed, cap 12 h) × 0.5; < 2 min → silent credit (no report); clock rollback → 0; a wall-clock jump larger than the device uptime since the save is clamped to the uptime (same boot); after a reboot the cap applies.
- Offline never equips; loot held in `GameState.PendingOfflineLoot` (saved) until Collect.

## Review Focus
1. Save → load → identical state, including RNG (the next fight is the same) → `RoundTrip_SameNextFight`.
2. Truncated or tampered save file → newest valid backup loads, nothing crashes → `CorruptMain_FallsBackToBackup`.
3. Clock moved back, or forward far beyond uptime → no or clamped credit → `ClockRollback_NoCredit`, `ClockJumpBeyondUptime_Clamped`.
4. Offline loot larger than free bag space at Collect → salvaged and reported, nothing lost → `Collect_BagFull_SalvagesRest`.
5. Absurd absences (30 days, `long.MaxValue` ticks) → capped, finite, fast → `HugeAbsence_Capped`.

---
### Task 1: Save model, codec, migrations (Core)
`Save/SaveModel` (GameState ↔ JObject, explicit field mapping, string enums), `Save/ISaveCodec` + `JsonGzipCodec`, `Save/SaveEnvelope`, `Save/Migrations`. RNG state in `GameState.Rng`; `GameSession` uses it. Tests: Review Focus 1, string enums in output, checksum mismatch detected, newer schema refused, migration chain applied in order (test-only migration).

### Task 2: Save store (Core)
`Save/IFileStore` (+ in-memory and directory implementations), `Save/SaveStore` (atomic write, backup rotation, load with fallback, report which file loaded). Tests: Review Focus 2, rotation keeps 3, first launch (no files) → new game.

### Task 3: Offline calculator and simulation (Core)
`Offline/IClock`, `Offline/OfflineCalculator` (credit rules above), `GameSession.SimulateOffline(seconds)` → `OfflineReport` (kills, bosses won/failed, levels, gold, materials, held items, auto-salvaged), events off for speed; `Collect()`. Tests: Review Focus 3–5, report numbers match state changes, no equipping offline, boss toggle respected, performance smoke (12 h credited in < 2 s on CI).

### Task 4: Auto-allocate toggle and Custom preset (Core)
`CharacterState.AutoAllocateOnLevelUp`, `CharacterState.CustomWeights`; preset "custom"; applied after level-ups online and offline; saved. Tests: weights respected, default custom = primary, toggle off keeps points.

### Task 5: Unity save service, report and settings
`Presentation/SaveService` (persistentDataPath, autosave 30 s, OnApplicationPause/Quit, after critical actions), welcome-back panel with Collect, HUD toggle + Custom preset editor, dev panel: "Simulate 8 h away" (shows simulation ms), "Delete save".

### Task 6: Docs and device guide
`Docs/ANDROID_DEVICE_TEST.md`, SAVE_SYSTEM/GDD/TESTING updates, `CLAUDE.md`, Jira task "M4 device checks" under DFIRPG-5.
