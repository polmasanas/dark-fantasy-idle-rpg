# CREATIVE_DIRECTION.md: how the game looks, sounds and is named

> **Status:** v0.2, approved by the owner on 2026-11-20 (with CD-8 and CD-9 added in review). Decisions CD-0…CD-10 are logged in DECISIONS.md. **Built** on branch `cd/creative-direction` (plan `Docs/plans/2026-11-20-creative-direction.md`): §3 (UI, Fighter, Training Grounds, humanoids, boss camera), §4 region and boss names for regions 1–6, §5 placeholder music. Not yet built: Archer/Mage in play and the other families (M5), the naming generator (M5/M10). Items marked **[Claude's call]** were not asked; the owner can overrule them.
> **Replaces:** coloured placeholder boxes, "Region N" names and the default UI colours from M2–M4.

## 1. Intent

The owner's brief: the vibe of **Game of Thrones × Attack on Titan × Hollow Knight**.

| Source | What we take | What we do not take |
|---|---|---|
| Game of Thrones | A grim, believable medieval world: houses, banners, winter coming, victories that cost something | Its characters, houses, places or names |
| Attack on Titan | Scale and dread: small humans against enormous threats, humanity behind walls | Titans, uniforms, gear, anime styling |
| Hollow Knight | 2D silhouettes, muted layered backgrounds, melancholy quiet, colour only in small glows | Its characters (masked bug-knights), its world or its art |

**Premise (CD-0, accepted with the vibe):** the last kingdom lives behind great walls. The heroes are **wardens** who push out into a darkening wilderness; each region is further from the walls and darker. **Region 1 is the Training Grounds inside the walls** (CD-8): recruits train against practice targets before the gates open; region 2 is the first land beyond the walls. Prestige is a retreat behind the walls to rebuild stronger, which matches the pillar "the wall is a friend".

**Goals:** a look that is recognisable from one screenshot; readable at phone size during thousands of idle fights; cheap to extend to hundreds of generated regions (R2-10, R2-22).
**Non-goals (for now):** painted art, character portraits, cutscenes, voice acting, final music.
**Anti-goals:** bright cartoon cuteness, gore, anime-styled characters, anything that copies the three sources' own characters or names, colour noise that hides loot glows.

## 2. Decisions

| ID | Decision | Owner's choice |
|---|---|---|
| CD-1 | **Art source** | Silhouette art built by Claude in code/data (dark shapes, glowing accents, layered backgrounds, recolourable from data). A human artist may replace it later (M10–M12) without code changes |
| CD-2 | **Fight screen layout** | Side view for normal fights (hero left, enemy right). When a boss arrives, the camera **pulls back**: the hero shrinks and the boss fills the frame, partly off-screen. Same side-view drawings for both |
| CD-3 | **Region colour** | A **muted biome palette** per region, **darker with distance** from the walls. Two data dials: biome × depth. The glow accent follows the biome |
| CD-4 | **Hero body style** | **Cloaked wardens**: lean hooded figures (about 1:5). One big shape names each class: Fighter broad pauldrons + huge blade; Archer deep hood + long cape + bow; Mage tall pointed cowl + long robe + staff. A thin glowing eye-slit carries the class colour |
| CD-5 | **Interface style** | **Iron and ember**: dark slate panels, bevelled iron border, small ember-gold corner marks, carved-capital titles |
| CD-6 | **Naming** | Places mostly plain English (the Ashen March, the Frost Pass), **with invented fantasy names here and there** (the Vael Marshes, Kharath Deep). People and houses get invented names ("Oskar Venn, Warden of the Third Gate", House Varrow). The share of invented place names is a data setting |
| CD-8 | **Region 1 = the Training Grounds** (owner, design review) | Inside the walls. Normal enemies are **practice targets** (straw dummies, target posts, sparring frames); the **region 1 boss is a recruit** (the top of your training class, a graduation duel). Regions 2+ are the wilderness |
| CD-9 | **Enemy shapes = 5 bodies per family + accents** (owner, design review) | Each family has **5 base body shapes** to pick from; **accents** layered on top tell the rest: damage element, combat archetype, rank (normal / elite / boss) and family traits |
| CD-7 | **Sound** | Sparse and melancholy for everyday play (wind, distant bells, a lone cello or piano motif per biome); when a boss arrives, drums and low brass swell in, then fade back after the fight |

## 3. Visual language

### 3.1 Shapes
- **Everything alive is a black silhouette** (`#07080B`). Detail comes from the outline, never from interior drawing.
- **Glow is the only colour on characters:** eyes, eye-slits, weapon edges, spells. Glows are tinted by data (class, element, rarity, biome).
- **Backgrounds are 3–4 flat layers** (sky gradient, far layer, near layer, ground) with slow parallax. Layers are drawn in greyscale and **tinted from the region palette**, so one set of shapes serves every biome.
- **Size rules:** heroes about 1/6 of screen height in normal fights; normal enemies 0.6–1.2× hero height by family; bosses 3–5× hero height and drawn larger than the frame, cut off by an edge.

### 3.2 Enemy families (CD-9)
An enemy picture = **one of its family's 5 base bodies** + **accents**. The base body says *what* it is; accents say *how it fights*. Both are chosen in data per enemy (or rolled by the generator), so the same parts make many enemies.

| Accent | Shows | How |
|---|---|---|
| Element | Damage element (Fire, Nature, Water, Light, Dark; none = Physical) | Colour of eyes, weapon edge and core glow (§3.3) |
| Archetype | Brute / Marksman / Caster (GDD §4) | Held item: heavy weapon or shield / bow, spear-thrower / staff, orb, floating runes |
| Rank | Normal / Elite / Boss | Elite: a second glow ring and a tattered banner or crown shape; Boss: drawn 3–5× larger plus a name card |
| Trait marks | Family traits (R2-11) | Constructs: armour plates; Beasts: motion streaks (evasion); Undead: cracked outline; Demons: ember sparks |

Base-body ideas per family [Claude's call; the 5 bodies are drawn in the milestone that introduces the family]:

| Family | Outline idea | Glow |
|---|---|---|
| Humanoids | Ragged soldiers, deserters, cultists: spears, banners | Torch orange |
| Beasts | Low, hunched, many-legged or long-snouted; fast | Eyes only (biome accent) |
| Undead | Thin, broken armour, tilted heads | Pale green-white |
| Demons | Horns and wings, sharp angles | Deep red |
| Elementals | Shapeless masses with a bright element core | Their element colour |
| Constructs | Blocky, riveted, wall-like; the walls' own failed guardians | Ember cracks |
| Dragons | Long necks and wings; usually bosses, always cut off by the frame | Throat glow by element |
| *Training* (region 1 look only) | Straw dummy, target post, sparring frame, shield-board on a pole, a weighted sack; the boss is a recruit (a cloaked-warden silhouette with a plain hood) | Faint chalk-white marks |

**The Training look is visual only** [Claude's call]: region 1 keeps the stat family it has today (no Construct armour-penetration rule in the first region), so the M2–M4 balance and the ON-1 tuning do not change.

### 3.3 Colour
**Base (world and UI):**
| Token | Hex | Use |
|---|---|---|
| silhouette | `#07080B` | All characters |
| ground | `#0B0D12` | Floor strip under the fight |
| panel | `#191C24` | UI panels |
| iron | `#3A3F4C` | Panel borders |
| ink | `#E6E1D6` | Text |
| muted | `#9A978F` | Secondary text |
| ember | `#E8A24A` | Gold, corner marks, highlights |

**Biome palettes (CD-3)**, each = sky top, sky bottom, far layer, near layer, accent glow. Starting set (more are added as data):
| Biome | Sky top | Sky bottom | Far | Near | Accent |
|---|---|---|---|---|---|
| Garrison (region 1, inside the walls) | `#8A8174` | `#4A443C` | `#5A5248` | `#35302A` | `#F2C66D` |
| Ash | `#76767A` | `#3A3B40` | `#4A4B51` | `#2C2D32` | `#E8A24A` |
| Moss | `#5F6E5F` | `#2D362F` | `#3A463B` | `#222A24` | `#B6E07A` |
| Frost | `#647A8E` | `#2A3644` | `#344354` | `#1E2731` | `#8FD3FF` |
| Ochre | `#7A6A4F` | `#382F22` | `#47392A` | `#29211A` | `#FFCF6E` |
| Rust | `#6B4C47` | `#2E1F1E` | `#3B2826` | `#211615` | `#FF6F5E` |

**Depth (distance from the walls):** every palette colour except the accent is multiplied by `1 − depth_darkening × (region − 1)`, never below `depth_floor`. Proposed values: `depth_darkening = 0.03`, `depth_floor = 0.45` (region 1 = 100%, region 10 ≈ 73%, region 19+ = 45%) [Claude's call; tune by eye].

**Fixed meaning colours** (never reused for decoration):
- Rarity: Common `#B8B4AA`, Uncommon `#6FC36B`, Rare `#5AA0FF`, Epic `#B07CFF`, Legendary `#FF9A3C`, Mythical `#FF4D5E` (the order already used in M3, retuned to the new palette).
- Elements: Fire `#FF7A3D`, Nature `#8FD45A`, Water `#5EC8E8`, Light `#FFE08A`, Dark `#9B6BFF`, Physical = no glow.
- Classes (eye-slit): Fighter ember `#E8A24A`, Archer moss `#B6E07A`, Mage violet `#9B7BDC`.
- Health: player green `#5FAE6A`, enemy red `#D0464A`; XP violet `#9B7BDC`.

### 3.4 Type
- **Titles:** Cinzel (carved capitals; free, Google Fonts / OFL), used for screen titles, region names and boss names only.
- **Body and numbers:** Alegreya Sans (OFL), tabular figures for stats.
- Both ship inside the game as font assets (no downloads at runtime) [Claude's call].

### 3.5 Interface (CD-5)
- Panels: `panel` fill, 2 px `iron` border with an inner dark line and a 1 px light bevel on top, ember corner marks on title panels only.
- Buttons: dark iron gradient, Cinzel small caps, 56 px minimum height (current rule).
- Item cards: border in the rarity colour plus a soft outer glow for Rare and above.
- The ▲/▼ upgrade colours stay green/red.
- All of this is a UI Toolkit theme (`GameRuntimeTheme.tss` + one USS file) instead of colours set in code, so views stop hard-coding colours.

### 3.6 Motion and the boss camera (CD-2)
- Idle: cloaks sway, background layers drift slowly, glows flicker gently.
- Hits: a short lunge, a white flash on the target's outline, damage numbers in Cinzel rising and fading; crits larger and ember-coloured.
- **Boss arrival:** about 1.5 s pull-back (camera zoom out to about 60%, hero shrinks, boss slides in from the right), a low rumble, the boss name card in Cinzel ("Oskar Venn · Warden of the Third Gate"). After the fight the camera eases back. All durations are data.
- The existing "animations on/off gives identical results" rule (M2) still holds: the camera and effects are presentation only.

## 4. Naming (CD-6)

- **Places:** "the [adjective] [landform]" from word lists by biome (Ashen / Drowned / Frozen / Hollow … + March / Fen / Pass / Reach / Deep …). A data setting `invented_place_share` (proposed 0.2) swaps the adjective for an invented name ("the Vael Marshes").
- **Invented names:** built from a curated syllable list per culture (harsh, short: Vael, Kha-, -rath, Venn, Dorr, Ysg-), checked against a blocklist, max 2 syllables for places, so nothing becomes gibberish.
- **Bosses:** "[Invented first name] [invented surname], [title]" for humanoid bosses; "[Title] of the [place]" for beasts and dragons ("the Maw of Kharath Deep").
- **Houses:** "House [invented name]", used for banners, prestige flavour and item names later.
- **Items:** keep the M3 type names, with optional house or place prefixes later (not in this scope).
- **Regions 1–5 (hand-written):** 1 **the Training Grounds** (garrison, CD-8), 2 the Ashen March (ash), 3 the Drowned Fen (moss), 4 the Frost Pass (frost), 5 the Vael Wastes (ochre). Kharath Deep (rust) is kept for region 6. The owner may rename any of them.
- **Region 1 people [Claude's call]:** the region 1 boss is a named recruit ("Bram Hollis, First of the Recruits"); the ON-1 training boss after subzone 1 is the instructor ("Sergeant Maera Dunn, Drillmaster"), which keeps "it cannot kill you" believable.
- The **game's title** stays a placeholder until M12 (D-042).

## 5. Sound (CD-7)

- Two music layers per biome: an **ambient layer** (wind, bells, a solo cello or piano motif) and a **boss layer** (drums, low brass) that crossfades in on boss arrival and out after the fight. Crossfade times are data.
- Effects: dull metal hits, bowstring, low spell hums, a deep horn for boss arrival, a soft chime for Rare+ drops.
- Sourcing: royalty-free libraries with licences that allow commercial mobile release, logged in a `Docs/CREDITS.md` from the first sound file [Claude's call]. A composer is an M12 option.
- Audio is optional to the rules: muting changes nothing in the simulation.

## 6. How it is built [Claude's call, approach chosen with CD-1]

| Approach considered | Verdict |
|---|---|
| **Shapes authored as SVG files in the repo, turned into PNG sprites by a Tools script, imported by Unity; background layers in greyscale and tinted at runtime from palette data** | **Chosen.** SVG is diff-able text Claude can write; PNGs need no extra Unity package; tinting makes every biome from one set of layers |
| Unity Vector Graphics package (SVG imported directly) | Rejected for now: still a preview package in Unity 6, uncertain on Android |
| Drawing shapes at runtime with UI Toolkit `Painter2D` | Rejected: every shape becomes code, harder to tweak and to hand over to an artist later |

- **Data:** new config sections (appended, D-032): `biomes` (palettes above), `depth` (darkening, floor), `naming` (invented share), `presentation` (camera pull-back zoom and timings, music crossfade). Region data gains `biome` and `name`. Game.Core stays free of colours; it only exposes the region's biome key, depth and name.
- **Where art lives:** `Art/Source/` (SVG, committed) → `Tools/ArtBuild/` script → `Assets/_Game/Art/` (PNG sprites, committed). Re-running the script reproduces the sprites exactly. The script uses a Python SVG rasterizer and is run by Claude; the PNGs are committed, so the owner's PC needs nothing new.

## 7. Scope and order (proposal)

1. **Creative Direction milestone (now, before M5):** the UI theme (iron and ember) across HUD, Bag, report and dev panel; the Fighter silhouette with idle and attack motion; the region 1 Training Grounds (garrison background, the 5 practice-target bodies with accents, the instructor training boss, the recruit boss with the camera pull-back); region names from data; placeholder ambient and boss sound layers with the crossfade. Ends with an owner playtest in the editor.
2. **M5:** Archer and Mage silhouettes, regions 2–5 (biomes, names, the 5 bodies of each family they introduce), the naming generator for later regions.
3. **M10:** the region generator uses biome × depth × family × naming data.

## 8. Left to the builder
Exact SVG shapes (5 bodies per family, accents) and animation curves, sprite resolutions per screen density, sound file selection within the direction above, the word and syllable lists (reviewed by the owner before shipping), USS class names.
