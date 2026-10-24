"""Spires for the pyramid HQ, second round, FLUX: spire2_concepts.py [--seed 1301]
(The user, 2026-10-24, agreeing to mix round one's picks: a lattice or ring spire with a glowing core in its lower
half that burns brighter as the base tiers up.)
(The user, 2026-10-24: "Can you use flux to do some concept art for the spires and then we will have some more concepts off that".)
(The user, 2026-10-24: "the main base is a large central pyramid and the units leave from it, the base expands as you
collect more resources and will have a look that kind of tells you what it is producing" ... "lets spire up and get
fatter over time".) A small early pyramid, a grown one with a spire, and grown ones wearing add-ons that say what
they make. Four tries each.
-> art/_gen/concepts/spire2_<key>_<try>_s<seed>.png and spire2_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("latcore", "LATTICE+CORE", "a tall open steel lattice mast spire rising out of a heavy armoured chamber whose slots show a glowing amber energy core inside, warning lights up the mast and a sensor eye at the tip"),
    ("ringcore", "RINGS+CORE", "a tall spire whose lower half is a glowing amber energy core seen through armour gaps, with three thick armoured rings floating around the upper half at different heights"),
    ("eyecore", "EYE+CORE", "a tall steel lattice tower spire with a big glowing amber eye pod near the top and a glowing amber energy core caged in the lattice near its base, cables between them"),
    ("cage", "CAGED CORE", "a tall spire made of a steel cage of vertical girders around a glowing amber energy core column, two armoured rings clamped round the cage, a beacon at the top"),
    ("grown", "BIG + BRIGHT", "a huge tall spire with a blazing bright amber energy core in its lower half pouring light out of every armour gap, many armoured rings stacked up the lattice above it, a very large base"),
    ("night", "NIGHT", "a tall steel lattice spire with floating armoured rings and a glowing amber energy core in its lower half lighting up the pyramid steps around it, at night, dark scene lit mainly by the core"),
]
WHAT = "An isometric game building for a sci-fi strategy game, big in the picture, filling most of the frame: a stepped armoured pyramid robot base crowned with {idea}."
COLOURS = "Dark slate grey armour panels with orange hazard stripes and small warm amber lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole "
         "building in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in "
         "two or three tones, crisp dark outlines. No text, no letters, no people, no windows.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 1301
TRIES, S = 4, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * TRIES, 40 + S * len(IDEAS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "SPIRES, ROUND 2 (FLUX): lattice and rings over a glowing core", font=font, fill=(255, 240, 200))
for r, (key, label, idea) in enumerate(IDEAS):
    for c in range(TRIES):
        path = os.path.join(OUT, "spire2_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + r * 10 + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        x, y = c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (r * TRIES + c + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "spire2_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "spire2_sheet_s%d.png" % seed)))
