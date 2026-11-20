# M3 Loot & Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Real loot replaces the dev-only placeholder armour: enemies drop items for 11 slots with rarities, types and random attributes; the player equips, compares, upgrades with gold, locks and salvages them in an inventory screen, and beats bosses with real gear.

**Architecture:** Item rules live in Game.Core (tested): config-driven item types and attribute pool, `LootSystem` (drops), `InventorySystem` (equip/lock/salvage/expand/auto-salvage), `UpgradeSystem` (gold), `GearStats` (equipped items → `StatInputs`), `PowerScore` (comparison). `GameSession` rolls drops on kills. The Python simulation adopts the same item model so balance stays checked. A Unity `InventoryView` (UI Toolkit) shows it.

**Tech Stack:** as M2.

**Spec:** `Docs/DECISIONS.md` D-003, D-004, D-015, D-025, D-027 (later), R2-3b, R2-6, R2-18, R2-20, R2-21 (inventory), **M3-1…M3-5**; `Docs/GAME_BALANCE.md` §6; `Docs/GDD.md` §6.

## Global Constraints
- 11 active slots: MainHand, OffHand, Helmet, Chest, Legs, Boots, Armguards, Gloves, Ring1, Ring2, Amulet. Accessory and Pet slots exist in the enum but are locked.
- Base stats × 1.07^(itemLevel−1) × rarity mult (1.0/1.2/1.45/1.75/2.1/2.5) × (1 + 0.05 × upgrade) × type multiplier. Jewellery crit points scale linearly: (2 + 0.08 × itemLevel).
- Random attributes per rarity 0/0/1/1/2/3; no duplicates; percentages fixed ranges; flat attributes `min..max + 0.1 × itemLevel`; rarity biases rolls upward.
- Drops: 8% per normal kill, main hand weight ×3, item level = enemy level. Boss: first kill per run → guaranteed Rare+; later kills → 1 item at normal odds (M3-2).
- ~30% of drops are class items: one extra line "+8% damage, <class> only" (inactive on other classes).
- Never equip automatically (R2-18). One wearer per item (D-004). Equip requires character level ≥ item level − 5 (D-025).
- Upgrade cost `12 × 1.07^(itemLevel−1) × 1.2^upgrade` gold, max +5/+10/+15/+20/+25/+30 by rarity. Salvage: materials 1/2/4/8/16/32 by rarity + 50% of gold spent on upgrades.
- Inventory 200, +50 per expansion for gold (cost to be balanced in Task 7, configurable). Locked items never salvaged; full inventory auto-salvages and reports.
- Remove `CharacterState.PlaceholderArmourItemLevel` and the dev "Gear +10" button; dev panel gets "Drop item" (choose rarity) instead.

## Review Focus
1. Equipping an item worn by another character moves it (one wearer) → `Equip_ItemWornByOther_MovesIt`.
2. Inventory full during offline-sized drop bursts: nothing lost silently, counts reported → `FullInventory_AutoSalvagesAndReports`.
3. Upgrade at max level or without enough gold: no change, clear result → `Upgrade_AtMaxOrPoor_Refused`.
4. Salvaging an equipped or locked item: refused → `Salvage_EquippedOrLocked_Refused`.
5. Boss killed twice in one run: only the first guarantees Rare+ → `Boss_SecondKillSameRun_NoGuarantee`.

---

### Task 1: Item config (types, attribute pool, class lines) + loader
Append `items` section to `balance_config.json` (types per slot with stat multipliers, attribute pool with slot lists and ranges, class item share and bonus, drop/salvage numbers already in `gear`/`economy`); copy to `Assets/_Game/Data`. Tests: every active slot has 3 types; pool matches M3-3; loader rejects unknown slots/stats.

### Task 2: Item model and loot rolls
`Items/ItemInstance`, `Items/EquipSlot`, `Items/LootSystem` (`ItemInstance Roll(int itemLevel, Rarity? minRarity, ClassId? classHint, IRng)`), rarity weights, slot weights, type pick, attribute rolls, class lines. Tests: rarity distribution (chi-square, 200k rolls), attribute counts per rarity, no duplicate attributes, main-hand weight, deterministic per seed.

### Task 3: Gear stats and power score
`Items/GearStats` (equipped items → additions to `StatInputs`, incl. new fields PctArmour, GoldFind, DropChance, Luck, AttackSpeed), `Items/PowerScore`. Remove placeholder armour. Tests: golden values for item base stats vs Python; type multipliers; class line only for its class; power score prefers clearly better items.

### Task 4: Inventory, equipment, upgrades, salvage
`Items/InventoryState`, `Items/InventorySystem`, `Items/UpgradeSystem`. Tests: Review Focus 1–4, level requirement, lock, filtered salvage, auto-salvage modes, expansion, "Equip upgrades" (player-triggered) equips only ▲ items.

### Task 5: Session integration
Drops on kills and bosses (M3-2), materials currency, events `ItemDropped`, `ItemsAutoSalvaged`. Tests: Review Focus 5, drop rate over a long headless run, boss beatable with real loot (fresh Fighter reaches region 2 within 4 h game time).

### Task 6: Python simulation alignment
Sim uses the same types, attribute pool, boss rule and gear stats; golden item stats exported; results re-run; GAME_BALANCE updated.

### Task 7: Unity inventory screen
`Presentation/InventoryView` (equipped grid, inventory grid, detail panel, sort/filter, salvage controls), HUD materials, dev panel "Drop item". Owner verifies in the editor.

### Task 8: Docs, CLAUDE.md, finish
