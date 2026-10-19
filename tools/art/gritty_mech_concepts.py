"""Gritty dieselpunk mech shapes for Battler: gritty_mech_concepts.py [--seed 7]
(The user, 2026-10-19, after the nuclearpunk sheet: "lets explore gritty dieselpunk more with mechs, like the first
gritty pass", meaning mech_style_concepts.py's GRITTY INDUSTRIAL (2a/2b) and DIESELPUNK (5a/5b).) One look, six body
types x two seeds, so the shape language can be chosen. Weapons are spelt out and weighted: the first sheet mostly
gave claw hands. No crews: the machines are the factions. Pixel LoRA 0.
-> art/_gen/concepts/gmech_<key>_s<seed>_<v>.png and gmech_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
LOOK = ("gritty dieselpunk, worn olive drab and grey riveted steel plates, chipped paint, rust streaks, soot, oil "
        "stains, exhaust stacks, hydraulic pistons, thick bolts, faded stencilled serial numbers, unmanned machine with "
        "glowing sensor eyes, post-apocalyptic, painterly realistic concept art like Supreme Commander")
BODIES = [
    ("biped", "HEAVY BIPED", "a heavy two-legged combat mech walker with a squat armoured hull body and no head, "
     "(a big cannon barrel instead of the right arm:1.2), (a box missile launcher on the left shoulder:1.2)"),
    ("quad", "QUAD WALKER", "a four-legged walking tank, a boxy armoured hull on four stout piston legs, "
     "(a tank turret with a long cannon on top:1.25)"),
    ("chicken", "REVERSE-JOINT RAIDER", "a light fast two-legged mech with backward bending bird-like legs, a small "
     "armoured cockpit pod body, (twin machine guns under the body:1.2), a tall exhaust stack"),
    ("brawler", "HUNCHED BRAWLER", "a hunched gorilla-like heavy mech with huge thick forearms and short legs, a big "
     "diesel engine block on its back, (rotary autocannons for fists:1.2)"),
    ("artillery", "ARTILLERY STRIDER", "a tall long-legged two-legged mech carrying (a huge long artillery howitzer "
     "barrel along its back:1.25), stabiliser feet, ammunition drums"),
    ("crawler", "SIX-LEG CRAWLER", "a low wide six-legged spider-like walking war machine, a flat armoured body, "
     "(a flak cannon turret with four barrels pointing up:1.2)"),
]
PROMPT = ("war machine design concept art, one single machine seen whole, isolated on a plain flat pale grey "
          "background, nothing around it, (it is {what}:1.15), ({look}:1.2), seen from above at a three-quarter angle, "
          "centred, the whole machine in frame, game concept art, high detail")
NEG = ("people, human, pilot, crew, anime, face, crowd, scene, environment, landscape, city, buildings, terrain, sky, "
       "clouds, explosions, blurry, text, watermark, signature, letters, frame, border, many robots, two robots, "
       "several views, sprite sheet, cropped, cut off, claws, hands, fingers, clean, shiny, white, steampunk, brass, "
       "gears, flag, star")

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
d.text((14, 8), "BATTLER: GRITTY DIESELPUNK MECHS  (pick shapes, mix freely)", font=font, fill=(255, 240, 200))
for n, (key, name, what) in enumerate(BODIES):
    for v in range(2):
        path = os.path.join(OUT, "gmech_%s_s%d_%d.png" % (key, seed, v))
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
sheet.save(os.path.join(OUT, "gmech_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
