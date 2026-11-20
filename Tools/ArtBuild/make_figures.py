#!/usr/bin/env python3
"""Writes the person-shaped character SVGs in Art/Source/ from one shared anatomy (owner, CD playtest:
"more human, less rectangular"). Practice targets and scenery are hand-written SVGs and are not touched.

Every figure faces right, stands on y = 150 and is made of rig parts (see build_art.py):
banner, cloak, legs, torso, offhand, head, weapon/bow/staff. Builds widen the body around x = 50.
Run, then: python3 Tools/ArtBuild/build_art.py
"""
import re
from pathlib import Path

SRC = Path(__file__).resolve().parents[2] / "Art" / "Source"
K = "#07080B"
VIEWBOX = "12 2 94 150"
BUILDS = {"gaunt": 0.84, "lean": 1.0, "broad": 1.14, "huge": 1.32}


def mapx(d, k):
    """Widens a path around x = 50: every x coordinate (first number of each pair) is scaled by k."""
    out, nums, cmd = [], [], None
    for tok in re.findall(r"[A-Za-z]|-?\d+(?:\.\d+)?", d):
        if tok.isalpha():
            cmd = tok
            out.append(tok)
            nums = []
            continue
        nums.append(float(tok))
        if len(nums) == 2 and cmd and cmd.upper() in "MLCQST":
            x, y = nums
            out.append(f"{50 + (x - 50) * k:.2f} {y:.2f}")
            nums = []
    return " ".join(out)


def P(d, k=1.0):
    return f'<path d="{mapx(d, k)}"/>'


def line(x1, y1, x2, y2, w):
    return f'<line x1="{x1:.2f}" y1="{y1:.2f}" x2="{x2:.2f}" y2="{y2:.2f}" stroke-width="{w}"/>'


def circle(x, y, r):
    return f'<circle cx="{x:.2f}" cy="{y:.2f}" r="{r}"/>'


def part(name, pivot, shapes, glow=""):
    g = f'\n    <g class="glow" fill="#FFFFFF" stroke="#FFFFFF" stroke-linecap="round" stroke-width="0">{glow}</g>' if glow else ""
    return (f'  <g id="part:{name}" data-pivot="{pivot[0]:.2f} {pivot[1]:.2f}">\n'
            f'    <g class="shape" fill="{K}" stroke="{K}" stroke-linecap="round" stroke-linejoin="round" stroke-width="0">{shapes}</g>{g}\n  </g>\n')


# ---------------- anatomy (k = build width) ----------------
def legs(k):
    back = P("M43 90 C41 104 42 116 41 128 C40 134 40 140 39 146 L37 150 L47 150 L47 146 C47 138 48 132 48 126 C50 114 51 102 51 92 Z", k)
    front = P("M49 90 C51 102 55 114 55 126 C55 134 55 140 55 145 L54 150 L67 150 C67 148 64 146 61 145 C61 138 61 132 61 126 C61 112 60 100 59 90 Z", k)
    return back + front


def torso(k, pauldrons=False, back_arm=True):
    body = P("M37 52 C38 45 44 42 50 42 C57 42 62 44 64 50 C64 58 62 66 61 72 C60 78 59 84 60 92 L40 92 C41 84 40 78 39 72 C38 66 37 58 37 52 Z", k)
    arm = P("M40 49 C35 55 34 64 35 74 C35 78 36 81 37 83 L41 82 C40 76 40 70 41 64 C42 58 43 54 44 51 Z", k) if back_arm else ""
    pads = ""
    if pauldrons:
        pads = P("M33 53 C32 45 41 41 47 45 C44 49 39 53 33 53 Z", k) + P("M55 44 C61 41 69 44 68 53 C63 53 58 50 55 44 Z", k)
    return arm + body + pads


def robe(k, flare=1.0):
    return P(f"M39 58 C37 82 {38 - 8 * flare} 116 {34 - 10 * flare} 150 L{66 + 10 * flare} 150 C{62 + 8 * flare} 116 63 82 61 58 Z", k)


def cloak(k, kind):
    if kind == "long":
        return P("M39 47 C31 66 29 96 30 138 C34 134 37 141 42 137 C46 142 49 135 53 139 C55 124 55 100 56 80 C57 66 59 56 61 47 Z", k)
    if kind == "torn":
        return P("M39 47 C32 64 30 88 31 116 L35 110 L37 122 L41 113 L44 126 L47 114 L51 120 C53 100 55 80 57 66 C58 58 60 52 61 47 Z", k)
    if kind == "cape":
        return P("M41 46 C33 62 27 92 22 130 C30 126 33 134 39 128 C43 134 47 126 50 130 C52 110 54 86 56 66 C57 58 59 51 60 46 Z", k)
    return ""


def head(k, kind):
    x = lambda v: 50 + (v - 50) * k
    if kind == "hood":       # soft rounded hood with a fall at the back
        shape = P("M41 42 C38 34 39 22 47 17 C53 14 60 17 62 25 C63 30 63 35 62 40 C60 44 54 46 48 45 C45 45 42 44 41 42 Z", k) + P("M41 34 C36 38 35 44 38 48 L44 46 Z", k)
        eyes = line(x(55), 31, x(60.5), 31, 1.6)
    elif kind == "deep_hood":  # pointed back, deep opening
        shape = P("M40 43 C37 34 37 24 34 14 C42 15 50 14 57 18 C62 22 63 30 62 40 C59 44 53 46 47 45 C44 45 41 44 40 43 Z", k)
        eyes = line(x(55), 32, x(60.5), 32, 1.6)
    elif kind == "plain_hood":
        shape = P("M42 43 C39 35 40 24 47 19 C53 16 59 19 61 26 C62 31 62 36 61 41 C58 44 53 45 48 45 C45 45 43 44 42 43 Z", k)
        eyes = line(x(55), 31, x(60), 31, 1.4)
    elif kind == "cowl":     # tall wizard cowl leaning back
        shape = P("M41 44 C40 36 42 30 44 22 C45 14 42 6 38 2 C48 6 56 14 60 24 C62 30 63 36 62 42 C59 45 53 46 48 46 C45 46 42 45 41 44 Z", k)
        eyes = line(x(54), 34, x(59.5), 34, 1.6)
    elif kind == "cult_hood":
        shape = P("M41 44 C40 34 42 24 48 12 C50 9 52 9 54 12 C59 22 62 32 62 42 C59 45 53 46 48 46 C45 46 42 45 41 44 Z", k)
        eyes = circle(x(53.5), 31, 1.5) + circle(x(58.5), 31, 1.5)
    elif kind == "helm":     # rounded helm with a crest
        shape = P("M41 44 C40 36 40 28 43 23 C46 18 55 18 58 23 C61 28 61 36 60 44 C56 46 46 46 41 44 Z", k) + P("M47 19 C49 13 53 11 56 12 C54 15 53 18 52 20 Z", k)
        eyes = circle(x(53.5), 31, 1.5) + circle(x(58), 31, 1.5)
    elif kind == "bare":     # heavy bald head and thick neck
        shape = P("M42 44 C40 38 40 30 43 25 C46 20 55 20 58 25 C61 30 61 38 59 44 C55 47 46 47 42 44 Z", k)
        eyes = circle(x(53.5), 32, 1.5) + circle(x(57.8), 32, 1.5)
    elif kind == "horned":   # great helm with horns
        shape = (P("M40 45 C39 36 39 28 42 22 C46 16 55 16 58 22 C61 28 62 36 61 45 C56 47 45 47 40 45 Z", k)
                 + P("M42 26 C36 22 33 14 34 6 C37 12 41 17 46 21 Z", k) + P("M57 24 C62 19 64 12 63 5 C66 11 66 19 61 27 Z", k))
        eyes = line(x(52), 31, x(55.5), 30, 1.6) + line(x(56.5), 30, x(60), 31, 1.6)
    else:
        raise ValueError(kind)
    neck = P("M46 42 L55 42 L55 47 L46 47 Z", k)
    return neck + shape, eyes


SHOULDER = (60, 49)


def arm(k, pose):
    """Weapon arm from the shoulder; returns (shapes, hand x, hand y) in canvas units."""
    x = lambda v: 50 + (v - 50) * k
    if pose == "down":       # hand at the hip, weapon held upright
        d = "M57 46 C63 45 66 50 66 56 C67 62 67 67 68 71 C69 75 71 78 73 80 L69 84 C66 81 64 77 62 73 C60 67 59 61 58 56 Z"
        hand = (x(71.5), 82)
    elif pose == "forward":  # arm out in front (bow, orb)
        d = "M57 46 C63 44 68 48 70 52 C73 55 77 57 81 58 L80 63 C75 63 70 61 66 59 C62 57 59 54 58 51 Z"
        hand = (x(81.5), 60.5)
    elif pose == "raised":   # hand up and forward (staff, orb)
        d = "M57 46 C63 44 67 47 69 51 C71 55 72 60 74 64 L70 67 C67 63 65 58 63 55 C61 52 59 50 58 49 Z"
        hand = (x(72.5), 66)
    else:
        raise ValueError(pose)
    return P(d, k) + circle(hand[0], hand[1], 3.4), hand


def weapon(kind, hand):
    hx, hy = hand
    if kind == "greatsword":
        return (line(hx - 4, hy + 14, hx + 18, hy - 70, 4.6) + line(hx - 7, hy + 3, hx + 6, hy + 7, 2.6)
                + circle(hx - 4.6, hy + 15.5, 2.2), line(hx + 15.5, hy - 61, hx + 18, hy - 70, 3))
    if kind == "sword":
        return (line(hx - 3, hy + 9, hx + 20, hy - 28, 3.2) + line(hx - 5, hy + 1, hx + 5, hy + 6, 2.2), "")
    if kind == "training_sword":
        return (line(hx - 3, hy + 8, hx + 17, hy - 24, 4.2) + line(hx - 5, hy + 1, hx + 5, hy + 5, 2.4), line(hx + 15, hy - 21, hx + 17, hy - 24, 2))
    if kind == "spear":
        return (line(hx - 22, hy + 62, hx + 22, hy - 54, 2.6), P(f"M{hx + 21} {hy - 52} L{hx + 27} {hy - 66} L{hx + 18} {hy - 56} Z"))
    if kind == "maul":
        return (line(hx - 4, hy + 16, hx + 14, hy - 48, 4.2)
                + f'<rect x="{hx + 4:.2f}" y="{hy - 62:.2f}" width="22" height="15" rx="2.5" transform="rotate(18 {hx + 15:.2f} {hy - 54:.2f})"/>', "")
    if kind == "staff":
        return (line(hx + 2, hy - 54, hx - 3, hy + 84, 3), circle(hx + 2.4, hy - 58, 5.4))
    if kind == "quarterstaff":
        return (line(hx + 4, hy - 58, hx - 2, hy + 68, 3.6), "")
    if kind == "orb":
        return ("", circle(hx + 4, hy - 6, 6.2))
    if kind == "bow":
        return (f'<path d="M{hx + 3:.2f} {hy - 40:.2f} C{hx + 17:.2f} {hy - 20:.2f} {hx + 17:.2f} {hy + 20:.2f} {hx + 3:.2f} {hy + 40:.2f}" fill="none" stroke-width="2.8"/>',
                line(hx + 3, hy - 40, hx + 3, hy + 40, 0.8))
    raise ValueError(kind)


def figure(name, height, build, head_kind, weapon_kind, pose, cloak_kind="", robe_flare=None,
           pauldrons=False, shield=False, banner=False, quiver=False, cord=False):
    k = BUILDS[build]
    parts = []
    if banner:
        parts.append(part("banner", (19, 150), line(19, 150, 19, 4, 2.4) + P("M19 7 C28 9 36 8 44 10 L39 22 L44 34 C36 33 28 34 19 32 Z")))
    if cloak_kind:
        parts.append(part("cloak", (50, 46), cloak(k, cloak_kind)))
    if robe_flare is None:
        parts.append(part("legs", (50, 92), legs(k)))
    t = torso(k, pauldrons)
    if robe_flare is not None:
        t = robe(k, robe_flare) + t
    if quiver:
        t = P("M33 46 L38 44 L44 80 L39 82 Z", k) + t
    tglow = ""
    if cord:
        tglow = f'<path d="M{50 + (45 - 50) * k:.2f} 50 C47 60 54 62 56 52" fill="none" stroke-width="1"/>' + circle(56, 53, 1.4)
    parts.append(part("torso", (50, 92), t, tglow))
    if shield:
        parts.append(part("offhand", (42, 58), P("M30 60 C30 54 44 52 50 56 C51 68 47 80 40 86 C33 80 30 70 30 60 Z", k)))
    hshape, eyes = head(k, head_kind)
    parts.append(part("head", (50, 45), hshape, eyes))
    arm_shape, hand = arm(k, pose)
    w_shape, w_glow = weapon(weapon_kind, hand)
    pid = {"bow": "bow", "staff": "staff", "orb": "staff"}.get(weapon_kind, "weapon")
    if weapon_kind in ("staff", "quarterstaff", "bow"):
        shapes = w_shape + arm_shape   # the hand grips in front of the shaft
    else:
        shapes = arm_shape + w_shape
    parts.append(part(pid, (50 + (SHOULDER[0] - 50) * k, SHOULDER[1]), shapes, w_glow))
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{VIEWBOX}" data-height="{height}">\n' + "".join(parts) + "</svg>\n"
    (SRC / f"{name}.svg").write_text(svg)


FIGURES = [
    # heroes
    dict(name="fighter", height=512, build="broad", head_kind="hood", weapon_kind="greatsword", pose="down", cloak_kind="long", pauldrons=True),
    dict(name="archer_hero", height=512, build="lean", head_kind="deep_hood", weapon_kind="bow", pose="forward", cloak_kind="cape", quiver=True),
    dict(name="mage", height=512, build="lean", head_kind="cowl", weapon_kind="staff", pose="raised", robe_flare=1.0, cloak_kind="long"),
    # region 1 people
    dict(name="recruit", height=1024, build="lean", head_kind="plain_hood", weapon_kind="training_sword", pose="down"),
    dict(name="instructor", height=512, build="broad", head_kind="plain_hood", weapon_kind="quarterstaff", pose="raised", robe_flare=0.4, cord=True),
    # humanoids
    dict(name="deserter", height=512, build="lean", head_kind="hood", weapon_kind="sword", pose="down", cloak_kind="torn"),
    dict(name="spearman", height=512, build="lean", head_kind="helm", weapon_kind="spear", pose="down", shield=True),
    dict(name="cultist", height=512, build="lean", head_kind="cult_hood", weapon_kind="orb", pose="forward", robe_flare=0.8),
    dict(name="archer", height=512, build="lean", head_kind="hood", weapon_kind="bow", pose="forward", quiver=True),
    dict(name="brute", height=512, build="huge", head_kind="bare", weapon_kind="maul", pose="down"),
    # wizards (M5-6): robed human casters of Light (glow tinted by the game)
    dict(name="battle_magus", height=512, build="broad", head_kind="plain_hood", weapon_kind="quarterstaff", pose="raised", robe_flare=0.4, pauldrons=True),
    dict(name="lancer_adept", height=512, build="lean", head_kind="helm", weapon_kind="spear", pose="down", robe_flare=0.5),
    dict(name="lightcaller", height=512, build="lean", head_kind="cowl", weapon_kind="orb", pose="raised", robe_flare=1.0, cloak_kind="long"),
    dict(name="warding_monk", height=512, build="broad", head_kind="bare", weapon_kind="quarterstaff", pose="down", robe_flare=0.5, shield=True),
    dict(name="hierophant", height=512, build="lean", head_kind="cult_hood", weapon_kind="staff", pose="raised", robe_flare=1.0, cloak_kind="cape"),
    # undead people (M5): gaunt bodies, torn cloth
    dict(name="wight", height=512, build="gaunt", head_kind="deep_hood", weapon_kind="orb", pose="forward", robe_flare=0.6, cloak_kind="torn"),
    dict(name="ghoul", height=512, build="gaunt", head_kind="bare", weapon_kind="sword", pose="down", cloak_kind="torn"),
    dict(name="grave_priest", height=512, build="gaunt", head_kind="cult_hood", weapon_kind="staff", pose="raised", robe_flare=0.7),
    dict(name="bone_seer", height=512, build="gaunt", head_kind="cowl", weapon_kind="orb", pose="raised", robe_flare=0.9),
    dict(name="lich_acolyte", height=512, build="gaunt", head_kind="horned", weapon_kind="staff", pose="raised", robe_flare=0.8, cloak_kind="long"),
    dict(name="hollow_lord", height=1024, build="broad", head_kind="horned", weapon_kind="staff", pose="raised", robe_flare=1.0, cloak_kind="long", pauldrons=True, banner=True),
    dict(name="humanoid_boss", height=1024, build="huge", head_kind="horned", weapon_kind="greatsword", pose="down", cloak_kind="long", pauldrons=True, banner=True),
]

if __name__ == "__main__":
    for f in FIGURES:
        figure(**f)
    print(f"wrote {len(FIGURES)} figures into {SRC.relative_to(SRC.parents[1])}")
