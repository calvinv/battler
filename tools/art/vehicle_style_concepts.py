"""Style directions for Battler's army: vehicle_style_concepts.py [--seed 7]
(The user, 2026-10-19: "I want to lean away from too mechy things yet, tanks, artillery, flak cannons, jets,
bombers etc", then: "things should be a bit steam punk, grimey, think post apocolypse machines take over and fight
for resourves".) Six takes on that world, each shown on a tank and a bomber so it's judged on ground and air. No
crews anywhere: the machines are the factions, so sensors and furnace glow instead of cockpits and hatches. Every
look must be buildable as a flat-sided low-poly model painted by the ai-tools paint pass. Pixel LoRA 0.
-> art/_gen/concepts/vstyle_<key>_<subject>_s<seed>.png and vstyle_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
SUBJECTS = [
    ("tank", "a heavy battle tank with tracks and a big turret with a long cannon, parked on its own"),
    ("bomber", "a heavy bomber aircraft with broad wings and big propeller engines, flying on its own"),
]
WORLD = ("unmanned self-aware war machine, no crew, no cockpit windows, glowing sensor eyes, post-apocalyptic, "
         "grimy and soot-stained, steampunk")
STYLES = [
    ("scrap", "SCRAP-BUILT", "welded together from scavenged scrap plates and salvaged parts, mismatched rusty panels, "
     "chains, spikes, patched and rebuilt, junkyard war machine, painterly concept art"),
    ("boiler", "BRASS AND BOILER", "riveted blackened iron with tarnished brass and copper pipes, a big boiler, "
     "smokestacks puffing soot, pressure gauges, valves, glowing furnace grille, painterly concept art"),
    ("foundry", "FOUNDRY IRON", "heavy cast iron and dark steel, molten orange glow from furnace vents, coal smoke, "
     "massive and brutal, few large shapes, industrial revolution, painterly concept art"),
    ("ruin", "OVERGROWN RELIC", "ancient corroded green bronze and rust, moss and creeping vines, cracked plating, "
     "dust, a machine that has fought for centuries, faded team paint, painterly concept art"),
    ("diesel", "DIESEL WAR ENGINE", "oversized 1940s war machine, dark olive steel, exhaust stacks, big bolts, "
     "stencilled numbers worn away, oil stains, mud caked tracks, painterly concept art"),
    ("inked", "GRIMY CEL + INK", "rusty iron and brass with soot, (flat cel shading, thick black ink outlines, comic "
     "book style:1.2), flat colours in two or three hard bands, bold readable shapes"),
]
PROMPT = ("war machine design concept art, one single vehicle seen whole, isolated on a plain flat pale grey "
          "background, nothing around it, (it is {what}:1.15), ({world}:1.1), ({look}:1.2), seen from above at a "
          "three-quarter angle, centred, the whole vehicle in frame, game concept art, high detail")
NEG = ("mech, robot, legs, walker, people, soldiers, pilot, crew, human, face, crowd, scene, environment, landscape, "
       "city, buildings, terrain, ground, sky, clouds, explosions, blurry, text, watermark, signature, letters, frame, "
       "border, many vehicles, two vehicles, sprite sheet, cropped, cut off, clean, shiny, new")

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
d.text((14, 8), "BATTLER: GRIMY STEAMPUNK WAR MACHINES  (pick a number, mix freely)", font=font, fill=(255, 240, 200))
for n, (key, name, look) in enumerate(STYLES):
    for v, (subject, what) in enumerate(SUBJECTS):
        path = os.path.join(OUT, "vstyle_%s_%s_s%d.png" % (key, subject, seed))
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
sheet.save(os.path.join(OUT, "vstyle_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
