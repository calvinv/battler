"""Simple futuristic builds in the cel shaded / pixel look (as Voidwake's art): hover_concepts.py [--seed 101] [--unit lighttank]
(The user, 2026-10-23: "the shell shaded look we used in void wake ... simple but futuristic builds. Starting with a
light tank: a rectangular base that hovers above the ground, a turret at the back about as tall as the base and a
longish barrel that shoots a laser; generate some simple pixel art off the idea".)
A row per way of painting it (SDXL + pixel LoRA, SDXL cel shaded, FLUX pixel art), a column per try.
-> art/_gen/concepts/hover_<unit>_<way>_<try>_s<seed>.png and hover_<unit>_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
UNITS = {
    "lighttank": "a small futuristic hover tank: a flat rectangular hull floating a little above the ground on a soft "
                 "glow, no wheels or tracks, a low boxy turret at the back of the hull about as tall as the hull, a "
                 "long thin laser cannon barrel reaching forward past the front, simple clean shapes",
}
LOOK = "white and light grey armour panels with blue team stripes and a glowing cyan laser tip"
SDXL_PROMPT = ("{style}, one single {what}, ({look}:1.1), seen from above at a three-quarter angle, isolated on a "
               "plain flat dark background, centred, the whole vehicle in frame, game unit sprite")
NEG = ("wheels, tracks, legs, people, pilot, cockpit glass, windows, text, watermark, letters, frame, border, ground "
       "scene, landscape, buildings, several vehicles, sprite sheet, blurry, photo, realistic, cropped")
WAYS = [
    ("pix", "SDXL + PIXEL LORA", "pixel art, clean pixel art game sprite, limited palette, crisp pixels, dark outlines", 0.85),
    ("cel", "SDXL CEL SHADED", "cel shaded game concept art, flat colours in clean bands, crisp dark ink outlines, hard edges", 0.0),
    ("flux", "FLUX PIXEL ART", None, None),
]

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 101
unit = args[args.index("--unit") + 1] if "--unit" in args else "lighttank"
what = UNITS[unit]
os.makedirs(OUT, exist_ok=True)
pg.start_server()
TRIES, S = 4, 320
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (170 + S * TRIES, 40 + S * len(WAYS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "HOVER LIGHT TANK: SIMPLE FUTURISTIC, CEL / PIXEL LOOK", font=font, fill=(255, 240, 200))
for r, (way, label, style, lora) in enumerate(WAYS):
    d.text((10, 40 + r * S + S // 2 - 10), label, font=font, fill=(255, 255, 255))
    for c in range(TRIES):
        path = os.path.join(OUT, "hover_%s_%s_%d_s%d.png" % (unit, way, c, seed))
        if not os.path.exists(path):
            if way == "flux":
                prompt = ("Pixel art game sprite of %s. %s. Seen from above at a three-quarter angle, isolated on a plain "
                          "flat dark background, centred, whole vehicle in frame. Clean retro pixel art with a limited "
                          "palette, crisp hard-edged pixels, flat cel shading in two or three tones, dark outlines. No "
                          "text, no people." % (what[0].upper() + what[1:], LOOK[0].upper() + LOOK[1:]))
                img = fluxgen.image(prompt, seed + c)
            else:
                g = pg.workflow(SDXL_PROMPT.format(style=style, what=what, look=LOOK), 1024, 1024, seed + c, 30, lora, False)
                g["neg"]["inputs"]["text"] = NEG
                img = pg.generate(g)
            img.convert("RGB").save(path)
            print("done", way, c, flush=True)
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.NEAREST if way != "cel" else Image.Resampling.LANCZOS),
                    (170 + c * S, 40 + r * S))
sheet.save(os.path.join(OUT, "hover_%s_sheet_s%d.png" % (unit, seed)))
print("sheet", os.path.abspath(os.path.join(OUT, "hover_%s_sheet_s%d.png" % (unit, seed))))
