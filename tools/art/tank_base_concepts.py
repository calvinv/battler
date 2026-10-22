"""Concepts: one unit's look (the battle tank) and a base growing from the middle out: tank_base_concepts.py [--seed 31]
(The user, 2026-10-22: "let's work on one unit's aesthetic and a base that grows from the middle out".)
-> art/_gen/concepts/tb_<key>_s<seed>.png, tank_sheet_s<seed>.png (a column per look, two tries each) and
base_sheet_s<seed>.png (a column per stage of growth, two tries each). About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
LOOK = ("gritty dieselpunk, heavy riveted steel armour plates, olive drab and gunmetal paint chipped to bare steel, "
        "soot and oil stains, rust streaks, tall exhaust stacks, amber lamps, faded stencilled numbers, blue team markings")

TANKS = [
    ("brute", "BRUTE", "a heavy main battle tank with a big boxy riveted turret and one long thick cannon, twin exhaust "
                       "stacks at the back, wide (caterpillar tracks:1.1) with armoured side skirts"),
    ("wedge", "WEDGE", "a low wide tank with steeply sloped wedge-shaped front armour, a squat turret set far back and a "
                       "very long cannon with a muzzle brake, broad (caterpillar tracks:1.1)"),
    ("tower", "TOWER", "a tall stepped tank, a small upper turret stacked on a big lower turret, a long main cannon and "
                       "a short machine gun, a searchlight, (caterpillar tracks:1.1), a big diesel engine deck"),
    ("hammer", "HAMMER", "a heavy tank with a round cast-steel turret and a short fat howitzer cannon, sandbag and spare "
                         "track links hung on the hull, smoking exhaust, wide (caterpillar tracks:1.1)"),
]
STAGES = [
    ("seed", "1 HQ ALONE", "a lone fortified dieselpunk headquarters bunker with chimneys and a radar mast, standing "
                           "alone on bare dirt, a few crates and a fuel tank beside it"),
    ("wings", "2 FIRST WINGS", "a fortified dieselpunk headquarters bunker in the centre with three vehicle workshop "
                               "halls with sawtooth roofs joined to it, radiating outward like spokes, short roads"),
    ("spread", "3 SPREADING", "a dieselpunk military base growing outward from a central headquarters bunker: workshop "
                              "halls joined in a ring around it, pipes and roads running out to an oil pumpjack and a "
                              "smoking power plant, gun emplacements at the edges"),
    ("sprawl", "4 SPRAWL", "a big sprawling dieselpunk war factory compound grown out in rings from a central "
                           "headquarters, many workshop halls, smokestacks, pipelines and roads reaching out to oil "
                           "derricks and power plants, sandbagged gun pits and walls around the outside"),
]
TANK_PROMPT = ("RTS game concept art, one single war machine seen whole, isolated on a plain flat pale grey background, "
               "(it is {what}:1.2), ({look}:1.1), (no crew, no humans, no hatches open, no windows:1.1), seen from above "
               "at a three-quarter angle, centred, the whole thing in frame, high detail, painterly realism")
BASE_PROMPT = ("RTS game concept art, (top-down three-quarter aerial view:1.2) of {what}, on flat brown dirt ground, "
               "({look}:1.0), the whole base in frame and centred, readable chunky shapes, no people, high detail, "
               "painterly realism")
NEG = ("people, human, soldier, crew, face, anime, cartoon, city, sky, horizon, clouds, explosions, blurry, text, "
       "watermark, letters, frame, border, several views, sprite sheet, lineup, cropped, cut off, steampunk, brass, "
       "gears, victorian, flag, sci-fi, spaceship, legs, glowing blue")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 31
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 22)
pg.start_server()
S = 384


def sheet(title, items, prompt, name):
    img = Image.new("RGB", (S * len(items), 44 + S * 2), (14, 14, 20))
    d = ImageDraw.Draw(img)
    d.text((14, 8), title, font=font, fill=(255, 240, 200))
    for c, (key, label, what) in enumerate(items):
        for r in range(2):
            path = os.path.join(OUT, "tb_%s_%d_s%d.png" % (key, r, seed))
            if os.path.exists(path):
                shot = Image.open(path).convert("RGB")
            else:
                g = pg.workflow(prompt.format(what=what, look=LOOK), 1024, 1024, seed + c * 10 + r, 30, 0.0, False)
                g["neg"]["inputs"]["text"] = NEG
                shot = pg.generate(g).convert("RGB")
                shot.save(path)
                print("done", key, r, flush=True)
            x, y = c * S, 44 + r * S
            img.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
            tag = "%s %s" % (label, "ab"[r])
            d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
            d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
    img.save(os.path.join(OUT, "%s_s%d.png" % (name, seed)))


sheet("BATTLE TANK: FOUR LOOKS", TANKS, TANK_PROMPT, "tank_sheet")
sheet("A BASE GROWING FROM THE MIDDLE OUT", STAGES, BASE_PROMPT, "base_sheet")
print("sheets in", os.path.abspath(OUT))
