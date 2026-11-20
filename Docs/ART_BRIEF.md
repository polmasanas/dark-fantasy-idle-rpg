# ART_BRIEF.md: commissioning the game's art

> **For:** a 2D artist (or studio) replacing Claude's placeholder silhouettes. The owner sends this brief with the reference pack (§8). Direction approved in `CREATIVE_DIRECTION.md` (CD-0…CD-11); this brief must not contradict it.

## 1. The game in three lines
A dark fantasy **idle RPG for Android phones** (portrait). Three heroes, wardens of the last walled kingdom, fight automatically through a wilderness that grows darker the further they go. The player watches, equips loot and makes build choices; most fights are seen many times and at small size.

## 2. The mood
- **Grim and believable**, like a medieval war chronicle: banners, walls, winter coming. Victories cost something.
- **Scale and dread:** small humans against enormous threats (bosses grow from 1.5× to 3× a normal enemy).
- **Quiet and melancholic:** muted layered backgrounds; colour appears only as small glows (eyes, weapon edges, magic, loot).
- **Not wanted:** bright or cute cartoon looks, anime styling, gore, or anything that copies an existing game's characters, creatures or places.

## 3. Style rules
| Rule | Detail |
|---|---|
| Characters are **solid black silhouettes** (`#07080B`) | Detail comes from the outline only; no interior shading or lines |
| **Glow is the only colour on characters** | Eye-slits, weapon edges, orbs. Drawn in white on separate layers; the game tints them (class, element, rarity) |
| **Readable at about 1/6 of a phone screen** | Each class and enemy type must be recognisable from its outline alone |
| Heroes: **cloaked wardens**, lean, about 1 : 5 head-to-body | One big shape names each class: Fighter = broad pauldrons + huge blade; Archer = deep hood + long cape + bow; Mage = tall pointed cowl + long robe + staff |
| Backgrounds: **flat layers**, drawn in **white/greyscale** | The game tints each layer per region (biome × depth), so one set of shapes serves many regions |
| Facing | All characters **face right**; the game mirrors enemies |

## 4. How files must be delivered (important)
The game animates characters **part by part**, so every character is a layered file with named parts.

1. **One layer per part**, drawn complete (fill in what other parts hide) on the **same full canvas**, in back-to-front order. Part names (use exactly these):
   - people: `cloak`, `legs`, `torso`, `head`, `offhand`, `weapon` (or `bow` / `staff`), `banner`
   - practice targets: `base` (still), `target` (moves)
2. **Glow layers** in white on transparent, one per part that glows: `head_glow`, `weapon_glow`, …
3. **A pivot per part:** the point it rotates around (neck for the head, shoulder for the weapon arm, hips for the torso, top of the cloak). Mark it with a small dot on a layer named `pivot_<part>`, or list the coordinates.
4. **Format:** layered **SVG** preferred (it drops straight into our build tool); otherwise layered **PSD or Krita** at least **2048 px tall**, plus transparent PNG exports of every layer.
5. **Canvas:** characters about 1 : 1.6 (width : height) for heroes and normal enemies, taller for bosses; leave room for the weapon swing. Backgrounds **2048 × 1024**, left and right edges that join when tiled.
6. **UI elements:** layered source plus PNGs; panel frames as **9-slice** pieces (corners stay sharp when stretched).

## 5. What to make
**Phase 1: replace today's placeholders** (one batch, after the style test in §6)

| # | Item | Count |
|---|---|---|
| 1 | Heroes: Fighter, Archer, Mage (parts + glow + pivots) | 3 |
| 2 | Region 1 people: Bram Hollis (recruit boss), Maera Dunn (drillmaster) | 2 |
| 3 | Practice targets: straw dummy, target post, sparring frame, shield board, weighted sack | 5 |
| 4 | Humanoid enemies: deserter, spearman, cultist, archer, brute | 5 |
| 5 | Humanoid warlord (boss of regions 2+), with banner | 1 |
| 6 | Backgrounds: garrison (walls + training yard) and far/near layers for ash fields, moss fen, frost pass, ochre waste, rust hollow | 6 sets |
| 7 | UI kit: panel frame + title panel with corner marks (9-slice), button (normal / pressed / disabled), toggle, 11 equipment-slot icons, 6 rarity frames, currency icons (gold, materials, gems, dust), 9 attribute icons, 5 element icons | 1 kit |

**Phase 2: later milestones** (quote separately): five body types each for Beasts, Undead, Demons, Elementals, Constructs and Dragons; one boss per family; item icons; pets.

## 6. Process
1. **Paid style test first:** the Fighter (with parts), one humanoid enemy and the ash-fields background. We test them in the game before going further.
2. **Then phase 1 in batches** (heroes and region 1 → enemies → backgrounds → UI), with one round of feedback per batch.
3. Work-in-progress as silhouettes or thumbnails first; parts and pivots only after the outline is approved.

## 7. Rights and terms
- Full, exclusive, worldwide commercial rights for the game and its marketing, including the source files.
- Original work only: no traced or copied material from other games. If any AI tool is used, say so and say which one.
- Credit in the game's credits screen (name as the artist wishes).
- Please quote **per item or per batch**, with delivery dates per batch.

## 8. Reference pack to send with this brief
- The **contact sheet** of the current placeholder silhouettes in idle, attack and hit poses.
- `Art/Source/*.svg`: the placeholder parts and pivots (shows the exact part structure we need).
- The colour palette (`CREATIVE_DIRECTION.md` §3.3) and a few in-game screenshots.

## 9. Using an AI image tool instead
AI image tools are useful for **concept art and mood boards**, not for the final game files:
- They return one flat image: no named part layers and no pivots, so every character would still need to be cut into parts by hand.
- They struggle to keep dozens of characters consistent in one style.
- Licences differ per tool: check that commercial use in a published game is allowed. Treat the output as reference for an artist (or for Claude's placeholders), not as shipped art.

### Prompts (adapt the bracketed parts)
Attach the contact sheet as a style reference when the tool allows it, and keep the wording the same between characters so the style stays consistent.

**Character**
> 2D game character, full body, side view facing right, a single solid flat black silhouette with no interior detail or shading, [a lean hooded warden in a long tattered cloak, broad armoured pauldrons, holding a huge two-handed greatsword raised over the shoulder], a thin glowing [amber] eye-slit as the only light, faint [amber] glow along the outer edge only, clean vector shapes, readable at small size, grim melancholic dark fantasy, plain muted blue-grey background, centred, no text

Avoid: anime, chibi, cute, bright colours, gradients or highlights on the figure, interior lines, realistic rendering, gore, text, watermark, frame

**Background layer**
> 2D side-scrolling game background, wide panorama 2:1, flat layered silhouettes with no texture, [a distant fortified curtain wall with square towers and banners] in the far layer and [a training yard fence and weapon racks] in the near layer, muted [warm grey-brown] palette, misty, melancholic dark fantasy, empty ground strip at the bottom, no characters, no text

**Interface**
> mobile game UI kit on a dark background, dark slate panels with a bevelled iron border and small ember-gold corner brackets, iron buttons with carved Roman-capital labels, equipment slot icons as simple white line icons, flat, clean, high contrast, dark fantasy, no text except labels
