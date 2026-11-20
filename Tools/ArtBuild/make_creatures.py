#!/usr/bin/env python3
"""Writes the non-human enemy SVGs (beasts and elementals, M5) into Art/Source/ as rigged parts.

Same conventions as make_figures.py: facing right, standing on (or hovering above) y = 150, parts drawn
back to front, black shapes with white glow groups (the game tints glows by element / region accent).
Part ids: tail, legs, body, head, jaw, core, weapon (motions in look_config.json).
Run, then: python3 Tools/ArtBuild/build_art.py
"""
from pathlib import Path

SRC = Path(__file__).resolve().parents[2] / "Art" / "Source"
K = "#07080B"
VIEWBOX = "0 0 160 150"


def P(d):
    return f'<path d="{d}"/>'


def C(x, y, r):
    return f'<circle cx="{x}" cy="{y}" r="{r}"/>'


def part(name, pivot, shapes, glow=""):
    g = f'\n    <g class="glow" fill="#FFFFFF" stroke="#FFFFFF" stroke-linecap="round" stroke-width="0">{glow}</g>' if glow else ""
    return (f'  <g id="part:{name}" data-pivot="{pivot[0]} {pivot[1]}">\n'
            f'    <g class="shape" fill="{K}" stroke="{K}" stroke-linejoin="round" stroke-width="0">{shapes}</g>{g}\n  </g>\n')


def write(name, parts, height=512, viewbox=VIEWBOX):
    (SRC / f"{name}.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{viewbox}" data-height="{height}">\n' + "".join(parts) + "</svg>\n")


def leg(x, top, width=7, bend=3):
    """One tapering leg with a paw, from the body (top) to the ground."""
    return P(f"M{x} {top} L{x + width} {top} C{x + width + bend} {top + 18} {x + width - 1} {top + 30} {x + width} 146 "
             f"L{x + width + 4} 150 L{x - 2} 150 C{x} {top + 30} {x - bend} {top + 18} {x} {top} Z")


# ---------------- beasts (Drowned Fen) ----------------
def fen_wolf(name="fen_wolf", scale_note=False, height=512, mane=False):
    tail = part("tail", (34, 98), P("M38 96 C26 90 14 92 6 104 C16 102 24 104 36 106 Z"))
    legs = part("legs", (70, 108), leg(40, 104) + leg(50, 106) + leg(98, 102) + leg(108, 100))
    body = P("M34 92 C40 80 70 76 98 82 C110 85 116 96 112 108 C100 116 60 118 40 112 C30 108 28 100 34 92 Z")
    if mane:
        body += P("M84 80 L88 64 L94 78 L100 62 L104 80 L110 68 L110 86 Z")
    body_p = part("body", (72, 110), body)
    head = part("head", (108, 88),
                P("M100 84 C106 74 114 70 122 72 L124 60 L130 72 C138 74 148 80 154 88 C150 93 140 94 130 93 C124 99 114 100 104 96 Z"),
                C(132, 80, 2.2))
    write(name, [tail, legs, body_p, head], height)


def bog_boar():
    tail = part("tail", (30, 100), P("M32 98 C26 94 22 96 20 102 C24 100 28 102 32 104 Z"))
    legs = part("legs", (70, 116), leg(38, 116, 9) + leg(50, 118, 9) + leg(96, 116, 9) + leg(108, 114, 9))
    body = part("body", (70, 120), P("M30 100 C34 80 60 70 90 74 C112 76 124 90 122 108 C118 122 90 126 56 124 C38 122 28 114 30 100 Z")
                + P("M60 74 L64 66 L68 74 L74 64 L78 74 L84 66 L86 76 Z"))
    head = part("head", (116, 96), P("M112 84 C122 80 134 84 142 92 C148 96 150 104 146 108 C138 112 126 112 116 108 Z")
                + P("M124 84 L128 76 L132 86 Z"), C(132, 92, 2.0) + P("M144 106 C150 102 152 96 150 92 L146 100 Z"))
    write("bog_boar", [tail, legs, body, head])


def spitting_toad():
    legs = part("legs", (70, 130), P("M36 128 C30 110 46 100 60 108 C64 118 56 130 66 146 L72 150 L40 150 C38 142 38 136 36 128 Z")
                + P("M96 120 L104 120 L106 146 L110 150 L94 150 L98 140 Z"))
    body = part("body", (72, 128), P("M34 120 C34 96 60 84 90 86 C112 88 122 102 120 118 C118 132 96 140 70 140 C48 140 34 134 34 120 Z"))
    head = part("head", (106, 104), P("M98 92 C106 82 120 82 128 90 C134 96 134 104 128 110 C120 114 106 114 98 108 Z")
                + P("M104 88 C106 82 112 80 116 84 Z"),
                C(116, 92, 2.2) + P("M106 110 C112 120 124 120 128 112 C120 114 112 114 106 110 Z"))
    write("spitting_toad", [legs, body, head])


def marsh_stalker():
    tail = part("tail", (40, 116), P("M42 112 C24 112 12 120 2 132 C14 130 26 126 44 124 Z"))
    legs = part("legs", (80, 124), leg(50, 122, 6, 6) + leg(62, 124, 6, 6) + leg(100, 122, 6, 6) + leg(112, 120, 6, 6))
    body = part("body", (80, 126), P("M40 114 C50 102 84 98 112 104 C124 106 130 114 126 122 C112 130 72 132 48 128 C38 126 34 120 40 114 Z")
                + P("M60 102 L64 94 L68 102 L74 92 L78 102 L84 94 L88 104 Z"))
    head = part("head", (122, 112), P("M118 104 C128 100 142 102 156 108 C150 112 140 114 128 116 C124 116 120 114 118 110 Z"),
                C(134, 106, 1.8))
    jaw = part("jaw", (122, 114), P("M120 114 C132 116 144 116 154 114 C146 120 134 122 122 120 Z"))
    write("marsh_stalker", [tail, legs, body, jaw, head])


def mire_bear():
    legs = part("legs", (74, 112), leg(38, 110, 12) + leg(54, 112, 12) + leg(92, 110, 12) + leg(106, 108, 12))
    body = part("body", (74, 116), P("M30 108 C26 84 44 62 74 58 C104 56 122 72 122 96 C122 112 108 122 76 122 C48 122 32 118 30 108 Z"))
    head = part("head", (112, 82), P("M106 70 C112 62 126 62 134 70 L146 78 C146 86 138 92 126 92 C116 92 108 86 106 78 Z")
                + C(112, 64, 4) + C(122, 62, 4), C(130, 74, 2.0))
    write("mire_bear", [legs, body, head])


# ---------------- elementals (Frost Pass) ----------------
def crystal(points):
    return P("M" + " L".join(f"{x} {y}" for x, y in points) + " Z")


def shard_thrower():
    body = part("body", (70, 120), crystal([(52, 146), (48, 110), (56, 70), (70, 58), (84, 70), (90, 110), (86, 146)]))
    core = part("core", (70, 96), crystal([(64, 96), (70, 84), (76, 96), (70, 108)]), C(70, 96, 5))
    head = part("head", (70, 60), crystal([(58, 62), (62, 40), (68, 52), (72, 34), (76, 52), (82, 42), (84, 62)]), C(66, 54, 1.6) + C(75, 54, 1.6))
    weapon = part("weapon", (84, 72), crystal([(82, 70), (104, 60), (108, 64), (88, 78)]) + crystal([(104, 52), (124, 44), (112, 62)]),
                  crystal([(112, 50), (122, 46), (114, 56)]))
    write("shard_thrower", [body, core, head, weapon])


def hail_spitter():
    body = part("body", (72, 96), crystal([(40, 100), (50, 72), (72, 62), (96, 70), (106, 96), (96, 120), (70, 128), (48, 120)])
                + crystal([(58, 128), (62, 140), (66, 128)]) + crystal([(78, 128), (82, 142), (86, 128)]))
    core = part("core", (74, 96), C(74, 96, 9), C(74, 96, 6))
    jaw = part("jaw", (100, 98), crystal([(98, 92), (122, 88), (118, 98), (100, 104)]))
    write("hail_spitter", [body, core, jaw])


def frost_wisp():
    tail = part("tail", (70, 116), P("M62 112 C54 126 60 138 52 148 C66 140 72 130 78 114 Z"))
    body = part("body", (70, 100), P("M70 52 C80 70 92 82 90 100 C88 114 78 120 70 120 C60 120 50 112 50 98 C50 82 64 74 70 52 Z"))
    core = part("core", (70, 100), C(70, 100, 7), C(70, 100, 7) + C(64, 90, 1.6) + C(76, 90, 1.6))
    write("frost_wisp", [tail, body, core])


def rime_lancer():
    body = part("body", (66, 120), crystal([(50, 150), (54, 104), (60, 72), (68, 64), (76, 72), (80, 104), (84, 150)]))
    head = part("head", (68, 66), crystal([(58, 66), (64, 44), (68, 36), (72, 44), (78, 66)]), C(65, 58, 1.5) + C(72, 58, 1.5))
    weapon = part("weapon", (76, 78), crystal([(74, 76), (100, 82), (98, 88), (74, 84)]) + crystal([(96, 80), (150, 72), (98, 90)]),
                  crystal([(132, 75), (150, 72), (134, 79)]))
    write("rime_lancer", [body, head, weapon])


def ice_brute():
    legs = part("legs", (72, 120), crystal([(46, 150), (48, 118), (66, 116), (64, 150)]) + crystal([(80, 150), (80, 116), (98, 118), (100, 150)]))
    body = part("body", (72, 120), crystal([(38, 120), (34, 80), (48, 62), (96, 60), (110, 80), (106, 120)]))
    core = part("core", (72, 92), crystal([(66, 92), (72, 82), (78, 92), (72, 102)]), C(72, 92, 5))
    head = part("head", (72, 62), crystal([(60, 64), (62, 46), (82, 46), (84, 64)]), C(67, 55, 1.8) + C(77, 55, 1.8))
    weapon = part("weapon", (100, 70), crystal([(98, 66), (114, 74), (122, 104), (108, 110), (100, 84)]))
    write("ice_brute", [legs, body, core, head, weapon])


def frozen_oath():
    legs = part("legs", (80, 112), crystal([(52, 150), (56, 108), (76, 106), (72, 150)]) + crystal([(88, 150), (86, 106), (106, 108), (110, 150)]))
    body = part("body", (80, 112), crystal([(40, 112), (36, 64), (56, 40), (104, 40), (124, 64), (120, 112)]))
    core = part("core", (80, 78), crystal([(72, 78), (80, 62), (88, 78), (80, 94)]), C(80, 78, 7))
    head = part("head", (80, 40), crystal([(64, 42), (64, 22), (70, 30), (74, 8), (80, 26), (86, 6), (90, 30), (96, 22), (96, 42)]),
                C(74, 34, 2) + C(86, 34, 2))
    weapon = part("weapon", (116, 52), crystal([(114, 48), (128, 56), (134, 70), (120, 66)]) + crystal([(128, 60), (158, 4), (140, 66)]),
                  crystal([(150, 18), (158, 4), (152, 24)]))
    write("frozen_oath", [legs, body, core, head, weapon], height=1024)


if __name__ == "__main__":
    SRC.mkdir(parents=True, exist_ok=True)
    fen_wolf()
    fen_wolf("fen_mother", height=1024, mane=True)
    bog_boar(); spitting_toad(); marsh_stalker(); mire_bear()
    shard_thrower(); hail_spitter(); frost_wisp(); rime_lancer(); ice_brute(); frozen_oath()
    print("wrote 12 creatures into Art/Source")
