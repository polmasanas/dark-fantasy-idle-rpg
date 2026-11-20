#!/usr/bin/env python3
"""Renders Art/Source/*.svg into Assets/_Game/Resources/Art/*.png (CREATIVE_DIRECTION.md §6).

Each SVG has a <g id="body"> and optionally a <g id="glow">:
  <name>.png       everything except the glow group (black silhouettes, or white scenery to be tinted)
  <name>_glow.png  only the glow group (white, tinted at runtime by class / element / region accent)
The root's data-height attribute sets the output height in pixels; width follows the viewBox.

  python3 Tools/ArtBuild/build_art.py           render all
  python3 Tools/ArtBuild/build_art.py --check   exit 1 if any committed PNG is missing or differs
"""
import io
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

import cairosvg
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Art" / "Source"
OUT = ROOT / "Assets" / "_Game" / "Resources" / "Art"
SVG_NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", SVG_NS)

# A pixel may differ by this much per channel (cairo builds vary slightly between machines).
CHANNEL_TOLERANCE = 8
# ...and at most this share of pixels may exceed it.
MAX_DIFF_SHARE = 0.002


def variants(svg_path):
    """Yields (output name, svg bytes) for the body and, if present, the glow."""
    tree = ET.parse(svg_path)
    root = tree.getroot()
    groups = {g.get("id"): g for g in root.findall(f"{{{SVG_NS}}}g")}
    name = svg_path.stem
    outs = [(name, "glow")]
    if "glow" in groups:
        outs.append((name + "_glow", "body"))
    for out_name, drop in outs:
        copy = ET.fromstring(ET.tostring(root))
        for g in copy.findall(f"{{{SVG_NS}}}g"):
            if g.get("id") == drop:
                copy.remove(g)
        yield out_name, ET.tostring(copy), int(root.get("data-height", "512")), root.get("viewBox")


def render(svg_bytes, height, viewbox):
    _, _, w, h = (float(v) for v in viewbox.split())
    width = max(1, round(height * w / h))
    return cairosvg.svg2png(bytestring=svg_bytes, output_width=width, output_height=height)


def build(target):
    target.mkdir(parents=True, exist_ok=True)
    names = []
    for svg in sorted(SRC.glob("*.svg")):
        for out_name, data, height, viewbox in variants(svg):
            (target / (out_name + ".png")).write_bytes(render(data, height, viewbox))
            names.append(out_name)
    return names


def same(a_path, b_path):
    try:
        a = Image.open(a_path).convert("RGBA")
        b = Image.open(b_path).convert("RGBA")
    except (OSError, ValueError):
        # Not an image: e.g. a Git LFS pointer when LFS files were not fetched.
        return False
    if a.size != b.size:
        return False
    r, g, bl, al = ImageChops.difference(a, b).split()
    worst = ImageChops.lighter(ImageChops.lighter(r, g), ImageChops.lighter(bl, al))
    bad = worst.point(lambda v: 255 if v > CHANNEL_TOLERANCE else 0).histogram()[255]
    return bad <= MAX_DIFF_SHARE * a.size[0] * a.size[1]


def main(argv):
    if "--check" not in argv:
        names = build(OUT)
        print(f"rendered {len(names)} PNGs into {OUT.relative_to(ROOT)}")
        return 0
    with tempfile.TemporaryDirectory() as tmp:
        names = build(Path(tmp))
        stale = [n for n in names if not (OUT / (n + ".png")).exists() or not same(Path(tmp) / (n + ".png"), OUT / (n + ".png"))]
    extra = sorted(p.stem for p in OUT.glob("*.png") if p.stem not in names)
    for n in stale:
        print(f"stale or missing: {n}.png")
    for n in extra:
        print(f"no source SVG for: {n}.png")
    if stale or extra:
        print("Run: python3 Tools/ArtBuild/build_art.py")
        return 1
    print(f"art up to date ({len(names)} PNGs)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
