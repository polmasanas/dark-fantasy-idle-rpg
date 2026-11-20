#!/usr/bin/env python3
"""Renders Art/Source/*.svg into Assets/_Game/Resources/Art/*.png (CREATIVE_DIRECTION.md §6).

Scenery SVGs have a <g id="body"> (and optionally a <g id="glow">) and give:
  <name>.png       everything except the glow group (white, tinted at runtime)
Character SVGs are made of parts, back to front: <g id="part:NAME" data-pivot="x y"> holding a
<g class="shape"> (black) and optionally a <g class="glow"> (white). They give:
  <name>.png, <name>_glow.png     the whole character (fallback, contact sheets)
  <name>__<part>.png (+ _glow)    each part on the full canvas, so parts stack without offsets
  <name>__<part>_aura.png         a soft blurred copy of the part's outline (its halo), on a canvas padded
                                  by aura_pad (share of the height) on every side, so the glow is not cut
  rigs.json                       per character: aspect, aura_pad and the parts in draw order with pivots (0..1)
The root's data-height attribute sets the output height in pixels; width follows the viewBox.

  python3 Tools/ArtBuild/build_art.py           render all
  python3 Tools/ArtBuild/build_art.py --check   exit 1 if any committed PNG is missing or differs
"""
import io
import json
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

import cairosvg
from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Art" / "Source"
OUT = ROOT / "Assets" / "_Game" / "Resources" / "Art"
SVG_NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", SVG_NS)

# A pixel may differ by this much per channel (cairo builds vary slightly between machines).
CHANNEL_TOLERANCE = 8
# ...and at most this share of pixels may exceed it.
MAX_DIFF_SHARE = 0.002


def _parts(root):
    return [g for g in root.findall(f"{{{SVG_NS}}}g") if (g.get("id") or "").startswith("part:")]


def _filtered(root, keep_part=None, keep_class="shape"):
    """Copy of the SVG with only one part (or all parts) and only its shape or glow groups."""
    copy = ET.fromstring(ET.tostring(root))
    for g in _parts(copy):
        if keep_part is not None and g.get("id") != "part:" + keep_part:
            copy.remove(g)
            continue
        for inner in list(g):
            if inner.get("class") != keep_class:
                g.remove(inner)
    return ET.tostring(copy)


def variants(svg_path):
    """Yields (output name, svg bytes, height, viewBox) for every PNG an SVG produces, plus its rig (or None)."""
    root = ET.parse(svg_path).getroot()
    name = svg_path.stem
    height = int(root.get("data-height", "512"))
    vb = root.get("viewBox")
    parts = _parts(root)
    if not parts:  # scenery: body / glow groups
        groups = {g.get("id"): g for g in root.findall(f"{{{SVG_NS}}}g")}
        outs = [(name, "glow")] + ([(name + "_glow", "body")] if "glow" in groups else [])
        for out_name, drop in outs:
            copy = ET.fromstring(ET.tostring(root))
            for g in copy.findall(f"{{{SVG_NS}}}g"):
                if g.get("id") == drop:
                    copy.remove(g)
            yield out_name, ET.tostring(copy), height, vb
        return
    vx, vy, vw, vh = (float(v) for v in vb.split())
    yield name, _filtered(root), height, vb
    yield name + "_glow", _filtered(root, keep_class="glow"), height, vb
    for g in parts:
        pid = g.get("id")[5:]
        yield f"{name}__{pid}", _filtered(root, pid), height, vb
        if any(inner.get("class") == "glow" for inner in g):
            yield f"{name}__{pid}_glow", _filtered(root, pid, "glow"), height, vb


def rig(svg_path):
    root = ET.parse(svg_path).getroot()
    parts = _parts(root)
    if not parts:
        return None
    vx, vy, vw, vh = (float(v) for v in root.get("viewBox").split())
    out = []
    for g in parts:
        px, py = (float(v) for v in g.get("data-pivot").split())
        out.append({
            "id": g.get("id")[5:],
            "pivot": [round((px - vx) / vw, 4), round((py - vy) / vh, 4)],
            "glow": any(inner.get("class") == "glow" for inner in g),
        })
    return {"aspect": round(vw / vh, 4), "aura_pad": AURA_PAD, "parts": out}


AURA_PAD = 0.08  # share of the character height added around each part's aura


def aura(part_png_bytes):
    """White, dilated and blurred copy of a part's alpha on a padded canvas: the part-shaped halo."""
    img = Image.open(io.BytesIO(part_png_bytes)).convert("RGBA")
    pad = round(img.size[1] * AURA_PAD)
    canvas = Image.new("L", (img.size[0] + 2 * pad, img.size[1] + 2 * pad), 0)
    canvas.paste(img.getchannel("A"), (pad, pad))
    grown = canvas.filter(ImageFilter.MaxFilter(_odd(img.size[1] // 40)))
    soft = grown.filter(ImageFilter.GaussianBlur(img.size[1] / 30))
    out = Image.new("RGBA", canvas.size, (255, 255, 255, 0))
    out.putalpha(soft)
    buf = io.BytesIO()
    out.save(buf, "PNG")
    return buf.getvalue()


def _odd(n):
    n = max(3, n)
    return n if n % 2 else n + 1


def render(svg_bytes, height, viewbox):
    _, _, w, h = (float(v) for v in viewbox.split())
    width = max(1, round(height * w / h))
    return cairosvg.svg2png(bytestring=svg_bytes, output_width=width, output_height=height)


def build(target):
    target.mkdir(parents=True, exist_ok=True)
    names, rigs = [], {}
    for svg in sorted(SRC.glob("*.svg")):
        for out_name, data, height, viewbox in variants(svg):
            png = render(data, height, viewbox)
            (target / (out_name + ".png")).write_bytes(png)
            names.append(out_name)
            if "__" in out_name and not out_name.endswith("_glow"):
                (target / (out_name + "_aura.png")).write_bytes(aura(png))
                names.append(out_name + "_aura")
        r = rig(svg)
        if r is not None:
            rigs[svg.stem] = r
    (target / "rigs.json").write_text(json.dumps(rigs, indent=1, sort_keys=True) + "\n")
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
        rig_path = OUT / "rigs.json"
        if not rig_path.exists() or rig_path.read_text() != (Path(tmp) / "rigs.json").read_text():
            stale.append("rigs.json (not a png)")
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
