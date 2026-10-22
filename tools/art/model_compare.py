"""The same subjects through three ComfyUI models, side by side: model_compare.py [--seed 81] [--only flux]
SDXL base (what the concept sheets used), Juggernaut XL v9 (photographic SDXL) and FLUX.1 schnell (GGUF Q4, follows
plain-English prompts closely, Apache licence). Asked for practical, built-for-real machines rather than concept art.
-> art/_gen/concepts/cmp_<model>_<key>_s<seed>.png and compare_sheet_s<seed>.png (a row per model).
Needs ComfyUI-GGUF in custom_nodes and the model files in models/ (checkpoints, unet, text_encoders, vae)."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

SUBJECTS = [
    ("drum", "GUN DRUM", "an unmanned armoured war machine whose body is a big riveted steel drum mounted on a "
                         "six-wheeled bogie chassis, a short cannon sticking out of the front of the drum, a diesel "
                         "exhaust stack on top"),
    ("strider", "PISTON STRIDER", "an unmanned war machine with a riveted steel furnace body carried on four hydraulic "
                                  "piston legs with flat feet, a machine gun on the front, hoses and pipes"),
    ("crab", "GIRDER CRAB", "an unmanned war machine built from a diesel engine block and a fuel tank on a wide "
                            "girder frame with four big spoked steel wheels, a cannon on a swivel mount on top"),
    ("hive", "HIVE BASE", "an aerial view of an unmanned automated war factory: a central domed furnace building with "
                          "smokestacks, surrounded by rings of small hexagonal steel assembly sheds added outward, "
                          "the outermost still being built in scaffolding, conveyors leading out of it"),
    ("web", "WEB BASE", "an aerial view of an unmanned automated war factory in the desert: a central furnace core with "
                        "long conveyor lines running outward like spokes to smaller assembly sheds and oil pumps, "
                        "small vehicles leaving along the conveyors"),
]
STYLE = ("Practical and believable, as if really built by engineers from off-the-shelf industrial parts: welded steel "
         "plate, rivets, bolts, hydraulic rams, hoses, worn olive drab paint, dust, oil and soot, a few amber lamps, "
         "blue team markings. 1940s diesel-era engineering. Photographed in daylight, clear and sharp, the whole "
         "thing in view from a high three-quarter angle, on plain bare dirt. No people.")
NEG = ("people, human, soldier, crew, face, cartoon, anime, painting, concept art sketch, fantasy, steampunk, brass, "
       "sci-fi, glowing, text, watermark, frame, several views, cropped, blurry")


def sdxl(ckpt, prompt, seed):
    g = pg.workflow(prompt, 1024, 1024, seed, 30, 0.0, False)
    g["ckpt"]["inputs"]["ckpt_name"] = ckpt
    g["neg"]["inputs"]["text"] = NEG
    if "juggernaut" in ckpt:
        g["sample"]["inputs"].update({"cfg": 4.5, "sampler_name": "dpmpp_2m", "scheduler": "karras"})
    return g


def flux(prompt, seed):
    return {
        "unet": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "flux1-schnell-Q4_K_S.gguf"}},
        "clip": {"class_type": "DualCLIPLoader", "inputs": {"clip_name1": "t5xxl_fp8_e4m3fn.safetensors",
                                                            "clip_name2": "clip_l.safetensors", "type": "flux"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": "flux_ae.safetensors"}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["clip", 0]}},
        "neg": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["pos", 0]}},
        "latent": {"class_type": "EmptySD3LatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
        "sample": {"class_type": "KSampler", "inputs": {
            "model": ["unet", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0],
            "seed": seed, "denoise": 1.0, "steps": 4, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple"}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sample", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": "flux"}},
    }


MODELS = [("sdxl", "SDXL BASE", lambda p, s: sdxl("sd_xl_base_1.0.safetensors", p, s)),
          ("jugg", "JUGGERNAUT XL", lambda p, s: sdxl("juggernaut_xl_v9.safetensors", p, s)),
          ("flux", "FLUX SCHNELL", flux)]

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 81
only = args[args.index("--only") + 1] if "--only" in args else None
os.makedirs(OUT, exist_ok=True)
pg.start_server()
S = 320
font = ImageFont.truetype("arialbd.ttf", 18)
sheet = Image.new("RGB", (150 + S * len(SUBJECTS), 40 + S * len(MODELS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "SAME PROMPTS, THREE MODELS (asked for practical, really-built machines)", font=font, fill=(255, 240, 200))
for r, (mkey, mname, build) in enumerate(MODELS):
    d.text((10, 40 + r * S + S // 2 - 10), mname, font=font, fill=(255, 255, 255))
    for c, (key, label, what) in enumerate(SUBJECTS):
        path = os.path.join(OUT, "cmp_%s_%s_s%d.png" % (mkey, key, seed))
        if not os.path.exists(path):
            if only and only != mkey:
                continue
            prompt = ("%s. %s" % (what[0].upper() + what[1:], STYLE)) if mkey == "flux" else ("photo, %s, %s" % (what, STYLE))
            pg.generate(build(prompt, seed + c)).save(path)
            print("done", mkey, key, flush=True)
        x, y = 150 + c * S, 40 + r * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        if r == 0:
            d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
            d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
sheet.save(os.path.join(OUT, "compare_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "compare_sheet_s%d.png" % seed)))
