"""Repaints Battler models' flat views with ComfyUI: paint_units.py <name> ... [--stray 0.5] [--seed 5] [--flux]
The whole paint pass for one model:
  1. blender_views.py (ai-tools) renders art/units/<name>.glb straight on -> art/_gen/ships_iso/paint/<name>_<view>.png
  2. this: each view cropped to the model, enlarged so SDXL has room for detail, repainted as worn dieselpunk armour
     (not far from what it was given), shrunk back -> <name>_<view>_paint.png
  3. paint_model.py (ai-tools) --folder art/units projects the repaints onto <name>.png (the old one kept as _plain).
Run with ComfyUI's Python. About 25 s a view."""
import os, sys, subprocess
from PIL import Image
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DIR = os.path.join(ROOT, "art", "_gen", "ships_iso", "paint")
BLENDER = r"C:\dev\blender-4.5.14-windows-x64\blender.exe"
TOOLS = r"C:\dev\ai-tools\blender"
LOOK = {"top": "seen from directly above", "side": "side view", "front": "front view", "back": "rear view"}
WORDS = ("{look} of a dieselpunk military {what}, heavy riveted olive drab steel armour plates, weld seams, bolts, "
         "access hatches, grilles, chipped paint showing bare steel on the edges, soot, oil and dust stains, faded "
         "stencil markings, orthographic, flat even lighting, crisp, high detail game texture")
NEG = ("blurry, soft, smooth, plastic, toy, cartoon, text, watermark, background scenery, ground, person, crew, face, "
       "perspective, cast shadow, new parts, extra barrels, glowing, sci-fi, spaceship, camouflage blotches")
WHAT = {"hq": "headquarters bunker", "factory": "vehicle workshop", "extractor": "oil pumpjack", "powerplant": "diesel power plant",
        "turret": "gun emplacement", "flak": "anti-aircraft gun", "bombard": "heavy mortar emplacement", "wall": "armoured wall",
        "outpost": "field outpost", "flier": "propeller fighter plane", "bomber": "propeller bomber plane", "scout": "armoured car",
        "ranger": "armoured car with a machine gun", "mobilebase": "big armoured truck", "artillery": "self-propelled howitzer",
        "mortar": "mortar carrier", "lighttank": "light war machine with an open girder frame on six tall spoked wheels, an engine with a big flywheel in front and a swivel cannon on an armoured box", "tank": "riveted steel drum war machine on four huge treaded wheels"}

args = sys.argv[1:]


def opt(name, default, kind=float):
    if name in args:
        i = args.index(name)
        v = kind(args[i + 1])
        del args[i:i + 2]
        return v
    return default


stray, seed = opt("--stray", 0.5), int(opt("--seed", 5))
use_flux = "--flux" in args          # (FLUX schnell repaints instead of SDXL: cleaner, more believable metal)
if use_flux:
    args.remove("--flux")
FLUX_WORDS = ("{look}, straight on, of a {what}. Practical 1940s diesel-era engineering painted olive drab: clean "
              "welded steel plates, rivets, bolts, hinges, worn chipped paint edges showing bare steel, light dust "
              "and soot. Flat even lighting, sharp, orthographic, plain dark background. No text.")
env = dict(os.environ, ART_PROJECT=ROOT)
pg.start_server()
for name in args:
    subprocess.run([BLENDER, "--background", "--python", os.path.join(TOOLS, "blender_views.py"), "--",
                    name, os.path.join(ROOT, "art", "units"), "1024"], env=env, capture_output=True, check=True)
    what = next((v for k, v in WHAT.items() if name.startswith(k) and (k != "factory" or "_" in name)), "war machine")
    if name.startswith("factory"):
        what = "vehicle workshop"
    for view, look in LOOK.items():
        src = Image.open(os.path.join(DIR, "%s_%s.png" % (name, view))).convert("RGBA")
        box = src.getchannel("A").getbbox()
        if not box:
            continue
        pad = 24
        box = (max(0, box[0] - pad), max(0, box[1] - pad), min(src.width, box[2] + pad), min(src.height, box[3] + pad))
        crop = src.crop(box)
        scale = 1024.0 / max(crop.size)
        w, h = [max(256, int(round(v * scale / 64.0)) * 64) for v in crop.size]
        canvas = Image.new("RGB", crop.size, (58, 60, 70))
        canvas.paste(crop, (0, 0), crop)
        init = os.path.join(DIR, "_init.png")
        canvas.resize((w, h), Image.Resampling.LANCZOS).save(init)
        if use_flux:
            g = fluxgen.graph(FLUX_WORDS.format(look=look[0].upper() + look[1:], what=what), seed, w, h, steps=8)
            g["init"] = {"class_type": "LoadImage", "inputs": {"image": pg.stage_init(init)}}
            g["latent"] = {"class_type": "VAEEncode", "inputs": {"pixels": ["init", 0], "vae": ["vae", 0]}}
            g["sample"]["inputs"]["denoise"] = stray
        else:
            g = pg.workflow(WORDS.format(look=look, what=what), w, h, seed, 30, 0.0, False, init=pg.stage_init(init), denoise=stray)
            g["neg"]["inputs"]["text"] = NEG
        out = pg.generate(g).convert("RGBA").resize(crop.size, Image.Resampling.LANCZOS)
        out.putalpha(crop.getchannel("A"))
        full = Image.new("RGBA", src.size, (0, 0, 0, 0))
        full.paste(out, box[:2])
        full.save(os.path.join(DIR, "%s_%s_paint.png" % (name, view)))
        print(name, view, "painted", flush=True)
