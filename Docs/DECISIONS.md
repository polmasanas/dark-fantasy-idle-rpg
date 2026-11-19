# DECISIONS.md — Decision Log

> Each entry: ID · status · decision · reason · date. Status: **APPROVED** (by the project owner), **PROPOSED** (awaiting approval), **SUPERSEDED**.
> Rule: no mechanic changes silently. Every change to an approved decision gets a new entry.

## Approved

| ID | Date | Decision | Reason |
|---|---|---|---|
| D-001 | 2026-11-19 | Gem **speedups are allowed**: "pay to progress faster, never further". Free and paying players can earn identical rewards. | Owner decision; resolves the spec conflict with "no pay-to-win". |
| D-002 | 2026-11-19 | Gems never buy equipment, stats or permanent power. | Master spec. |
| D-003 | 2026-11-19 | **Rarity scales base stats** (e.g. level 1 sword: Common 10, Uncommon 12). This is what separates Common from Uncommon, since both have 0 random attributes. | Owner decision. Multipliers in GAME_BALANCE §6.2. |
| D-004 | 2026-11-19 | An item (and a pet) can be **equipped by only one character at a time**. Equipping it elsewhere unequips it, after a confirmation prompt. | Owner decision. |
| D-005 | 2026-11-19 | Game repository on GitHub (`dark-fantasy-idle-rpg`), Git LFS for binary assets, UnityYAMLMerge for scenes and prefabs. | Workspace setup. |
| D-006 | 2026-11-19 | **Bosses can be fought offline**, under the same "Fight boss when available" toggle. | Owner decision. |

## Proposed (need approval)

| ID | Decision | Alternatives | Doc |
|---|---|---|---|
| D-010 | Unity 6 LTS + URP 2D Renderer | Built-in pipeline | TECH §1 |
| D-011 | UI Toolkit for menus, sprites for combat view | uGUI everywhere | TECH §1 |
| D-012 | Game.Core is pure C# (no UnityEngine), tested with `dotnet test` too | Unity-only tests | TECH §2 |
| D-013 | Event-driven deterministic combat, PCG32 RNG stored in save | Frame ticks, Unity RNG | TECH §6 |
| D-014 | 4 points per level: 3 automatic by class template, 1 manual (= 75/25) | 5 points per level, 4+1 (80/20) | GDD §5 |
| D-015 | Starter weapon each run | Guaranteed early weapon drop | GDD §6.1 |
| D-016 | 5 subzones per region, 2 levels per subzone, 10 levels per region | 3 or 10 subzones | GAME_BALANCE §2 |
| D-017 | Class triangle via enemy archetypes: ×1.20 dealt / ×0.85 taken with advantage | Flat class-vs-family table | GDD §4.3 |
| D-018 | HP fully restores between normal fights | Potions / regeneration economy | GDD §4.1 |
| D-019 | Elements: Strong ×1.5, Weak ×0.75 | ×2 / ×0.5 | GDD §4.4 |
| D-020 | Offline: 50% efficiency, 12 h cap (24 h with artifacts) | 25–100%, 8–24 h | GDD §13 |
| D-021 | Prestige unlocks at region 4 boss. Dust `10 · 1.5^(R−4)` | Polynomial dust | GAME_BALANCE §8 |
| D-022 | Artifacts multiplicative ×1.10/level, exponential cost | Additive | GAME_BALANCE §8 |
| D-023 | Prestige also keeps pets, skill-branch unlocks, tokens, gems, achievements; resets gold and materials | — | GDD §11 |
| D-024 | Only the active character earns XP | Inactive 10% | GDD §3 |
| D-025 | Item level requirement: character level ≥ item level − 5 | None | GDD §3 |
| D-026 | Crafting (chosen slot) as protection against bad luck | Pity timers | GDD §6.6 |
| D-027 | Enchant = one extra socket on Rare+ items, the main gold sink | Reroll random attributes | GDD §6.7 |
| D-028 | Skill branches unlocked by "Sigil" artifacts (dust) | Prestige count thresholds | GDD §7 |
| D-029 | Dungeons: no keys or stamina, tokens per dungeon, choose-outcome price = ceil(1/p) | Keys | GDD §10 |
| D-030 | Save format: gzip JSON envelope, string enums, atomic write + 3 backups | Binary | SAVE §2–3 |
| D-031 | Python simulation + shared JSON config as the balance reference; C# golden tests against it | Spreadsheet only | GAME_BALANCE |
