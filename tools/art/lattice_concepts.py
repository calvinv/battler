"""Concept vehicles for the Lattice's tier 1: lattice_concepts.py [--seed 11]
(The user, 2026-10-22: the spider bots "need a lot of work... I dont think they need lots of spider bots either, lets
come up with concept vehicles for lattice".) A row per role, a column per way of getting about, all in the Lattice's
look (welded scrap, rust and bone, clusters of amber eyes), so the roster isn't all legs. Machines only, no crew.
-> art/_gen/concepts/lattice_<role>_<take>_s<seed>.png and lattice_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

LOOK = ("cobbled together from mismatched scrap metal plates welded over an exposed girder frame, rust orange and "
        "bone white paint worn through to bare metal, clusters of small glowing amber sensor eyes, salvaged parts, "
        "machine-made, post-apocalyptic, grounded military sci-fi like Supreme Commander")
ROLES = [
    ("swarm", "SWARM RAIDER", "a small cheap fast raider with a (short stubby flamer gun:1.1), built to attack in swarms"),
    ("skirm", "SKIRMISHER", "a light combat vehicle with a (small turret with a twin laser:1.2)"),
    ("anti", "ANTI-ARMOUR", "a tank hunter with (one very long heavy spike cannon laid along its body:1.25)"),
    ("scout", "SCOUT FLIER", "a small (flying scout drone with rotors:1.25), a big sensor eye underneath"),
]
TAKES = [
    ("wheels", "SCRAP WHEELS", "riding low on (six mismatched chunky salvaged wheels:1.25), a dune buggy stance"),
    ("segments", "SEGMENTED", "a (long low segmented body like a centipede, each segment on its own pair of small wheels:1.25)"),
    ("halftrack", "HALF-TRACK", "(a half-track: big scrap wheels at the front and a single crude track at the back:1.25)"),
]
SCOUT_TAKES = [   # the flier's row: three ways to hang in the air instead
    ("quad", "QUAD ROTOR", "(four ducted rotors on welded scrap arms:1.2)"),
    ("blimp", "GAS BAG", "(a patched gas bag with a small engine pod slung under it:1.25)"),
    ("tilt", "TILT ROTOR", "(two big tilting rotor rings either side of a thin scrap fuselage:1.25)"),
]
PROMPT = ("futuristic RTS game concept art, one single unmanned war machine seen whole, isolated on a plain flat pale "
          "grey background, nothing around it, (it is {what}:1.15), {how}, ({look}:1.15), (no crew, no humans, no "
          "cockpit, no windows:1.1), seen from above at a three-quarter angle, centred, the whole thing in frame, "
          "high detail")
NEG = ("people, human, pilot, crew, cockpit, windows, hatch, face, anime, animal, insect, spider, crowd, scene, "
       "landscape, city, sky, clouds, explosions, blurry, text, watermark, letters, frame, border, several views, "
       "sprite sheet, lineup, cropped, cut off, steampunk, brass, flag, star, clean, shiny, white armour, {avoid}")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 11
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 22)
pg.start_server()
S = 448
sheet = Image.new("RGB", (S * 3, 44 + S * len(ROLES)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "LATTICE TIER 1: CONCEPT VEHICLES", font=font, fill=(255, 240, 200))
for r, (rkey, rname, what) in enumerate(ROLES):
    for c, (tkey, tname, how) in enumerate(SCOUT_TAKES if rkey == "scout" else TAKES):
        path = os.path.join(OUT, "lattice_%s_%s_s%d.png" % (rkey, tkey, seed))
        if os.path.exists(path):
            shot = Image.open(path).convert("RGB")
        else:
            g = pg.workflow(PROMPT.format(what=what, how=how, look=LOOK), 1024, 1024, seed + r * 10 + c, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG.format(avoid="legs, walker" if rkey != "scout" else "wheels, tracks, legs")
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        x, y = c * S, 44 + r * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%s  %s" % (rname, tname)
        d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
        print("done", rkey, tkey, flush=True)
sheet.save(os.path.join(OUT, "lattice_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
