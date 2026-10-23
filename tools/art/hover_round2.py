"""Hover light tank, FLUX round 2: hover_round2.py [--seed 201]
FLUX followed the brief best in round 1 (hover_concepts.py). Here: the turret pushed to the back, two colour schemes,
and each picture also cut down to real pixel art (a small grid, a limited palette, scaled back up square).
-> art/_gen/concepts/hover2_<scheme>_<try>_s<seed>.png (+ _px) and hover2_sheet_s<seed>.png."""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
WHAT = ("A small futuristic hover tank for a strategy game, big in the picture, filling most of the frame. Its hull is a "
        "flat rectangular slab, about twice as long as it is wide, floating a short way above the ground with a soft "
        "glow and a shadow under it, no wheels or tracks. The front half of the hull is a flat empty deck. A low boxy "
        "turret sits on the rear end of the hull, right at its back edge, about as tall as the hull is thick. A long "
        "thin laser cannon barrel reaches forward from the turret, over the empty front deck and well past the front "
        "of the hull, ending in a glowing tip.")
SCHEMES = [
    ("white", "WHITE + BLUE", "White and light grey armour panels with blue stripes."),
    ("slate", "SLATE + ORANGE", "Dark slate grey armour panels with orange stripes and small warm lights."),
]
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole vehicle "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people.")


def pixelate(img, across=64, colours=20):
    """Real pixel art from a picture: the vehicle found against the plain background and cut down so it is `across`
    pixels wide, a limited palette taken from it, then scaled back up square with hard pixels."""
    a = np.asarray(img, dtype=np.float32)
    bg = np.median(np.concatenate([a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3), a[:, :8].reshape(-1, 3), a[:, -8:].reshape(-1, 3)]), axis=0)
    ys, xs = np.nonzero(np.abs(a - bg).sum(axis=2) > 40)
    if len(xs) == 0:
        return img
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    side = int(max(x1 - x0, y1 - y0) * 1.1)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    crop = img.crop((cx - side // 2, cy - side // 2, cx + side // 2, cy + side // 2))
    small = crop.resize((across, across), Image.Resampling.BOX)
    small = small.quantize(colours, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    return small.resize(img.size, Image.Resampling.NEAREST)


args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 301
TRIES, S = 4, 300
font = ImageFont.truetype("arialbd.ttf", 18)
rows = [(k, l, w, px) for k, l, w in SCHEMES for px in (False, True)]
sheet = Image.new("RGB", (190 + S * TRIES, 40 + S * len(rows)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "HOVER LIGHT TANK (FLUX): turret on the rear edge, empty front deck; each also as 64 px pixel art", font=font, fill=(255, 240, 200))
for r, (key, label, colours, px) in enumerate(rows):
    d.text((10, 40 + r * S + S // 2 - 10), label + ("  PIXELS" if px else ""), font=font, fill=(255, 255, 255))
    for c in range(TRIES):
        path = os.path.join(OUT, "hover2_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT, colours, STYLE), seed + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        img = Image.open(path).convert("RGB")
        if px:
            pxpath = path.replace(".png", "_px.png")
            img = pixelate(img)
            img.save(pxpath)
        sheet.paste(img.resize((S, S), Image.Resampling.NEAREST if px else Image.Resampling.LANCZOS), (190 + c * S, 40 + r * S))
sheet.save(os.path.join(OUT, "hover2_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "hover2_sheet_s%d.png" % seed)))
