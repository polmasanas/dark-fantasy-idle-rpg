# M3_PLAYTEST.md: trying loot and the inventory in Unity

## Get the code
Close Unity, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg`:

```bash
git fetch origin && git checkout m3/loot-inventory && git pull
```

Open the project. The scene from M2 needs no changes (the bootstrap builds the new UI in code). If the Console shows **red errors**, copy the first one or two to Claude (the Unity layer was written without an editor).

## Press Play and try
1. **x20 speed.** Watch the status line: Rare and better drops are announced ("Loot: Rare Greataxe", "Upgrade! …").
2. **Bag** (top bar): equipped slots (▲ = a better item is waiting), the bag grid with rarity colours (grey, green, blue, purple, orange, red), sort and filter.
3. Tap an item: stat lines with ▲/▼ differences against what you wear, "Power +x% if equipped", and **Equip / Upgrade / Lock / Salvage**.
4. **Equip upgrades** equips everything that raises your power. Nothing is ever equipped without a tap.
5. **Training boss:** after the first 10 kills a brown "Training Boss" appears (once per account). It cannot defeat you and drops a guaranteed Rare+ item.
6. **The region 1 boss** now falls in about 9 minutes at x1 (median) if you keep equipping upgrades. The first kill of each boss in a run drops a guaranteed Rare or better item.
7. **Salvage:** "Salvage shown" salvages every unlocked item in the current filter; **Auto-salvage** (off by default) handles new drops. Locked items are never salvaged. A full bag salvages new drops and says so.
8. **+50 slots** expands the bag for gold.
9. Dev panel: **Drop Rare / Epic / Legendary** for quick testing.

Then tell Claude how it felt: readability of the bag, the comparison, pacing of drops, anything confusing.
