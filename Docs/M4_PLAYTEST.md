# M4_PLAYTEST.md: trying saves and offline progress in Unity

## Get the code
Close Unity, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg`:

```bash
git fetch origin && git checkout m4/save-offline && git pull
```

Open the project. The scene needs no changes. If the Console shows **red errors**, copy the first one or two to Claude (the Unity layer was written without an editor).

## Press Play and try
1. **It remembers.** Play a minute (x20 is fine), note your level, gold and bag. **Stop**, then **Play** again: everything is back, including settings, and the same region and kills.
2. **While you were away.** Stop Play mode, wait **3 minutes or more**, press Play: the report shows the time away, the time credited (half), kills, levels, gold, materials, the items waiting (by rarity, ▲ = upgrades) and bag space. **Collect** moves the items into the bag; what does not fit is salvaged. Under 2 minutes away is credited silently.
3. **Leave items uncollected.** Open the report, don't press Collect, Stop and Play again: the items are still waiting.
4. **Auto-allocate on level-up** (bottom panel): tick it and new attribute points are spent automatically, online and offline. **Custom** off = everything into STR (the Fighter's primary); on = your weights. **Edit** sets a weight 0–10 per attribute and turns Custom on. Remember: of the 4 points per level, 3 are automatic and follow the class template (Fighter: mostly STR, D-014); the preset only steers the 1 manual point, so STR still grows fastest.
5. Dev panel:
   - **Simulate 12 h away** shows the report for 6 h of fighting and how many ms it took (on a PC this should be well under 100 ms).
   - **Save now** saves at once.
   - **Delete save** wipes the save files and starts a new game.
6. **Damaged save (optional).** Find the save folder: in the Console look for "SaveService: loaded save.dfrs from …" (normally `C:\Users\<you>\AppData\LocalLow\DFIRPG Placeholder\Dark Fantasy Idle RPG`). With Unity stopped, open `save.dfrs` in Notepad, type a few letters anywhere, save, press Play: the game loads the newest backup and the Console warns about the damaged file.

Note: Unity's **Pause** button counts as time away (like putting the phone in your pocket), so a long pause gives a report on resume.

Then tell Claude how it felt: is the report clear, is Collect obvious, is the Custom preset editor understandable?
