"""Machine design languages for Battler's units: machine_concepts.py [--seed 7]
(The user, 2026-10-20: "can we come up with concepts, I think they should look quite different"; "the ships should be
artificial, the game brief is future, no humans"; "the tank shape is similar to what the player understands".)
So: machines built by machines for machines, no crew anywhere (no hatches, cockpits, windows, seats, ladders), each
unit still reading at a glance as its role (a tank is a tank, artillery is artillery). Five design languages down the
sheet, the five units across, to pick from or mix. Pixel LoRA 0.
-> art/_gen/concepts/machine_<style>_<unit>_s<seed>.png and machine_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

STYLES = [
    ("monolith", "A  MONOLITH",
     "seamless smooth white and graphite ceramic armour, sharp faceted planes, no visible seams or bolts, thin glowing "
     "cyan light lines, sleek and minimal, like a futuristic AI-made machine"),
    ("hover", "B  HOVER",
     "floating on glowing anti-gravity pads instead of tracks or wheels, hovering above the ground, sleek curved armour "
     "shells, exposed glowing energy cores, futuristic"),
    ("insect", "C  ARTICULATED",
     "segmented articulated armour like an insect or crustacean, overlapping plates, exposed mechanical joints and "
     "hydraulics, many small tracks, robotic, futuristic"),
    ("modular", "D  MODULAR",
     "built from interlocking modular blocks and hexagonal panels, exposed machinery and cable bundles between modules, "
     "industrial robotic, utilitarian, futuristic"),
    ("crystal", "E  ENERGY",
     "dark metal frame holding floating armour plates around a glowing crystalline energy core, energy weapons with "
     "glowing coils, alien but machine-made, futuristic"),
]

UNITS = [
    ("light", "LIGHT TANK", "a small fast light tank, low and nimble, (one long thin cannon:1.15)"),
    ("tank", "TANK", "a main battle tank, (a big turret with a long heavy cannon:1.2), clearly a tank"),
    ("mortar", "MORTAR", "a small squat vehicle carrying (a short fat mortar tube pointing almost straight up:1.25)"),
    ("artillery", "ARTILLERY", "a long self-propelled artillery vehicle, (a very long cannon raised steeply high:1.25)"),
    ("scout", "SCOUT", "a small fast unarmed scout vehicle, (a big sensor dish or radar array:1.2), no gun"),
]

PROMPT = ("futuristic robotic war machine design concept art, one single unmanned vehicle seen whole, isolated on a plain "
          "flat pale grey background, nothing around it, (it is {what}:1.15), ({look}:1.2), (no crew, no cockpit, no "
          "windows, no hatches:1.1), blue team colour panels, seen from above at a three-quarter angle, centred, the "
          "whole machine in frame, Supreme Commander style RTS unit concept art, high detail")
NEG = ("people, human, pilot, crew, driver, cockpit, windows, windshield, hatch, seat, ladder, face, anime, animal, crowd, "
       "scene, environment, landscape, city, buildings, terrain, sky, clouds, explosions, blurry, text, watermark, "
       "signature, letters, frame, border, many vehicles, two vehicles, several views, sprite sheet, lineup, cropped, "
       "cut off, rust, dieselpunk, steampunk, brass, flag, star, world war, camouflage")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 7
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 22)
pg.start_server()
S = 384
sheet = Image.new("RGB", (60 + S * len(UNITS), 44 + S * len(STYLES)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "BATTLER: MACHINE DESIGN LANGUAGES (no crew)  -  pick a row, or mix", font=font, fill=(255, 240, 200))
for r, (skey, sname, look) in enumerate(STYLES):
    d.text((8, 44 + r * S + S // 2 - 12), sname[0], font=font, fill=(255, 255, 255))
    for c, (ukey, uname, what) in enumerate(UNITS):
        path = os.path.join(OUT, "machine_%s_%s_s%d.png" % (skey, ukey, seed))
        if os.path.exists(path):      # (kept from a run that was stopped)
            shot = Image.open(path).convert("RGB")
        else:
            g = pg.workflow(PROMPT.format(what=what, look=look), 1024, 1024, seed + r * 10 + c, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        x, y = 60 + c * S, 44 + r * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%s  %s" % (sname, uname)
        d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
        print("done", skey, ukey, flush=True)
sheet.save(os.path.join(OUT, "machine_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
