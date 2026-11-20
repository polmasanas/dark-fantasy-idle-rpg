# Creative Direction Milestone Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder boxes and "Region N" with the approved look (CD-0…CD-10): iron-and-ember UI, a cloaked Fighter, the Training Grounds as region 1 with practice targets, an instructor training boss and a recruit boss with the boss camera pull-back, named and coloured regions from data, and placeholder ambient/boss music with a crossfade.

**Architecture:** A new pure C# module `Game.Core/Look` reads `look_config.json` (content, not balance) and answers "how does this region / encounter / class look" with names, palette keys, shaded colours and sprite names; it is fully unit-tested. Art is SVG source in `Art/Source/`, rasterised by `Tools/ArtBuild/build_art.py` (cairosvg) into PNGs under `Assets/_Game/Resources/Art/`; the Unity layer loads them with `Resources.Load` and tints them in UI Toolkit. A USS theme replaces hard-coded colours.

**Tech Stack:** Unity 6000.3.29f1, UI Toolkit (built in code), C# 9, NUnit via `dotnet test Core.Tests.Dotnet`, Python 3 + cairosvg, Google Fonts (OFL).

**Spec:** `Docs/CREATIVE_DIRECTION.md` v0.2 (decisions CD-0…CD-10 in `Docs/DECISIONS.md`).

## Global Constraints

- Game.Core never references UnityEngine, `System.Random` or wall-clock time; colours in Core are plain RGB values parsed from hex strings.
- C# 9 only; warnings are errors in `Core.Tests.Dotnet`.
- Look data lives in `Assets/_Game/Data/look_config.json`. It is **not** a balance file: it is not mirrored in `Tools/BalanceSim/` and not exported to Sheets. `balance_config.json` is not changed by this milestone.
- Region 1's stats, families and the ON-1 tuning do not change (CD-8 is visual only). All 175 existing tests keep passing; golden values unchanged.
- Silhouettes `#07080B`; ground `#0B0D12`; panel `#191C24`; iron `#3A3F4C`; ink `#E6E1D6`; muted `#9A978F`; ember `#E8A24A` (spec §3.3).
- Depth: `depth.darkening = 0.03`, `depth.floor = 0.45`; accent colours are never darkened.
- Fonts: Cinzel (titles, region and boss names only) and Alegreya Sans (everything else), OFL; licence files ship next to the fonts and are listed in `Docs/CREDITS.md`.
- Normal enemies never kill the player (R2-1c); nothing equips automatically (R2-18); animations on/off give identical game results (M2).
- New Unity files need `.meta` files: remind the owner to `git add -A Assets` after first import (CLAUDE.md rule).

## Review Focus

1. Region numbers beyond the hand-written list (6, 25, 400) → a fallback name "the Wilds, depth N", a biome cycled from the list, and the darkening floor; never an exception. Test: `RegionLook_BeyondList_UsesFallbackAndCyclesBiome` (Task 1).
2. A missing or misspelt key in `look_config.json` (biome name in a region, kit name, sprite name) → a `LookConfigException` naming the path at load time, not a null at runtime mid-fight. Tests: `Loader_UnknownBiome_ThrowsNamedError`, `Loader_UnknownKit_ThrowsNamedError` (Task 1).
3. A missing PNG at runtime (art not rebuilt, name typo) → the view shows the plain silhouette box and logs one warning, never a crash. Verified in Task 4's owner check (Unity-side).
4. The boss camera when a boss fight ends early (player retreats, offline catch-up abandons the fight, Delete save restarts) → the camera always returns to 100% and the boss music fades out. Covered by `CombatView.Reset()` called from `BeginFight`, `Abandon` and boot (Task 4) and the owner check list.
5. Art drift: someone edits an SVG but not the PNG (or the reverse) → CI fails with the list of stale files. Test: `build_art.py --check` in CI (Task 3).

---

### Task 1: Look config, region looks and depth shading (Core)

**Files:**
- Create: `Assets/_Game/Data/look_config.json`
- Create: `Assets/_Game/Core/Look/LookConfig.cs` (data classes + `Rgb`), `Assets/_Game/Core/Look/LookConfigLoader.cs`, `Assets/_Game/Core/Look/RegionLooks.cs`
- Test: `Assets/Tests/EditMode/Core/Look/LookTests.cs`; add `public const string LookConfigPath = "Assets/_Game/Data/look_config.json";` to `Assets/Tests/EditMode/Core/TestPaths.cs`

**Interfaces:**
- Produces:
  - `readonly struct Rgb { float R, G, B; static Rgb Parse(string hex); Rgb Scale(double f); string ToHex(); }`
  - `sealed class BiomePalette { Rgb SkyTop, SkyBottom, Far, Near, Accent; }`
  - `sealed class LookConfig { Dictionary<string,BiomePalette> Biomes; double DepthDarkening, DepthFloor; List<RegionEntry> Regions; RegionEntry Fallback; CharacterLook TrainingBoss; Dictionary<string,string[]> Kits; Dictionary<ClassId,ClassLook> Classes; Dictionary<string,string> RarityColors, ElementColors; CameraLook Camera; AudioLook Audio; }`
  - `sealed class RegionEntry { string Name; string Biome; string Kit; CharacterLook Boss; }` (Fallback.Name is a format string with `{0}` = region number)
  - `sealed class CharacterLook { string Name; string Title; string Body; }`, `sealed class ClassLook { string Body; string Glow; }`, `sealed class CameraLook { double BossZoom, ZoomInS, ZoomOutS; }`, `sealed class AudioLook { double CrossfadeS, AmbientVolume, BossVolume; }`
  - `static class LookConfigLoader { LookConfig FromJson(string json); }` throwing `LookConfigException(string)` with the JSON path
  - `sealed class RegionLooks { RegionLooks(LookConfig c); RegionLook For(int region); }` → `sealed class RegionLook { int Region; string Name; string BiomeKey; string Kit; CharacterLook Boss; BiomePalette Palette /* already depth-shaded */; double Depth /* 1.0 … floor */; }`

**look_config.json content (exact values from the spec):**
- `biomes`: garrison `#8A8174 #4A443C #5A5248 #35302A #F2C66D`; ash `#76767A #3A3B40 #4A4B51 #2C2D32 #E8A24A`; moss `#5F6E5F #2D362F #3A463B #222A24 #B6E07A`; frost `#647A8E #2A3644 #344354 #1E2731 #8FD3FF`; ochre `#7A6A4F #382F22 #47392A #29211A #FFCF6E`; rust `#6B4C47 #2E1F1E #3B2826 #211615 #FF6F5E` (keys `sky_top, sky_bottom, far, near, accent`).
- `depth`: `{ "darkening": 0.03, "floor": 0.45 }`
- `regions` (index 0 = region 1):
  1. the Training Grounds · garrison · kit `training` · boss Bram Hollis, "First of the Recruits", body `recruit`
  2. the Ashen March · ash · `humanoid` · Oskar Venn, "Captain of Deserters", `humanoid_boss`
  3. the Drowned Fen · moss · `humanoid` · Ysolde Marr, "the Fen Witch", `humanoid_boss`
  4. the Frost Pass · frost · `humanoid` · Halvard Kesh, "the Frozen Oath", `humanoid_boss`
  5. the Vael Wastes · ochre · `humanoid` · Dorran Vael, "Lord of the Wastes", `humanoid_boss`
  6. Kharath Deep · rust · `humanoid` · Sefa Kharath, "Keeper of the Deep", `humanoid_boss`
- `fallback`: name `"the Wilds, depth {0}"`, kit `humanoid`, boss name `"Nameless Warlord"`, title `"of the Wilds"`, body `humanoid_boss` (biome cycles through `biome_cycle`: `["ash","moss","frost","ochre","rust"]`)
- `training_boss`: Maera Dunn, "Sergeant, Drillmaster", body `instructor`
- `kits`: `training`: `dummy, target_post, sparring_frame, shield_board, weighted_sack`; `humanoid`: `deserter, spearman, cultist, archer, brute`
- `classes`: fighter body `fighter` glow `#E8A24A`; archer `archer` `#B6E07A`; mage `mage` `#9B7BDC`
- `rarity_colors` Common `#B8B4AA` … Mythical `#FF4D5E`; `element_colors` fire `#FF7A3D`, nature `#8FD45A`, water `#5EC8E8`, light `#FFE08A`, dark `#9B6BFF` (spec §3.3)
- `camera`: `{ "boss_zoom": 0.6, "zoom_in_s": 1.5, "zoom_out_s": 1.0 }`; `audio`: `{ "crossfade_s": 2.0, "ambient_volume": 0.6, "boss_volume": 0.8 }`

- [ ] **Step 1: Write the failing tests** in `LookTests.cs`:
  - `Loader_ReadsRepoLookConfig`: 6 biomes; region 1 name "the Training Grounds"; `Camera.BossZoom == 0.6`; `Classes[ClassId.Fighter].Glow == "#E8A24A"`.
  - `Rgb_ParseAndScale`: `Rgb.Parse("#80FF00").Scale(0.5).ToHex() == "#408000"`.
  - `RegionLook_Region1_IsTrainingGroundsAtFullBrightness`: Name, BiomeKey "garrison", Kit "training", Boss.Name "Bram Hollis", Depth 1.0, Palette.Far.ToHex() == "#5A5248".
  - `RegionLook_DarkensWithDepth_AccentUnchanged`: region 10 Depth == 0.73 (±1e-9); Palette.Accent equals the biome's unshaded accent.
  - `RegionLook_DepthFloor`: region 40 Depth == 0.45.
  - `RegionLook_BeyondList_UsesFallbackAndCyclesBiome`: region 7 Name "the Wilds, depth 7", BiomeKey == biome_cycle[(7-7) % 5] == "ash"; region 8 → "moss"; region 400 does not throw.
  - `RegionLook_RegionZeroOrNegative_Throws` (ArgumentOutOfRangeException).
  - `Loader_UnknownBiome_ThrowsNamedError`: region biome "lava" → message contains `regions[0].biome`.
  - `Loader_UnknownKit_ThrowsNamedError`: kit "goblins" → message contains `regions[0].kit`.
  - `Loader_BadHex_ThrowsNamedError`: `"#12"` → message contains `biomes.ash.far`.
- [ ] **Step 2: Run** `dotnet test Core.Tests.Dotnet --filter LookTests` → build fails (types missing).
- [ ] **Step 3: Implement** the classes above. Loader mirrors `BalanceConfigLoader` style (Newtonsoft `JObject`, required keys, `_comment` keys ignored). Depth = `max(floor, 1 − darkening × (region − 1))`; fallback biome = `biome_cycle[(region − regions.Count − 1) % biome_cycle.Length]`.
- [ ] **Step 4: Run** `dotnet test Core.Tests.Dotnet` → all pass (175 + new).
- [ ] **Step 5: Commit** `git commit -m "CD task 1: look config, region looks, depth shading"`

### Task 2: Encounter and hero looks (Core)

**Files:**
- Create: `Assets/_Game/Core/Look/EnemyLooks.cs`
- Test: `Assets/Tests/EditMode/Core/Look/EnemyLookTests.cs`

**Interfaces:**
- Consumes: `LookConfig`, `RegionLooks.For`, `Encounter` (`Region, Subzone, Level, IsBoss, IsTraining`).
- Produces:
  - `enum LookRank { Normal, Elite, Boss }`
  - `sealed class EnemyLook { string Body; LookRank Rank; string GlowHex; string DisplayName; /* "Bram Hollis · First of the Recruits" or "Straw dummy" */ bool UseBossCamera; }`
  - `sealed class EnemyLooks { EnemyLooks(LookConfig c); EnemyLook For(Encounter e); ClassLook ForClass(ClassId c); }`
  - Rules: normal → `kits[kit][(Subzone − 1) % 5]`, rank Normal, glow = region accent, name = body key with underscores → spaces, first letter upper ("Target post"); region boss → region's `Boss`, rank Boss, `UseBossCamera = true`; training boss (`IsTraining`) → `training_boss`, rank Boss, `UseBossCamera = false` (it is the first-run tutorial and stays small); the element accent is reserved: `GlowHex` uses `element_colors[e]` when an element is passed (M5), otherwise the region accent.

- [ ] **Step 1: Write the failing tests**:
  - `Region1_Subzones_UseTheFiveTrainingTargets`: subzones 1..5 → `dummy, target_post, sparring_frame, shield_board, weighted_sack`; DisplayName for subzone 2 == "Target post".
  - `Region1_Boss_IsTheRecruit_WithCamera`: Body "recruit", Rank Boss, UseBossCamera true, DisplayName "Bram Hollis · First of the Recruits".
  - `TrainingBoss_IsTheInstructor_NoCamera`: `new Encounter(1, 1, 2, true, true)` → Body "instructor", UseBossCamera false.
  - `Region2_Normal_UsesHumanoidKit_WithAshAccent`: subzone 3 → "cultist", GlowHex "#E8A24A".
  - `Region9_Boss_UsesFallbackBoss`: Body "humanoid_boss", DisplayName "Nameless Warlord · of the Wilds".
  - `ForClass_Fighter`: Body "fighter", Glow "#E8A24A".
- [ ] **Step 2: Run** `dotnet test Core.Tests.Dotnet --filter EnemyLookTests` → fails (missing types).
- [ ] **Step 3: Implement** `EnemyLooks`.
- [ ] **Step 4: Run** `dotnet test Core.Tests.Dotnet` → all pass.
- [ ] **Step 5: Commit** `git commit -m "CD task 2: encounter and hero looks"`

### Task 3: Silhouette art and the build script

**Files:**
- Create: `Art/Source/` SVGs (viewBox units, black `#07080B` body; glow parts in a separate `<g id="glow">` drawn white): `fighter.svg`, `recruit.svg`, `instructor.svg`, `dummy.svg`, `target_post.svg`, `sparring_frame.svg`, `shield_board.svg`, `weighted_sack.svg`, `deserter.svg`, `spearman.svg`, `cultist.svg`, `archer.svg` (the humanoid enemy), `brute.svg`, `humanoid_boss.svg`; backgrounds (greyscale, white = full tint): `bg_sky_gradient.svg` (white→transparent, top to bottom), `bg_far_walls.svg` (garrison walls and towers), `bg_far_hills.svg`, `bg_near_yard.svg` (training yard fence, racks), `bg_near_hills.svg`; `fx_glow.svg` (white radial).
- Create: `Tools/ArtBuild/build_art.py`, `Tools/ArtBuild/requirements.txt` (`cairosvg==2.7.1`)
- Create: `Assets/_Game/Resources/Art/*.png` (generated)
- Modify: `.github/workflows/core-tests.yml` (add an art freshness step)

**Interfaces:**
- Produces, per character SVG `<name>.svg`: `Resources/Art/<name>.png` (everything except the glow group) and `Resources/Art/<name>_glow.png` (only the glow group); per background/fx SVG: `Resources/Art/<name>.png`. Heights: characters 512 px tall, bosses (`recruit`, `humanoid_boss`) 1024 px, backgrounds 1080×1920 or 2048×1024 (far/near strips, transparent above the outline). Unity loads them as `Resources.Load<Texture2D>("Art/<name>")`.
- `build_art.py [--check]`: renders every SVG; `--check` renders into a temp dir and exits 1 listing PNGs that differ from or are missing in `Assets/_Game/Resources/Art/` (byte compare after rendering with fixed settings).

- [ ] **Step 1: Draw the SVGs** following spec §3.1–3.2 and the approved mockups: cloaked warden Fighter (broad pauldrons, huge blade, ember eye-slit in `glow`); recruit = warden silhouette with a plain hood and a training sword; instructor = broader warden with a staff and a whistle-cord; the five practice targets (straw dummy on a post, round target on a tripod, sparring frame with padded arms, shield nailed to a pole, sack hanging from a gallows); five humanoids (deserter with sword and torn cloak, spearman, hooded cultist with orb, humanoid archer with bow, brute with maul); `humanoid_boss` = towering armoured warlord with banner pole, eyes in `glow`. Every name must match `look_config.json` kits/bodies exactly.
- [ ] **Step 2: Write `build_art.py`** (cairosvg; split the glow group by rendering the SVG twice with the `glow` group hidden/only via a parsed copy; deterministic output). Run `python3 Tools/ArtBuild/build_art.py` → PNGs written; run `python3 Tools/ArtBuild/build_art.py --check` → exit 0.
- [ ] **Step 3: Add a Core test** `LookAssetTests.EveryBodyAndBackgroundHasAPng` in `Assets/Tests/EditMode/Core/Look/LookAssetTests.cs`: for every kit body, boss body, training boss body, class body in the repo `look_config.json`, `Assets/_Game/Resources/Art/<body>.png` and `<body>_glow.png` exist; and `bg_sky_gradient, bg_far_walls, bg_far_hills, bg_near_yard, bg_near_hills, fx_glow` exist. Run `dotnet test Core.Tests.Dotnet` → pass.
- [ ] **Step 4: CI**: add steps `pip install -r Tools/ArtBuild/requirements.txt` and `python3 Tools/ArtBuild/build_art.py --check` to `core-tests.yml`. Push and confirm the workflow is green.
- [ ] **Step 5: Publish a contact sheet** of all PNGs on a black and a frost background as an artifact for the owner to glance at before Unity work starts; fix anything unreadable.
- [ ] **Step 6: Commit** `git commit -m "CD task 3: silhouette art source, build script, sprites"`

### Task 4: Iron-and-ember UI theme and region names (Unity)

**Files:**
- Create: `Assets/_Game/Resources/Fonts/Cinzel-Bold.ttf`, `AlegreyaSans-Regular.ttf`, `AlegreyaSans-Bold.ttf` + `OFL-Cinzel.txt`, `OFL-AlegreyaSans.txt` (from the google/fonts GitHub repository)
- Create: `Assets/_Game/Resources/UI/IronEmber.uss`, `Docs/CREDITS.md`
- Modify: `Assets/_Game/Presentation/Ui.cs` (palette constants to the spec tokens; `PanelBox()` adds class `panel`; `Button()` adds `iron-button`; new `Title(string, int)` using class `title`), `GameBootstrap.cs` (load the USS and add it to `root.styleSheets`; new `[SerializeField] TextAsset lookConfig` pointing at `Assets/_Game/Data/look_config.json`, parsed with `LookConfigLoader.FromJson`; a missing assignment logs an error like the balance config does), `HudView.cs` (location line "the Training Grounds · 1-3" from `RegionLooks`), `InventoryView.cs` (rarity colours from `LookConfig.RarityColors`), `OfflineReportView.cs`, `CustomPresetView.cs`, `DebugPanel.cs` (titles via `Ui.Title`), `GameBootstrap.OnWorldChanged` texts ("Welcome to the Ashen March").
- Modify: the scene-setup editor tool in `Assets/_Game/Editor/` so `DFIRPG → Set Up M2 Scene` also assigns `look_config.json`; the owner either reruns it or drags the file onto the field once.

**Interfaces:**
- Consumes: `LookConfigLoader.FromJson`, `RegionLooks.For(int).Name`, `LookConfig.RarityColors`.
- Produces: `Ui.Title(string text, int size)`, USS classes `panel`, `panel--title` (adds ember corner marks), `iron-button`, `title`, `rarity-<name>`; `Ui.Hex(string) → Color`.

- [ ] **Step 1: Fonts and credits**: download the three TTFs and OFL texts; write `Docs/CREDITS.md` (font, author, licence, source URL).
- [ ] **Step 2: Write `IronEmber.uss`** per spec §3.5: panel fill `#191C24`, 2 px `#3A3F4C` border, inner bevel via a 1 px `border-top-color` lighter `#4A5060`, ember corner marks with two absolutely positioned child elements on `.panel--title`; buttons dark iron gradient approximation (background `#232834`, border `#454B59`, Cinzel 0.8em, letter-spacing 1px); default font Alegreya Sans via `-unity-font-definition: resource("Fonts/AlegreyaSans-Regular")`; `.title` uses Cinzel.
- [ ] **Step 3: Apply it**: change `Ui` helpers and views as listed; remove remaining hard-coded colours from views (search `new Color(` in `Assets/_Game/Presentation` → only `Ui.cs` remains).
- [ ] **Step 4: Owner check in the editor** (record results in the plan ledger): HUD, Bag, Custom editor, welcome-back report and dev panel all use the theme; region name reads "the Training Grounds · 1-1" on a new game; no red Console errors; on a 1080×1920 Game view nothing is cut off.
- [ ] **Step 5: Commit** `git commit -m "CD task 4: iron and ember theme, fonts, region names"`

### Task 5: The fight scene, motion and the boss camera (Unity)

**Files:**
- Create: `Assets/_Game/Presentation/SceneryView.cs` (sky colour + tinted gradient + far/near/ground layers, slow parallax drift), `Assets/_Game/Presentation/Silhouette.cs` (a `VisualElement` with body + tinted glow images, idle sway, lunge, hit flash), `Assets/_Game/Presentation/ArtLibrary.cs` (cached `Resources.Load<Texture2D>("Art/" + name)`; a missing texture logs one warning per name and returns null → `Silhouette` falls back to a plain `#07080B` box)
- Modify: `Assets/_Game/Presentation/CombatView.cs` (replace boxes with `SceneryView` + two `Silhouette`s; enemy name card from `EnemyLook.DisplayName` in Cinzel; boss camera), `GameBootstrap.cs` (create `RegionLooks`/`EnemyLooks`, pass to `CombatView`; call `_combat.Reset()` on boot and after `_replayer.Abandon()`)

**Interfaces:**
- Consumes: `EnemyLooks.For(Encounter)`, `EnemyLooks.ForClass(ClassId)`, `RegionLooks.For(int)`, `LookConfig.Camera`.
- Produces: `CombatView(LookConfig look, RegionLooks regions, EnemyLooks enemies)`; `CombatView.Reset()` (camera 100%, boss music off via the event below, scenery set to the current region); `CombatView.BossCameraChanged` event `Action<bool>` (true when the pull-back starts, false when it returns) for Task 6.

- [ ] **Step 1: Scenery**: `SceneryView.SetRegion(RegionLook)` sets sky top as background colour, `bg_sky_gradient` tinted sky bottom, far layer (`bg_far_walls` for kit `training`, else `bg_far_hills`) tinted far, near layer (`bg_near_yard` / `bg_near_hills`) tinted near, ground strip `#0B0D12`; layers drift horizontally at 2 / 5 px per second (looping).
- [ ] **Step 2: Silhouettes**: hero on the left facing right, enemy on the right facing left (mirror with `scale.x = -1`); sizes per spec §3.1 (hero 1/6 of the arena height; normal enemy 0.9× hero; boss 4× hero, anchored bottom-right so it runs off the frame); idle sway ±1.5° every 3 s; on the attacker's hit event a 0.15 s lunge of 6% arena width; on the target a 0.1 s white flash (tint) — durations as `const` in `Silhouette` (presentation only).
- [ ] **Step 3: Boss camera**: on `BeginFight` with `UseBossCamera`, animate the arena container's `scale` from 1 to `Camera.BossZoom` over `Camera.ZoomInS`, raise `BossCameraChanged(true)`; on `EndFight` of that fight animate back over `ZoomOutS` and raise `BossCameraChanged(false)`. `Reset()` snaps to 1 and raises `false` if the camera was pulled back.
- [ ] **Step 4: Owner check**: new game → the Training Grounds with walls behind, a straw dummy in 1-1, target post in 1-2; the instructor training boss appears after the first wave at normal size; the recruit boss pulls the camera back and fills the right side; Simulate 12 h away during a boss fight leaves the camera at 100%; x20 speed has no stuck animations; region 2 shows ash hills and humanoids.
- [ ] **Step 5: Commit** `git commit -m "CD task 5: fight scenery, silhouettes, boss camera"`

### Task 6: Placeholder music layers and crossfade (Unity + Tools)

**Files:**
- Create: `Tools/ArtBuild/make_audio.py` (numpy-free: Python `wave` + `math`; writes 16-bit 44.1 kHz mono loops), output `Assets/_Game/Resources/Audio/ambient_loop.wav` (30 s: filtered noise "wind" + a slow 3-note low tone with a soft bell every 8 s) and `boss_loop.wav` (16 s: low drum hits at 70 bpm + a sustained low brass-like saw chord); both seamless loops
- Create: `Assets/_Game/Presentation/MusicService.cs` (two `AudioSource`s on the bootstrap GameObject, looping; `SetBoss(bool)` crossfades over `Audio.CrossfadeS` to `BossVolume`/0 and `AmbientVolume`/0; `Muted` property)
- Modify: `GameBootstrap.cs` (create `MusicService`, subscribe to `CombatView.BossCameraChanged`; start ambient after boot), `DebugPanel.cs` (a "Sound on/off" button), `Docs/CREDITS.md` (audio: generated by `make_audio.py`, no third-party material)

**Interfaces:**
- Consumes: `CombatView.BossCameraChanged`, `LookConfig.Audio`.
- Produces: `MusicService(GameObject host, AudioLook settings)`, `void SetBoss(bool on)`, `bool Muted { get; set; }`, `void Tick(float unscaledDelta)` called from `GameBootstrap.Update`.

- [ ] **Step 1: Write and run `make_audio.py`**; listen to both files' first seconds by checking duration and peak level in the script output (`ambient_loop.wav 30.0 s peak -6 dB`, `boss_loop.wav 16.0 s peak -3 dB`).
- [ ] **Step 2: Implement `MusicService`** and wire it.
- [ ] **Step 3: Owner check**: ambient plays on start; the recruit boss fades the drums in over about 2 s; they fade out after the fight; "Sound off" silences both; muting changes nothing in the game.
- [ ] **Step 4: Commit** `git commit -m "CD task 6: placeholder ambient and boss music with crossfade"`

### Task 7: Docs, playtest guide, Jira

**Files:**
- Create: `Docs/CD_PLAYTEST.md` (pull, what to look at: theme, Training Grounds, targets, instructor, recruit + camera + music, region 2 colours and names, sound toggle; ask for feedback on readability, mood, names)
- Modify: `CLAUDE.md` (status row "Creative Direction" in progress → done after merge; where things live: `Art/Source`, `Tools/ArtBuild`, `Assets/_Game/Resources/Art`, `look_config.json`; commands: `python3 Tools/ArtBuild/build_art.py`), `Docs/TECHNICAL_ARCHITECTURE.md` (Look module paragraph), `Docs/DEVELOPMENT_ROADMAP.md` (CD row between M4 and M5), `Docs/CREATIVE_DIRECTION.md` (status: implemented parts)
- Jira: create epic "CD – Creative Direction (look, sound, naming)" in `DFIRPG` with the spec summary; set to Done after the merge.

- [ ] **Step 1: Write the docs** above.
- [ ] **Step 2: Run** `dotnet test Core.Tests.Dotnet` and `python3 Tools/ArtBuild/build_art.py --check` → both pass; CI green.
- [ ] **Step 3: Commit** `git commit -m "CD task 7: docs and playtest guide"`; push; ask the owner for the editor playtest, then the pull request and merge as in M4 (remind about `.meta` files first).
