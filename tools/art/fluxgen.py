"""FLUX.1 schnell through ComfyUI (GGUF Q4, fits 8 GB): import fluxgen; fluxgen.image(prompt, seed, w, h) -> PIL image.
Plain-English prompts, no negative prompt (schnell ignores it), 4 steps, about 7 s at 1024 on the RTX 2070.
Needs custom_nodes/ComfyUI-GGUF and models/unet/flux1-schnell-Q4_K_S.gguf, text_encoders/t5xxl_fp8_e4m3fn +
clip_l, vae/flux_ae (the same file as FLUX's ae.safetensors, from Comfy-Org's ungated repackage)."""
import sys
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg


def graph(prompt, seed, w=1024, h=1024, steps=4):
    return {
        "unet": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "flux1-schnell-Q4_K_S.gguf"}},
        "clip": {"class_type": "DualCLIPLoader", "inputs": {"clip_name1": "t5xxl_fp8_e4m3fn.safetensors",
                                                            "clip_name2": "clip_l.safetensors", "type": "flux"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": "flux_ae.safetensors"}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["clip", 0]}},
        "neg": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["pos", 0]}},
        "latent": {"class_type": "EmptySD3LatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
        "sample": {"class_type": "KSampler", "inputs": {
            "model": ["unet", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0],
            "seed": seed, "denoise": 1.0, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple"}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sample", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": "flux"}},
    }


def image(prompt, seed, w=1024, h=1024, steps=4):
    pg.start_server()
    return pg.generate(graph(prompt, seed, w, h, steps))
