"""Battle wheel, round 2: wheel_round2.py [--seed 701]
(The user, 2026-10-23: "it is one big wheel that drives, the guns are mounted on the side". Round 1 kept growing
extra wheels.) Row 1: words only, pushed hard at one wheel. Row 2: FLUX painting over a drawn layout (one big upright
wheel, a gun pod on each axle end), so the shape can't drift; at two strengths.
-> art/_gen/concepts/wheel2_<key>_s<seed>.png, wheel2_layout.png and wheel2_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
WHAT = ("A monowheel battle robot for a strategy game, big in the picture: its whole body is ONE single giant wheel "
        "standing upright and rolling on its edge, a thick chunky tyre with a deep tread around an armoured hub; only "
        "this one wheel touches the ground, there are no other wheels, no legs, no tracks. On each side of the wheel, "
        "bolted to the ends of its axle, is a gun pod with a short gun barrel pointing forward, one gun on the left "
        "and one on the right. A small glowing amber sensor eye on the hub. {extra}")
EXTRAS = ["", "The hub cap has orange stripes.", "Seen rolling toward the viewer, leaning slightly.",
          "The gun pods are chunky boxes with twin barrels."]
COLOURS = "Dark slate grey armour with orange stripes and small warm lights, a black rubber tyre."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole robot "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people, no rider.")


def layout():
    """One big upright wheel seen three-quarters from the front: a black tyre band, a slate hub, a gun pod on each
    axle end pointing forward (toward the lower right)."""
    N = 1024
    img = Image.new("RGB", (N, N), (42, 44, 52))
    d = ImageDraw.Draw(img)
    cx, cy, rx, ry = 512, 470, 190, 330
    d.ellipse((cx - 150 - 30, cy + ry - 40, cx + 230, cy + ry + 60), fill=(30, 31, 37))          # shadow
    d.ellipse((cx - rx + 70, cy - ry, cx + rx + 70, cy + ry), fill=(22, 22, 26))                 # far side of the tyre
    d.rectangle((cx, cy - ry, cx + 70, cy + ry), fill=(22, 22, 26))
    d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(30, 30, 34))                           # near tyre face
    for i in range(14):                                                                          # tread blocks
        import math
        t = i / 14 * math.tau
        x, y = cx + math.cos(t) * (rx + 18), cy + math.sin(t) * (ry + 18)
        d.rectangle((x - 18, y - 22, x + 18, y + 22), fill=(18, 18, 22))
    d.ellipse((cx - rx * 0.62, cy - ry * 0.62, cx + rx * 0.62, cy + ry * 0.62), fill=(70, 76, 88))   # rim
    d.ellipse((cx - rx * 0.32, cy - ry * 0.32, cx + rx * 0.32, cy + ry * 0.32), fill=(96, 102, 114))  # hub
    d.ellipse((cx - 30, cy - 70, cx + 30, cy - 10), fill=(250, 180, 60))                          # eye
    d.rectangle((cx - 60, cy + 40, cx + 60, cy + 60), fill=(230, 120, 40))                        # stripe
    # Near gun pod on the near axle end, barrel forward (lower right); the far one peeking out on the other side.
    d.polygon([(cx - 70, cy - 40), (cx + 70, cy - 10), (cx + 70, cy + 70), (cx - 70, cy + 40)], fill=(80, 86, 98))
    d.polygon([(cx + 60, cy + 5), (cx + 300, cy + 75), (cx + 300, cy + 105), (cx + 60, cy + 40)], fill=(28, 30, 36))
    d.polygon([(cx + 200, cy - 120), (cx + 330, cy - 90), (cx + 330, cy - 30), (cx + 200, cy - 60)], fill=(70, 76, 88))
    d.polygon([(cx + 320, cy - 85), (cx + 460, cy - 45), (cx + 460, cy - 20), (cx + 320, cy - 55)], fill=(28, 30, 36))
    return img.filter(ImageFilter.GaussianBlur(2))


args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 701
pg.start_server()
lay = layout()
lay_path = os.path.join(OUT, "wheel2_layout.png")
lay.save(lay_path)
init = pg.stage_init(lay_path)
jobs = [("words%d" % i, "WORDS %d" % (i + 1), WHAT.format(extra=e), None) for i, e in enumerate(EXTRAS)]
jobs += [("drawn%d" % i, "DRAWN %.2f" % dn, WHAT.format(extra=EXTRAS[i % 4]), dn) for i, dn in enumerate((0.7, 0.7, 0.82, 0.82))]
S, COLS = 300, 4
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * (COLS + 1), 40 + S * 2), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "BATTLE WHEEL, ROUND 2: one big wheel, a gun each side (row 2 painted over the drawn layout, right)", font=font, fill=(255, 240, 200))
sheet.paste(lay.resize((S, S)), (COLS * S, 40 + S))
for n, (key, label, prompt, dn) in enumerate(jobs):
    path = os.path.join(OUT, "wheel2_%s_s%d.png" % (key, seed))
    if not os.path.exists(path):
        full = "%s %s %s" % (prompt, COLOURS, STYLE)
        if dn:
            g = fluxgen.graph(full, seed + n, 1024, 1024, steps=8)
            g["init"] = {"class_type": "LoadImage", "inputs": {"image": init}}
            g["latent"] = {"class_type": "VAEEncode", "inputs": {"pixels": ["init", 0], "vae": ["vae", 0]}}
            g["sample"]["inputs"]["denoise"] = dn
            img = pg.generate(g)
        else:
            img = fluxgen.image(full, seed + n)
        img.convert("RGB").save(path)
        print("done", key, flush=True)
    x, y = (n % COLS) * S, 40 + (n // COLS) * S
    sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
    tag = "%d %s" % (n + 1, label)
    d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
    d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "wheel2_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "wheel2_sheet_s%d.png" % seed)))
