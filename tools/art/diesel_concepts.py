"""Concepts for a modern gritty dieselpunk look: diesel_concepts.py [--seed 21]
(The user, 2026-10-22: "I'm thinking of changing the theme to be modern gritty dieselpunk style"; concepts first.)
A row per civilisation, a column per thing, so the look can be judged across the roster. Machines only, no crew.
-> art/_gen/concepts/diesel_<civ>_<thing>_s<seed>.png and diesel_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

CIVS = [
    ("coalition", "COALITION",
     "modern gritty dieselpunk military machine, heavy riveted steel armour plates, big diesel engine with tall exhaust "
     "stacks trailing dark smoke, olive drab and gunmetal paint chipped to bare steel, rust streaks, oil and soot stains, "
     "chunky (caterpillar tracks:1.1), stencilled numbers, blue team markings, grounded and functional",
     "legs, glowing, sleek, clean, alien"),
    ("lattice", "LATTICE",
     "gritty dieselpunk scavenger war machine welded together from salvaged rusty scrap, mismatched riveted plates, "
     "crooked smoking exhaust pipes, oil leaks, clusters of small glowing amber lamps, (mechanical spider legs or "
     "mismatched wheels:1.1), rust orange and bone white, improvised and menacing",
     "clean, sleek, white, alien"),
]
THINGS = [
    ("light", "LIGHT TANK", "a small fast light tank with one short gun"),
    ("tank", "TANK", "a heavy main battle tank, (a big turret with a long heavy cannon:1.2)"),
    ("artillery", "ARTILLERY", "a self-propelled artillery gun, (a very long howitzer barrel raised steeply:1.25)"),
    ("bomber", "BOMBER", "(a heavy propeller bomber aircraft seen from above:1.3), engines with exhaust"),
    ("hq", "HQ", "(a fortified headquarters building:1.3) with chimneys, a radar mast and armoured walls"),
]
PROMPT = ("RTS game concept art, one single {kind} seen whole, isolated on a plain flat pale grey background, nothing "
          "around it, (it is {what}:1.15), ({look}:1.2), (no crew, no humans, no cockpit glass, no windows:1.1), seen "
          "from above at a three-quarter angle, centred, the whole thing in frame, high detail, painterly realism")
NEG = ("people, human, pilot, crew, soldier, face, anime, animal, crowd, scene, landscape, city, sky, clouds, "
       "explosions, blurry, text, watermark, letters, frame, border, several views, sprite sheet, lineup, cropped, "
       "cut off, steampunk, brass, gears, victorian, flag, {avoid}")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 21
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 22)
pg.start_server()
S = 384
sheet = Image.new("RGB", (S * len(THINGS), 44 + S * len(CIVS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "BATTLER: MODERN GRITTY DIESELPUNK", font=font, fill=(255, 240, 200))
for r, (ckey, cname, look, avoid) in enumerate(CIVS):
    for c, (tkey, tname, what) in enumerate(THINGS):
        path = os.path.join(OUT, "diesel_%s_%s_s%d.png" % (ckey, tkey, seed))
        if os.path.exists(path):
            shot = Image.open(path).convert("RGB")
        else:
            kind = "building" if tkey == "hq" else "war machine"
            g = pg.workflow(PROMPT.format(kind=kind, what=what, look=look), 1024, 1024, seed + r * 10 + c, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG.format(avoid=avoid)
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        x, y = c * S, 44 + r * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%s  %s" % (cname, tname)
        d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
        print("done", ckey, tkey, flush=True)
sheet.save(os.path.join(OUT, "diesel_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
