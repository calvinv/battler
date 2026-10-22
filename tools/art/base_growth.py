"""A base growing from the middle out, painted over drawn layouts: base_growth.py [--seed 51] [--stray 0.68] [--size 1536] [--guide 0.8]
Each stage is first drawn as plain shaded blocks seen from above (the same HQ in the middle every time; wings, pipes,
extractors, plants and defences spreading out), then ComfyUI repaints the drawing as a dieselpunk base, not straying
far, so the stages line up. -> art/_gen/concepts/growth_<stage>_s<seed>.png (+ _layout) and growth_sheet_s<seed>.png."""
import os, sys, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
N = 1024
C = N // 2
DIRT, ROAD, PIPE = (122, 98, 70), (92, 82, 70), (70, 66, 60)
OLIVE, ROOF, BLUE, STEEL, DARK = (98, 104, 72), (78, 84, 58), (60, 96, 170), (120, 120, 112), (46, 44, 40)


def box(d, cx, cy, w, h, ang, top, side=None, height=14):
    """A building seen from above, turned by ang (degrees), with its south-east wall showing as a dark band."""
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    pts = [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]
    d.polygon([(x + height * 0.6, y + height) for x, y in pts], fill=(30, 26, 22))        # shadow
    d.polygon(pts, fill=side or DARK)
    d.polygon([(x, y - height * 0.5) for x, y in pts], fill=top)
    return pts


def stripes(d, cx, cy, w, h, ang, n, col):
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    for i in range(n):
        y = -h / 2 + (i + 0.5) * h / n
        a = (cx + (-w / 2 + 6) * ca - y * sa, cy + (-w / 2 + 6) * sa + y * ca - 7)
        b = (cx + (w / 2 - 6) * ca - y * sa, cy + (w / 2 - 6) * sa + y * ca - 7)
        d.line([a, b], fill=col, width=3)


def tank(d, x, y, r, col):
    d.ellipse((x - r + 8, y - r + 10, x + r + 8, y + r + 10), fill=(30, 26, 22))
    d.ellipse((x - r, y - r, x + r, y + r), fill=col)
    d.ellipse((x - r * 0.6, y - r * 0.6, x + r * 0.6, y + r * 0.6), outline=DARK, width=3)


def wing(d, ang, dist, w=90, h=150):
    x, y = C + math.cos(math.radians(ang)) * dist, C + math.sin(math.radians(ang)) * dist
    d.line([(C, C), (x, y)], fill=ROAD, width=34)
    box(d, x, y, h, w, ang, ROOF, height=18)
    stripes(d, x, y, h, w, ang + 90, 4, (60, 64, 44))


def outpost(d, ang, dist, kind):
    """A resource building out along a pipe from the HQ, with its own two wings."""
    x, y = C + math.cos(math.radians(ang)) * dist, C + math.sin(math.radians(ang)) * dist
    d.line([(C, C), (x, y)], fill=PIPE, width=10)
    d.line([(C, C), (x, y)], fill=STEEL, width=4)
    for side in (-1, 1):
        a = ang + side * 90
        wx, wy = x + math.cos(math.radians(a)) * 95, y + math.sin(math.radians(a)) * 95
        d.line([(x, y), (wx, wy)], fill=ROAD, width=24)
        box(d, wx, wy, 70, 100, a, ROOF, height=14)
        stripes(d, wx, wy, 100, 70, a + 90, 3, (60, 64, 44))
    if kind == "oil":
        box(d, x, y, 70, 70, ang, OLIVE, height=10)
        d.line([(x - 30, y + 10), (x + 30, y - 30)], fill=DARK, width=9)       # pumpjack beam
        tank(d, x + 45, y + 30, 18, STEEL)
    else:
        box(d, x, y, 90, 80, ang, OLIVE, height=16)
        for k in range(3):
            tank(d, x - 25 + k * 25, y - 20, 10, DARK)                         # stacks


def gun(d, x, y):
    d.ellipse((x - 26, y - 26, x + 26, y + 26), fill=(150, 130, 96))           # sandbags
    d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=OLIVE)
    d.line([(x, y), (x + 30, y - 18)], fill=DARK, width=7)


def layout(stage):
    img = Image.new("RGB", (N, N), DIRT)
    d = ImageDraw.Draw(img)
    if stage >= 3:
        outpost(d, 200, 330, "oil")
        outpost(d, 20, 330, "power")
    if stage >= 4:
        outpost(d, 110, 330, "oil")
        outpost(d, 290, 330, "power")
        for a in range(0, 360, 45):
            gun(d, C + math.cos(math.radians(a + 22)) * 450, C + math.sin(math.radians(a + 22)) * 450)
    if stage >= 2:
        for a in (90 - 60, 90 + 60, 270):
            wing(d, a + (40 if stage >= 3 else 0), 150)
    box(d, C, C, 150, 150, 0, OLIVE, height=26)                                # HQ
    box(d, C, C - 14, 80, 80, 0, BLUE, side=OLIVE, height=16)
    for k in range(3):
        tank(d, C - 50 + k * 22, C - 70, 9, DARK)                              # chimneys
    if stage >= 2:
        for a in (150, 330):
            tank(d, C + math.cos(math.radians(a)) * 110, C + math.sin(math.radians(a)) * 110, 16, STEEL)   # fuel tanks
    return img.filter(ImageFilter.GaussianBlur(1.2))


STAGES = [(1, "1  HQ ALONE"), (2, "2  THREE WINGS"), (3, "3  OUT TO OIL + POWER"), (4, "4  GROWN OUT, RING OF GUNS")]
PROMPT = ("RTS game concept art, straight top-down aerial view of an unmanned automaton war factory that builds "
          "itself, sprawling out from the middle across bare brown dirt, gritty dieselpunk, a furnace core in the middle "
          "with smokestacks and a blue team-coloured roof, {extra}, riveted welded steel, girders, conveyors, soot, rust, "
          "oil stains, tracks in the dirt, no people, chunky readable shapes, painterly realism, high detail")
EXTRA = {1: "just the core, scaffolds and a crane starting to build",
         2: "three assembly halls budding off the core on short conveyors, strange machines rolling out of their bays",
         3: "assembly halls around it, pipelines running out to an oil pump and a smoking power plant that grow halls of their own, cranes building at the tips",
         4: "rings of assembly halls, pipelines out in every direction to pumps and power plants, conveyors carrying new war machines outward, gun nests in a ring around the edge"}
NEG = ("people, soldiers, text, watermark, letters, frame, border, perspective, horizon, sky, grass, trees, water, "
       "city, houses, blurry, cartoon, sci-fi, glowing, steampunk, brass")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 51
stray = float(args[args.index("--stray") + 1]) if "--stray" in args else 0.68
guide = float(args[args.index("--guide") + 1]) if "--guide" in args else 0.0   # canny control strength (0: repaint the drawing instead)
size = int(args[args.index("--size") + 1]) if "--size" in args else 1024     # painted this big (the layout enlarged): more detail per building
os.makedirs(OUT, exist_ok=True)
pg.start_server()
S = 384
font = ImageFont.truetype("arialbd.ttf", 22)
sheet = Image.new("RGB", (S * 4, 44 + S * 2), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "A BASE GROWING FROM THE MIDDLE OUT  (top: the drawn layout, bottom: painted)", font=font, fill=(255, 240, 200))
for c, (stage, label) in enumerate(STAGES):
    lay = layout(stage)
    lay_path = os.path.join(OUT, "growth_%d_layout.png" % stage)
    lay.resize((size, size), Image.Resampling.LANCZOS).save(lay_path)
    path = os.path.join(OUT, "growth_%d_s%d.png" % (stage, seed))
    if not os.path.exists(path):
        g = pg.workflow(PROMPT.format(extra=EXTRA[stage]), size, size, seed, 30, 0.0, False, init=pg.stage_init(lay_path), denoise=stray)
        g["neg"]["inputs"]["text"] = NEG
        if guide:      # strays far from the drawing, held to its outlines by the canny control-LoRA
            g["edges"] = {"class_type": "Canny", "inputs": {"image": ["init", 0], "low_threshold": 0.05, "high_threshold": 0.15}}
            g["cn"] = {"class_type": "ControlNetLoader", "inputs": {"control_net_name": "control-lora-canny-rank128.safetensors"}}
            g["apply"] = {"class_type": "ControlNetApplyAdvanced", "inputs": {
                "positive": ["pos", 0], "negative": ["neg", 0], "control_net": ["cn", 0], "image": ["edges", 0],
                "strength": guide, "start_percent": 0.0, "end_percent": 0.75}}
            g["sample"]["inputs"]["positive"] = ["apply", 0]
            g["sample"]["inputs"]["negative"] = ["apply", 1]
        pg.generate(g).convert("RGB").save(path)
        print("done", stage, flush=True)
    for r, im in enumerate((lay, Image.open(path).convert("RGB"))):
        x, y = c * S, 44 + r * S
        sheet.paste(im.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        if r:
            d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
            d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "growth_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "growth_sheet_s%d.png" % seed)))
