"""Style directions for Battler's mechs: mech_style_concepts.py [--seed 7]
(The user, 2026-10-19: "lets settle the look" for a Supreme Commander style mech base builder.) The same subject,
a mid-weight assault mech, in six looks x two seeds, so the look is chosen before any designs. Every look must be
buildable as a flat-sided low-poly model painted by the ai-tools paint pass. Pixel LoRA 0, as for Voidwake's
concept art. -> art/_gen/concepts/style_<key>_s<seed>.png and style_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
MECH = ("a heavy bipedal combat mech walker robot, two thick legs, a squat armoured torso, a big cannon arm and a "
        "missile pod on the shoulder, standing alone")
STYLES = [
    ("cel", "CEL SHADED + INK", "dark gunmetal grey plating with blue team colour panels, cel shaded, flat colours in "
     "clean bands, crisp dark ink outlines, hard edges"),
    ("gritty", "GRITTY INDUSTRIAL", "worn olive and grey military steel, scratched paint, hydraulic pistons, soot and "
     "rust, painterly realistic concept art like Supreme Commander and Total Annihilation"),
    ("clean", "CLEAN SCI-FI", "smooth white and light grey armour panels with blue team colour accents and glowing "
     "cyan lights, sleek hard surface, polished sci-fi concept art"),
    ("toy", "CHUNKY LOW POLY", "stubby chunky proportions, simple flat faceted low poly shapes, bright saturated blue "
     "and white with yellow accents, clean flat lighting, toy-like and readable, stylised game art"),
    ("diesel", "DIESELPUNK", "riveted dark olive steel plates, exhaust stacks, big bolts, brass fittings, stencilled "
     "numbers, a 1940s war machine walker, painterly concept art"),
    ("brutal", "BRUTALIST BLOCKS", "massive blocky slab armour like concrete bunkers, dark charcoal with bold orange "
     "hazard markings, few large shapes, heavy and monolithic, graphic flat shading"),
]
PROMPT = ("mech design concept art, one single machine seen whole, isolated on a plain flat pale grey background, "
          "nothing around it, (it is {what}:1.15), ({look}:1.2), seen from above at a three-quarter angle, centred, "
          "the whole machine in frame, game concept art, high detail")
NEG = ("people, human, pilot, anime girl, face, crowd, scene, environment, landscape, city, buildings, ground clutter, "
       "terrain, sky, clouds, explosions, smoke, blurry, text, watermark, signature, letters, frame, border, "
       "many robots, sprite sheet, cropped, cut off")

args = sys.argv[1:]
seed = 7
if "--seed" in args:
    i = args.index("--seed")
    seed = int(args[i + 1])
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 26)
pg.start_server()
S = 512
sheet = Image.new("RGB", (S * 4, 44 + S * 3), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "BATTLER: MECH STYLE DIRECTIONS  (pick a number, mix freely)", font=font, fill=(255, 240, 200))
for n, (key, name, look) in enumerate(STYLES):
    for v in range(2):
        path = os.path.join(OUT, "style_%s_s%d_%d.png" % (key, seed, v))
        if os.path.exists(path):      # (kept from a run that was stopped)
            shot = Image.open(path).convert("RGB")
        else:
            g = pg.workflow(PROMPT.format(what=MECH, look=look), 1024, 1024, seed + n * 10 + v, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        cell = n * 2 + v
        x, y = (cell % 4) * S, 44 + (cell // 4) * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%d%s  %s" % (n + 1, "ab"[v], name)
        d.text((x + 11, y + 9), label, font=font, fill=(0, 0, 0))
        d.text((x + 10, y + 8), label, font=font, fill=(255, 255, 255))
        print("done", key, v, flush=True)
sheet.save(os.path.join(OUT, "style_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
