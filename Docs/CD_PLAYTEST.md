# CD_PLAYTEST.md: trying the new look in Unity

## Get the code
Close Unity, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg`:

```bash
git fetch origin && git checkout cd/creative-direction && git pull
```

Open the project and let it import (new fonts, sprites and sounds). The scene needs no changes: the bootstrap fills in the new **Look Config** field (`Assets/_Game/Data/look_config.json`) by itself. If the Console shows **red errors**, copy the first one or two to Claude (the Unity layer was written without an editor). After the import, commit the new `.meta` files:

```bash
git add -A Assets && git commit -m "Add Unity .meta files for Creative Direction" && git push
```

## What to look at
1. **Panels and fonts.** Dark slate panels with iron borders; the top bar has ember corner marks; titles, buttons, region and boss names in carved capitals (Cinzel); everything else in Alegreya Sans. Check the HUD, Bag, Custom editor, the "While you were away" report and the dev panel.
2. **The Training Grounds.** Use **Delete save** for a fresh start. The top bar reads "The Training Grounds · 1-1"; walls and weapon racks behind; a straw dummy to beat. Each subzone has its own practice target (dummy, target post, sparring frame, shield board, weighted sack).
3. **The Fighter.** A cloaked warden with a huge blade and an ember eye-slit; it sways, lunges when it attacks, and its halo flashes when hit.
4. **The drillmaster** (training boss after the first wave): Maera Dunn, normal size, no camera move.
5. **The recruit boss** (end of region 1): the camera pulls back over about 1.5 s, the world and the Fighter shrink, Bram Hollis looms in from the right, partly off-screen, and drums fade in. After the fight everything eases back.
6. **Region 2 and later.** "The Ashen March": grey ash hills, humanoid enemies (deserter, spearman, cultist, archer, brute), and each region a little darker.
7. **Sound.** Wind, a low drone, a slow cello-like motif and a distant bell; drums and brass for bosses. **Sound on/off** in the dev panel mutes both.
8. **Edge cases.** **Simulate 12 h away** during a boss fight: the camera snaps back and the drums fade out. x20 speed: no stuck animations. 1080×1920 Game view: nothing cut off.

Then tell Claude how it feels: the mood, readability of the silhouettes at phone size, the boss moment, the names and the music. Art and sound here are placeholders made by Claude; the direction is what we're judging.
