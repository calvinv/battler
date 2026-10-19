"""Gritty dieselpunk tracked war machines for Battler: tracked_concepts.py [--seed 7]
(The user, 2026-10-19, on gritty_mech_concepts.py's sheet: "I like this style but with less legs and more tracks".)
The same look as that sheet, on tracks: a roster of six roles x two seeds, two of them mech-on-tracks hybrids to
show where legs end and tracks begin. No crews: the machines are the factions. Pixel LoRA 0.
-> art/_gen/concepts/track_<key>_s<seed>_<v>.png and track_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
LOOK = ("gritty dieselpunk, worn olive drab and grey riveted steel plates, chipped paint, rust streaks, soot, oil "
        "stains, exhaust stacks, thick bolts, faded stencilled serial numbers, mud caked tracks, unmanned machine with "
        "glowing sensor eyes, post-apocalyptic, painterly realistic concept art like Supreme Commander")
ROLES = [
    ("raider", "RAIDER", "a small fast light tracked war machine, low and narrow, a big engine with exhaust stacks, "
     "(twin machine guns in a small open mount:1.2)"),
    ("battle", "BATTLE TANK", "a heavy tracked battle tank, an angular riveted hull, (a big turret with a long thick "
     "cannon:1.2), a sensor mast"),
    ("land", "LAND BATTLESHIP", "an enormous super-heavy tracked land battleship, four wide tracks, a tall armoured "
     "superstructure, (two big cannon turrets:1.2), smokestacks"),
    ("artillery", "ARTILLERY", "a tracked self-propelled artillery vehicle, (a huge long howitzer barrel raised high "
     "on an open mount:1.25), recoil spades at the back, ammunition racks"),
    ("flak", "FLAK", "a tracked anti-aircraft vehicle, (a quad flak cannon turret with four long barrels pointing up "
     "at the sky:1.25), a range finder"),
    ("hybrid", "MECH ON TRACKS", "a heavy war machine with a robotic armoured upper body and two thick mechanical arms "
     "on a wide tracked tank chassis, no legs, (a cannon for one arm and a rotary autocannon for the other:1.2)"),
]
PROMPT = ("war machine design concept art, one single tracked vehicle seen whole, isolated on a plain flat pale grey "
          "background, nothing around it, (it is {what}:1.15), ({look}:1.2), seen from above at a three-quarter angle, "
          "centred, the whole machine in frame, game concept art, high detail")
NEG = ("legs, walker, feet, people, human, pilot, crew, anime, face, animal, crowd, scene, environment, landscape, city, "
       "buildings, terrain, sky, clouds, explosions, blurry, text, watermark, signature, letters, frame, border, many "
       "vehicles, two vehicles, several views, sprite sheet, lineup, cropped, cut off, clean, shiny, white, steampunk, "
       "brass, gears, flag, star")

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
d.text((14, 8), "BATTLER: GRITTY DIESELPUNK ON TRACKS  (pick shapes, mix freely)", font=font, fill=(255, 240, 200))
for n, (key, name, what) in enumerate(ROLES):
    for v in range(2):
        path = os.path.join(OUT, "track_%s_s%d_%d.png" % (key, seed, v))
        if os.path.exists(path):      # (kept from a run that was stopped)
            shot = Image.open(path).convert("RGB")
        else:
            g = pg.workflow(PROMPT.format(what=what, look=LOOK), 1024, 1024, seed + n * 10 + v, 30, 0.0, False)
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
sheet.save(os.path.join(OUT, "track_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
