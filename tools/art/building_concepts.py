"""Buildings the units come out of, FLUX, in the hover tank / mech / battle wheel look: building_concepts.py [--seed 801]
(The user, 2026-10-23: "lets work on the buildings they come out of".) The HQ core the base grows from, and a wing
for each new unit with that unit rolling out of it. Four tries each.
-> art/_gen/concepts/bld_<key>_<try>_s<seed>.png and bld_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("hq", "HQ CORE", "the central core building of an unmanned robot base: a chunky squat armoured hub, octagonal, with a "
                      "big glowing amber eye lamp on top like a sensor dome, heavy sloped armour panels, short stubby "
                      "connector arms sticking out on several sides where factory wings will attach, vents and small "
                      "warm lights"),
    ("hover", "HOVER BAY", "a small futuristic factory wing building, a low rectangular armoured hangar with a wide open "
                           "bay door at one end and a glowing launch pad in front of it; a small flat hover tank with a "
                           "long thin laser barrel is floating out of the bay door"),
    ("mech", "MECH BAY", "a small futuristic factory wing building, a tall narrow armoured assembly tower with a robotic "
                         "crane arm on top and a tall open doorway; a small cartoonish two-legged bird-legged robot "
                         "walker carrying a rifle is stepping out of the doorway"),
    ("wheel", "WHEEL BAY", "a small futuristic factory wing building, a round drum-shaped armoured workshop with a big "
                           "round rolling shutter door and a ramp; a chunky robot with two big tyres and a fat round "
                           "cannon between them is rolling down the ramp"),
]
WHAT = "An isometric game building for a sci-fi strategy game, big in the picture, filling most of the frame: {idea}."
COLOURS = "Dark slate grey armour panels with orange hazard stripes and small warm amber lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole "
         "building in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in "
         "two or three tones, crisp dark outlines. No text, no letters, no people, no windows.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 801
TRIES, S = 4, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * TRIES, 40 + S * len(IDEAS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "BUILDINGS (FLUX): the HQ core, and a wing for each unit", font=font, fill=(255, 240, 200))
for r, (key, label, idea) in enumerate(IDEAS):
    for c in range(TRIES):
        path = os.path.join(OUT, "bld_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + r * 10 + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        x, y = c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (r * TRIES + c + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "bld_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "bld_sheet_s%d.png" % seed)))
