"""Bird-leg mech, round 2, from the user's pick mech_chicken_0_s401: mech_round2.py [--seed 501]
(The user, 2026-10-23: "bird legs is the best, I prefer the cartoonish look of a, it almost has eyes, maybe try again
with more prototypes based off a".) Row 1: a itself repainted by FLUX (same shape, small changes, denoise rising).
Rows 2-3: new takes on it in words, each pushing one thing (the eyes, the feet, the rifle, the proportions).
-> art/_gen/concepts/mech2_<key>_s<seed>.png and mech2_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
SOURCE = os.path.join(OUT, "mech_chicken_0_s401.png")
BASE = ("A small cute cartoonish unmanned walker robot for a strategy game, big in the picture: a boxy dark slate grey "
        "body like a head, with two round glowing amber lamps on its front like a pair of eyes, standing on two "
        "backward-bending bird legs with wide splayed three-toed feet, a long rifle slung under the body pointing "
        "forward. Small thin orange stripes on its top corners. {twist}")
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole robot "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people.")
VARIANTS = [
    ("repaint_a", "A, LIGHT TOUCH", None, 0.45),
    ("repaint_b", "A, MORE EYES", "Its two eye lamps are bigger and brighter.", 0.55),
    ("repaint_c", "A, FRESHER", None, 0.65),
    ("repaint_d", "A, LOOSER", "Its two eye lamps are bigger and brighter.", 0.75),
    ("eyes", "BIG EYES", "The two eye lamps are big, round and expressive, with dark visor plates over them like brows.", None),
    ("tilt", "CURIOUS", "Its body is tilted a little as if curious, one foot lifted mid-step.", None),
    ("stubby", "STUBBY", "Its body is a chunky rounded cube, its legs short and thick, like a toy.", None),
    ("tall", "LANKY", "Its legs are long and thin like a heron's, the body small on top.", None),
    ("rifle", "BIG RIFLE", "Its rifle is oversized, longer than the robot, with a drum magazine and a scope.", None),
    ("antenna", "ANTENNA", "It has a single whip antenna with a tiny orange light on top, and a small backpack.", None),
    ("visor", "ONE VISOR", "Instead of two lamps it has one wide glowing amber visor slit across its front.", None),
    ("twin", "TWIN GUNS", "It carries two short rifles, one under each side of the body.", None),
]

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 501
pg.start_server()
S, COLS = 300, 4
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (S * COLS, 40 + S * ((len(VARIANTS) + COLS - 1) // COLS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "BIRD-LEG MECH, ROUND 2 (from a): top row repaints of a, then new takes", font=font, fill=(255, 240, 200))
init = None
for n, (key, label, twist, denoise) in enumerate(VARIANTS):
    path = os.path.join(OUT, "mech2_%s_s%d.png" % (key, seed))
    if not os.path.exists(path):
        prompt = BASE.format(twist=twist or "") + " " + STYLE
        if denoise:
            init = init or pg.stage_init(SOURCE)
            g = fluxgen.graph(prompt, seed + n, 1024, 1024, steps=8)
            g["init"] = {"class_type": "LoadImage", "inputs": {"image": init}}
            g["latent"] = {"class_type": "VAEEncode", "inputs": {"pixels": ["init", 0], "vae": ["vae", 0]}}
            g["sample"]["inputs"]["denoise"] = denoise
            img = pg.generate(g)
        else:
            img = fluxgen.image(prompt, seed + n)
        img.convert("RGB").save(path)
        print("done", key, flush=True)
    x, y = (n % COLS) * S, 40 + (n // COLS) * S
    sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
    tag = "%d %s" % (n + 1, label)
    d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
    d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "mech2_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "mech2_sheet_s%d.png" % seed)))
