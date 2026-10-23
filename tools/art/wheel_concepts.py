"""Battle wheel concepts, FLUX, in the hover tank / mech look: wheel_concepts.py [--seed 601]
(The user, 2026-10-23: "let's do the battle wheel now, a large wheel with two guns off to each side".)
Four ideas, two tries each. -> art/_gen/concepts/wheel_<key>_<try>_s<seed>.png and wheel_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("hub", "HUB CORE", "one huge single wheel standing upright with a thick chunky tread, its hub a rounded armoured "
                        "core with two glowing amber eye lamps, a short stubby gun pod sticking out on each side of the "
                        "hub like arms"),
    ("rim", "OPEN RIM", "one huge single open-centred ring wheel standing upright with a chunky tread, a boxy armoured "
                        "body hanging inside the ring, two long guns mounted on the axle ends sticking out to the left "
                        "and right"),
    ("arms", "GUN ARMS", "one large single wheel standing upright with a ribbed tread, short armoured arms reaching out "
                         "from the axle on both sides, each arm holding a gun pointing forward, a small sensor eye on top"),
    ("gyro", "GYRO", "one big single wheel standing upright, the wheel half covered by an armoured fender shell on top "
                     "with orange stripes, twin gun barrels poking forward from each side of the shell"),
]
WHAT = ("A cute cartoonish unmanned battle robot for a strategy game, big in the picture, filling most of the frame: "
        "{idea}. Rolling fast, leaning a little into its roll.")
COLOURS = "Dark slate grey armour panels with orange stripes and small warm lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole robot "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people, no rider.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 601
TRIES, S = 2, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * len(IDEAS), 40 + S * TRIES), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "BATTLE WHEEL (FLUX): a large wheel with a gun on each side", font=font, fill=(255, 240, 200))
for c, (key, label, idea) in enumerate(IDEAS):
    for r in range(TRIES):
        path = os.path.join(OUT, "wheel_%s_%d_s%d.png" % (key, r, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + c * 10 + r).convert("RGB").save(path)
            print("done", key, r, flush=True)
        x, y = c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (c * TRIES + r + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "wheel_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "wheel_sheet_s%d.png" % seed)))
