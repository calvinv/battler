"""The pyramid HQ, FLUX: pyramid_concepts.py [--seed 1101]
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
    ("small", "EARLY", "a small squat stepped pyramid, three wide armoured tiers, a glowing amber eye lamp on its flat "
                       "top, one big armoured garage door at the bottom of the front face with a small hover tank "
                       "floating out of it"),
    ("tall", "GROWN", "a big stepped pyramid with five armoured tiers rising to a tall thin antenna spire with a glowing "
                      "amber eye near the top, wide at the base, armoured garage doors on two faces with ramps"),
    ("armour", "+ HANGARS", "a big stepped pyramid with a tall spire and a glowing amber eye, with a long low vehicle "
                            "hangar bolted onto one side, ramps, and a chunky two-wheeled robot with a round cannon "
                            "rolling out"),
    ("guns", "+ BATTERY", "a big stepped pyramid with a tall spire and a glowing amber eye, with heavy artillery "
                          "cannon barrels and ammunition drums built into its upper tiers pointing up at an angle"),
    ("air", "+ AIR DECK", "a big stepped pyramid whose tall spire carries a round landing deck with propeller drones "
                          "parked on it, a glowing amber eye under the deck, landing lights round its rim"),
    ("support", "+ DISH", "a big stepped pyramid with a tall spire, a big glowing green radar dish on one corner tier "
                          "and a green lit drone launch pad with little repair drones hovering over it"),
]
WHAT = "An isometric game building for a sci-fi strategy game, big in the picture, filling most of the frame: {idea}."
COLOURS = "Dark slate grey armour panels with orange hazard stripes and small warm amber lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole "
         "building in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in "
         "two or three tones, crisp dark outlines. No text, no letters, no people, no windows.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 1101
TRIES, S = 4, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * TRIES, 40 + S * len(IDEAS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "PYRAMID HQ (FLUX): early, grown, and grown with add-ons", font=font, fill=(255, 240, 200))
for r, (key, label, idea) in enumerate(IDEAS):
    for c in range(TRIES):
        path = os.path.join(OUT, "pyr_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + r * 10 + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        x, y = c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (r * TRIES + c + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "pyr_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "pyr_sheet_s%d.png" % seed)))
