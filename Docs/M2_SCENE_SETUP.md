# M2_SCENE_SETUP.md: running the playable loop in the editor

One-time setup in Unity (about 5 minutes). Close nothing in between; Unity saves as you go.

## 1. Get the M2 code
Close Unity first, then in Git Bash from `D:\Dev\dark-fantasy-idle-rpg`:

```bash
git fetch origin
```

```bash
git checkout m2/playable-loop
```

Open the project from Unity Hub. If the Console shows **red compile errors**, copy the first one or two to Claude before continuing: the Unity layer was written without access to the editor, so a small fix may be needed.

## 2. Create the UI settings asset
1. In the **Project** window, right-click `Assets/_Game` → **Create → UI Toolkit → Panel Settings Asset**. Name it `GamePanelSettings`.
   (Unity also creates a default theme file next to it the first time; keep it.)
2. Select `GamePanelSettings` and in the **Inspector** set:
   - **Scale Mode:** Scale With Screen Size
   - **Reference Resolution:** X `1080`, Y `1920`
   - **Screen Match Mode:** Match Width Or Height, **Match** `0.5`

## 3. Set up the scene
1. Open `Assets/Scenes/SampleScene` (double-click).
2. **GameObject → Create Empty**, name it `Game`.
3. With `Game` selected, **Add Component → UI Document**. Drag `GamePanelSettings` into its **Panel Settings** field. Leave **Source Asset** empty (the UI is built in code).
4. **Add Component → Game Bootstrap**. Drag `Assets/_Game/Data/balance_config.json` into its **Balance Config** field. Leave **Seed** at 0.
5. Save the scene (**Ctrl+S**).

## 4. Make the Game view portrait
In the **Game** tab, open the resolution dropdown (says "Free Aspect" by default) → **+** → add `1080 x 1920` (Fixed Resolution), and select it.

## 5. Press Play
What you should see:
- **Top:** Region 1 · Subzone 1-1, kills counter, level, XP bar, gold.
- **Middle:** the blue Fighter and a red enemy, HP bars, floating damage numbers ("Miss", gold "123!" for crits, "Counter").
- **Bottom:** attribute points with **+** buttons, the preset dropdown and **Auto**, the boss toggle.
- **Dev panel:** speed x1 / x5 / x20, **Gear +10**, **+5 levels**.

Things to try:
1. Press **x20** and watch the subzones unlock (10 kills each in region 1).
2. When "Boss ready" shows, the dark-red boss appears. **Without gear the boss wins** (no armour exists until loot in M3); the status line says so and it retries every 60 s.
3. Press **Gear +10** once (weapon and placeholder armour at item level 11): the boss becomes beatable, and region 2 opens.
4. Untick **Fight boss when available**: the Fighter keeps farming subzone 5.
5. Spend points with **+**, or pick a preset and press **Auto**.

## 6. Commit the scene and settings
After it works, in Git Bash:

```bash
git add -A
```

```bash
git commit -m "M2: game scene with UI Document and bootstrap"
```

```bash
git push
```

Then tell Claude how it felt: pacing, readability, anything confusing.
