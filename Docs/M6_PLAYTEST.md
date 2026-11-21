# M6_PLAYTEST.md: trying the skill trees in Unity

## Get the code
Close Unity, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg` (discard local changes first if `git status` shows any):

```bash
git fetch origin && git checkout m6/skills && git pull
```

Open the project and let it import. The scene picks up the new `skills_config.json` by itself in the editor; save the scene once (Ctrl+S) so the reference is stored. If the Console shows **red errors**, copy the first one or two to Claude (the Unity layer was written without an editor). After the import, commit the new `.meta` files:

```bash
git add -A Assets && git commit -m "Add Unity .meta files for M6" && git push
```

## Your existing save
It converts automatically (save version 3). Every hero starts with an empty tree and all the points their level has earned (1 per 5 levels), so a level-30 hero has 6 points waiting.

## What to try
1. **The Skills button** (top panel, next to Bag) shows a dot and turns gold while points wait. It opens the active hero's tree.
2. **Ranks and tiers:** tap a skill to read what it does now and at the next rank; "+1" spends a point. Tier 2 opens after 5 points spent, tier 3 after 10, tier 4 after 15. Actives go to 3 ranks, passives to 5.
3. **Loadout:** a learned active shows buttons 1 / 2 / 3: put it in a slot. Slot 1 fires first when several are ready. Up / Down / Remove reorder the slots.
4. **In combat:** a skill replaces one normal attack. Its name appears above the hero and its number is bigger (and in its element colour for the Mage). Rend, Venom Arrow and Thornbind leave small ticking numbers; Frost Lance slows the enemy's attacks; War Cry shows its name and raises damage for 6 s.
5. **Cooldowns carry over:** fights are short, so a skill often fires every second or third fight. Its timer keeps running between fights. Switch to another hero and back: each hero keeps their own timers.
6. **Passives:** Toughness and Arcane Ward raise HP, Evasive makes the Archer dodge ("Dodge"), Second Wind heals once a fight below 30% HP, Unbreakable survives one killing blow per boss fight.
7. **Auto-spend:** tick "Spend points automatically": points go by a suggested order, and new actives fill empty slots. Untick it and nothing is spent for you.
8. **Reset tree:** costs gold (shown on the button); tap twice to confirm. All points come back.
9. **Save and offline:** your ranks, loadout and auto-spend are saved; time away is fought with your skills.

## A balance question for you (decision M6-B1)
Skills make the heroes about 55–65% stronger by region 5. Over 30 simulated days that moves the best boss reached from region 40 to 48 for an active player and 26 to 33 for a casual one, and the Fighter now gets past the region-4 boss much sooner than the Archer or Mage. Claude will ask you how to handle it; play a little first and see if skills feel too strong, about right, or too weak.

Then tell Claude how it feels: is the tree clear, do skills read well at phone size, is the auto-cast satisfying?
