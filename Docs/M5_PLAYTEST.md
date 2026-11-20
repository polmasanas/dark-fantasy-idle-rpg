# M5_PLAYTEST.md: trying the three heroes and the families in Unity

## Get the code
Close Unity, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg` (discard local changes first if `git status` shows any):

```bash
git fetch origin && git checkout m5/classes-world && git pull
```

Open the project and let it import (about 400 new sprites). The scene needs no changes. If the Console shows **red errors**, copy the first one or two to Claude (the Unity layer was written without an editor). After the import, commit the new `.meta` files:

```bash
git add -A Assets && git commit -m "Add Unity .meta files for M5" && git push
```

## Your existing save
It converts automatically (save version 2): your Fighter keeps everything. If you were past region 1, the Archer and Mage are unlocked straight away with their starter bow and staff.

## What to try
1. **The hero bar** (top panel): Fighter · level, Archer, Mage. On a new game (dev panel → Delete save) the Archer and Mage show as **locked**.
2. **The unlock:** beat Bram Hollis (region 1 boss). The status line says "The Archer and the Mage join you" and their buttons pulse.
3. **Switching:** tap the Archer during a normal fight: the fight on screen is dropped and the Archer takes the field. During any boss fight (drillmaster included) the buttons are greyed out and tapping says why. Each hero keeps their own level, points and gear; the HUD's points and toggles follow the active hero.
4. **Catch-up:** a hero below the highest hero's level earns +50% XP.
5. **Gear between heroes:** in the Bag, an item worn by another hero says "Worn by the …"; equipping it asks "Move it to the …?".
6. **Families:** the Ashen March (humanoids), the Drowned Fen (beasts: they dodge, the Archer is strongest), the Frost Pass (elementals of Fire / Nature / Water with matching glows, the Mage is strongest; the last subzone has Light wizards), the Vael Wastes (Dark undead, the Fighter is strongest).
7. **Elements:** gear with "adds X% Fire/…" lines shows coloured damage numbers; elementals resist their own element; elemental enemies drop more elemental gear. Light gear hits the undead hard.
8. **Bosses:** Grisel the Fen Mother, the Frozen Oath, Dorran Vael the Hollow Lord.

Then tell Claude how it feels: is the "right hero per region" noticeable, is switching clear, do the new enemies read well at phone size?
