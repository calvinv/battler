"""Ground textures painted by ComfyUI: ground_textures.py [--seeds 3] [--install dirt=1,mud=2,...]
Top-down soil (dirt, mud, gravel, scorched), made seamless so they tile, laid on a sheet to pick from.
-> art/_gen/ground/<name>_s<seed>.png and ground_sheet.png; --install copies picks to art/ground/<name>.png
(512, what game/ground_fog.gdshader samples). About 25 s a picture."""
import os, sys, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "art", "_gen", "ground")
LIVE = os.path.join(ROOT, "art", "ground")

GROUNDS = [
    ("dirt", "dry packed brown earth, dust, small scattered stones, fine dry cracks, clods"),
    ("mud", "dark churned damp earth, wet mud clods and ruts, puddle-dark patches"),
    ("gravel", "grey gravel and broken rubble scattered over brown dirt, pebbles and small rocks"),
    ("scorched", "scorched blackened earth, ash, soot, charred clods and small burnt debris"),
]
PROMPT = ("seamless tileable ground texture, straight top-down orthographic photo of {what}, flat even overcast "
          "lighting, no shadows from objects, uniform coverage edge to edge, high detail, sharp, game texture")
NEG = ("perspective, horizon, sky, plants, grass, leaves, people, animals, objects, vehicles, buildings, text, "
       "watermark, frame, border, vignette, strong shadows, blurry, cartoon, painting")


def seamless(img):
    """Wraps the picture so it tiles: the edges come from its middle (rolled by half), blended in by a soft
    noisy mask so no straight seam or ghost band shows."""
    a = np.asarray(img, dtype=np.float32)
    h, w = a.shape[:2]
    b = np.roll(a, (h // 2, w // 2), axis=(0, 1))
    y, x = np.mgrid[0:h, 0:w]
    edge = np.minimum(np.minimum(x, w - 1 - x) / (w / 2), np.minimum(y, h - 1 - y) / (h / 2))   # 0 at the edge, 1 mid
    rng = np.random.default_rng(3)
    noise = np.asarray(Image.fromarray((rng.random((h // 32, w // 32)) * 255).astype(np.uint8))
                       .resize((w, h), Image.Resampling.BICUBIC), dtype=np.float32) / 255
    m = np.clip((edge + (noise - 0.5) * 0.35 - 0.3) / 0.25, 0, 1)[..., None]
    return Image.fromarray((a * m + b * (1 - m)).clip(0, 255).astype(np.uint8))


def flatten(img):
    """Takes out the light's slow drift so the tile reads the same everywhere (the shader adds its own)."""
    a = np.asarray(img, dtype=np.float32)
    blur = np.asarray(img.filter(ImageFilter.GaussianBlur(96)), dtype=np.float32)
    mean = a.reshape(-1, 3).mean(0)
    return Image.fromarray((a / np.maximum(blur, 1) * mean).clip(0, 255).astype(np.uint8))


args = sys.argv[1:]
if "--install" in args:
    os.makedirs(LIVE, exist_ok=True)
    for pick in args[args.index("--install") + 1].split(","):
        name, seed = pick.split("=")
        src = Image.open(os.path.join(OUT, "%s_s%s.png" % (name, seed))).convert("RGB")
        src.resize((512, 512), Image.Resampling.LANCZOS).save(os.path.join(LIVE, name + ".png"))
        print("installed", name, seed)
    sys.exit()

seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 3
os.makedirs(OUT, exist_ok=True)
pg.start_server()
S = 256
sheet = Image.new("RGB", (S * 2 * seeds, 40 + S * 2 * len(GROUNDS) // 2 * 1 + 0), (14, 14, 20))
sheet = Image.new("RGB", (S * 2 * seeds, 40 + (S * 2 + 30) * len(GROUNDS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
font = ImageFont.truetype("arialbd.ttf", 20)
d.text((10, 8), "GROUND TEXTURES (each shown 2x2 to check the tiling)", font=font, fill=(255, 240, 200))
for r, (name, what) in enumerate(GROUNDS):
    for s in range(1, seeds + 1):
        path = os.path.join(OUT, "%s_s%d.png" % (name, s))
        if not os.path.exists(path):
            g = pg.workflow(PROMPT.format(what=what), 1024, 1024, 100 * r + s, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG
            seamless(flatten(pg.generate(g).convert("RGB"))).save(path)
            print("made", path, flush=True)
        tile = Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS)
        x, y = (s - 1) * S * 2, 40 + r * (S * 2 + 30)
        d.text((x + 6, y + 4), "%s %d" % (name, s), font=font, fill=(255, 240, 200))
        for i in range(2):
            for j in range(2):
                sheet.paste(tile, (x + i * S, y + 28 + j * S))
sheet.save(os.path.join(OUT, "ground_sheet.png"))
print("sheet", os.path.join(OUT, "ground_sheet.png"))
