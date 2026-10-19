"""Nuclearpunk style directions for Battler's army: nuclear_style_concepts.py [--seed 7]
(The user, 2026-10-19, after the steampunk sheet (vehicle_style_concepts.py): "im thinking more diesel punk but like
more modernised with nuclear power, maybe nuclearpunk??". The world stays: post-apocalypse, machines took over and fight
for resources.) Six takes on reactor-powered dieselpunk war machines, each shown as a tank and a bomber. No crews:
sensors, not cockpits. Real references: the Chrysler TV-8 nuclear tank and the Convair NB-36H nuclear bomber.
Every look must be buildable as a flat-sided low-poly model. Pixel LoRA 0.
-> art/_gen/concepts/nstyle_<key>_<subject>_s<seed>.png and nstyle_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
SUBJECTS = [
    ("tank", "a heavy nuclear powered battle tank with tracks and a big turret with a long cannon, a shielded reactor "
     "housing on its hull, parked on its own"),
    ("bomber", "a huge nuclear powered heavy bomber aircraft with broad wings and big engines, a shielded reactor in "
     "its fuselage, flying on its own"),
]
WORLD = ("unmanned self-aware war machine, no crew, no cockpit windows, glowing sensor eyes, post-apocalyptic, grimy, "
         "weathered, dieselpunk, atomic age, nuclearpunk")
STYLES = [
    ("atomic", "ATOMIC AGE RETRO", "1950s retro-futurism, rounded streamlined bodywork, faded chrome trim, tail fins, "
     "cream and red paint chipped to rust, Fallout style, painterly concept art"),
    ("coldwar", "COLD WAR HEAVY", "massive slab-sided Soviet cold war armour, dark olive and grey, radiation trefoil "
     "warnings, cooling fins, stencilled serial numbers, brutal and heavy, painterly concept art"),
    ("retrofit", "DIESEL + REACTOR RETROFIT", "a 1940s diesel war machine retrofitted with a bolted-on glowing reactor "
     "core, thick cables and coolant pipes, exhaust stacks, riveted olive steel, oil stains, painterly concept art"),
    ("glow", "LEAKING GLOW", "rusted dark steel with cracked reactor shielding leaking bright green radioactive glow, "
     "corrosion, hazard stripes, irradiated wasteland machine, painterly concept art"),
    ("prototype", "TEST PROGRAMME", "an experimental 1950s atomic test programme prototype, white and international "
     "orange test markings faded and dirty, exposed reactor domes, instrument booms, painterly concept art"),
    ("inked", "NUCLEAR CEL + INK", "grimy olive steel with yellow radiation hazard markings and cyan reactor glow, "
     "(flat cel shading, thick black ink outlines, comic book style:1.2), flat colours in hard bands, bold shapes"),
]
PROMPT = ("war machine design concept art, one single vehicle seen whole, isolated on a plain flat pale grey "
          "background, nothing around it, (it is {what}:1.15), ({world}:1.1), ({look}:1.2), seen from above at a "
          "three-quarter angle, centred, the whole vehicle in frame, game concept art, high detail")
NEG = ("mech, robot, legs, walker, people, soldiers, pilot, crew, human, face, crowd, scene, environment, landscape, "
       "city, buildings, terrain, ground, sky, clouds, explosions, mushroom cloud, blurry, text, watermark, signature, "
       "letters, frame, border, many vehicles, two vehicles, several views, sprite sheet, cropped, cut off, "
       "steampunk, brass, clockwork, gears, us army star, roundel, flag")

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
d.text((14, 8), "BATTLER: NUCLEARPUNK WAR MACHINES  (pick a number, mix freely)", font=font, fill=(255, 240, 200))
for n, (key, name, look) in enumerate(STYLES):
    for v, (subject, what) in enumerate(SUBJECTS):
        path = os.path.join(OUT, "nstyle_%s_%s_s%d.png" % (key, subject, seed))
        if os.path.exists(path):      # (kept from a run that was stopped)
            shot = Image.open(path).convert("RGB")
        else:
            g = pg.workflow(PROMPT.format(what=what, world=WORLD, look=look), 1024, 1024, seed + n * 10 + v, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        cell = n * 2 + v
        x, y = (cell % 4) * S, 44 + (cell // 4) * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%d  %s" % (n + 1, name)
        d.text((x + 11, y + 9), label, font=font, fill=(0, 0, 0))
        d.text((x + 10, y + 8), label, font=font, fill=(255, 255, 255))
        print("done", key, subject, flush=True)
sheet.save(os.path.join(OUT, "nstyle_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
