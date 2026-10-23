"""Small rifle mech concepts, FLUX, in the hover tank's look: mech_concepts.py [--seed 401]
(The user, 2026-10-23: "a small mech with rifle, smaller than the tank, moves faster and has animated legs; similar
concept work as the last tank".) A row per body idea in the slate + orange scheme (picked with hover2_slate_0), two
tries each, and each also cut down to 64 px pixel art (hover_round2.pixelate).
-> art/_gen/concepts/mech_<key>_<try>_s<seed>.png (+ _px) and mech_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("chicken", "BIRD LEGS", "a compact boxy body on two backward-bending bird-like legs with flat three-toed feet, a "
                             "long rifle held under the body pointing forward"),
    ("biped", "STOMPER", "a small chunky squared-off torso on two short sturdy straight legs with wide flat feet, a "
                         "long rifle carried in its right arm, a small sensor head"),
    ("pod", "SENSOR POD", "a round sensor pod body with one glowing eye slit, carried on two thin jointed legs, a long "
                          "rifle mounted along its side"),
    ("hunch", "HUNCHBACK", "a hunched low walker with a backpack power unit and antenna, two digitigrade legs, a long "
                           "rifle slung under its chin"),
]
WHAT = ("A small, fast, unmanned light walker robot for a strategy game, big in the picture, filling most of the frame: "
        "{idea}. Small and light, built for speed, standing mid-stride.")
COLOURS = "Dark slate grey armour panels with orange stripes and small warm lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole robot "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people, no pilot.")

from hover_round2 import pixelate  # noqa: E402

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 401
TRIES, S = 2, 300
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * TRIES * 2, 40 + S * len(IDEAS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "SMALL RIFLE MECH (FLUX): two tries per idea, each with its 64 px pixel art", font=font, fill=(255, 240, 200))
for r, (key, label, idea) in enumerate(IDEAS):
    for c in range(TRIES):
        path = os.path.join(OUT, "mech_%s_%d_s%d.png" % (key, c, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + r * 10 + c).convert("RGB").save(path)
            print("done", key, c, flush=True)
        img = Image.open(path).convert("RGB")
        px = pixelate(img)
        px.save(path.replace(".png", "_px.png"))
        x, y = c * S * 2, 40 + r * S
        sheet.paste(img.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        sheet.paste(px.resize((S, S), Image.Resampling.NEAREST), (x + S, y))
        tag = "%s %s" % (label, "ab"[c])
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "mech_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "mech_sheet_s%d.png" % seed)))
