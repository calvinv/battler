"""Spires for the pyramid HQ, FLUX: spire_concepts.py [--seed 1201]
(The user, 2026-10-24: "Can you use flux to do some concept art for the spires and then we will have some more concepts off that".)
(The user, 2026-10-24: "the main base is a large central pyramid and the units leave from it, the base expands as you
collect more resources and will have a look that kind of tells you what it is producing" ... "lets spire up and get
fatter over time".) A small early pyramid, a grown one with a spire, and grown ones wearing add-ons that say what
they make. Four tries each.
-> art/_gen/concepts/pyr_<key>_<try>_s<seed>.png and pyr_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("antenna", "ANTENNA", "a tall thin needle spire made of stacked armoured segments, a ring of glowing amber eye lenses partway up, thin radio aerials and a beacon light at the very tip"),
    ("rings", "RINGS", "a tall spire wrapped in several floating armoured rings that circle it at different heights, glowing amber seams between the rings, a sensor eye at the top"),
    ("drill", "DRILL", "a tall twisted spiral spire like a giant drill bit pointing at the sky, armour plates following the spiral, warm amber lights along the grooves"),
    ("lattice", "LATTICE", "a tall open steel lattice tower spire like a radio mast, with an armoured pod halfway up holding a big glowing amber eye, cables and warning lights"),
    ("stack", "STACK", "a tall spire built from chunky stacked machinery blocks getting smaller toward the top, pipes and vents, a smokestack, glowing amber windows, a crane arm"),
    ("crystal", "CORE", "a tall faceted spire with a glowing amber energy core visible through gaps in its armour plates, sparks of light rising off its tip"),
]
WHAT = "An isometric game building for a sci-fi strategy game, big in the picture, filling most of the frame: a stepped armoured pyramid robot base crowned with {idea}."
COLOURS = "Dark slate grey armour panels with orange hazard stripes and small warm amber lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole "
         "building in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in "
         "two or three tones, crisp dark outlines. No text, no letters, no people, no windows.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 1201
TRIES, S = 4, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * TRIES, 40 + S * len(IDEAS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "SPIRES (FLUX): what crowns the pyramid", font=font, fill=(255, 240, 200))
for r, (key, label, idea) in enumerate(IDEAS):
    for c in range(TRIES):
        path = os.path.join(OUT, "spire_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + r * 10 + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        x, y = c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (r * TRIES + c + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "spire_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "spire_sheet_s%d.png" % seed)))
