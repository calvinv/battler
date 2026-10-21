"""The three civilisations side by side: civ_concepts.py [--seed 7]
(The user, 2026-10-21: "I want 3 civilisations. 1 versatile. One swarm based, one tech focussed", named the Coalition,
Lattice and the Ascendancy.) A row per civ, a column per role (light unit, tank, artillery, HQ), so the three looks
can be judged against each other. Machines only, no crew. Pixel LoRA 0.
-> art/_gen/concepts/civ_<civ>_<role>_s<seed>.png and civ_sheet_s<seed>.png. About 25 s a picture."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

CIVS = [
    ("coalition", "COALITION (versatile)",
     "boxy chunky armoured vehicle on (tank tracks:1.2), clean grey and white armour panels with rivets and seams, "
     "blue team colour stripes, thin glowing cyan lights, yellow and black warning chevrons, stencilled serial numbers, "
     "grounded military sci-fi like Supreme Commander and Mechabellum",
     "legs, walker, hovering"),
    ("lattice", "LATTICE (swarm)",
     "low scuttling machine on (many thin mechanical legs:1.2), cobbled together from mismatched scrap plates welded "
     "over an exposed metal frame, rust orange and bone white, clusters of small glowing amber sensor eyes, salvaged "
     "parts, insect-like but clearly a machine, post-apocalyptic",
     "tank tracks, clean, shiny, white armour"),
    ("ascendancy", "ASCENDANCY (tech)",
     "sleek elegant machine (hovering above the ground on glowing anti-gravity pads:1.2), seamless dark graphite and "
     "white ceramic armour with sharp faceted planes, an exposed glowing violet energy core, thin violet light lines, "
     "advanced alien-like technology but machine made",
     "tank tracks, legs, wheels, rust, scrap"),
]
ROLES = [
    ("light", "LIGHT", "a small fast light combat unit with (one gun:1.1)"),
    ("tank", "TANK", "a main battle unit, (a big turret with a heavy main weapon:1.2), clearly the army's tank"),
    ("artillery", "ARTILLERY", "a long range artillery unit, (a very long cannon raised steeply high:1.25)"),
    ("hq", "HQ", "(a large headquarters building:1.3), the command base of the faction, a fortified structure"),
]
PROMPT = ("futuristic RTS game concept art, one single {kind} seen whole, isolated on a plain flat pale grey "
          "background, nothing around it, (it is {what}:1.15), ({look}:1.2), (no crew, no humans, no cockpit, no "
          "windows:1.1), seen from above at a three-quarter angle, centred, the whole thing in frame, high detail")
NEG = ("people, human, pilot, crew, cockpit, windows, hatch, face, anime, animal, crowd, scene, landscape, city, sky, "
       "clouds, explosions, blurry, text, watermark, letters, frame, border, several views, sprite sheet, lineup, "
       "cropped, cut off, steampunk, brass, flag, star, {avoid}")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 7
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 22)
pg.start_server()
S = 448
sheet = Image.new("RGB", (S * len(ROLES), 44 + S * len(CIVS)), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((14, 8), "BATTLER: THE THREE CIVILISATIONS", font=font, fill=(255, 240, 200))
for r, (ckey, cname, look, avoid) in enumerate(CIVS):
    for c, (rkey, rname, what) in enumerate(ROLES):
        path = os.path.join(OUT, "civ_%s_%s_s%d.png" % (ckey, rkey, seed))
        if os.path.exists(path):
            shot = Image.open(path).convert("RGB")
        else:
            kind = "building" if rkey == "hq" else "unmanned war machine"
            g = pg.workflow(PROMPT.format(kind=kind, what=what, look=look), 1024, 1024, seed + r * 10 + c, 30, 0.0, False)
            g["neg"]["inputs"]["text"] = NEG.format(avoid=avoid)
            shot = pg.generate(g).convert("RGB")
            shot.save(path)
        x, y = c * S, 44 + r * S
        sheet.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
        label = "%s  %s" % (cname, rname)
        d.text((x + 9, y + 7), label, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), label, font=font, fill=(255, 255, 255))
        print("done", ckey, rkey, flush=True)
sheet.save(os.path.join(OUT, "civ_sheet_s%d.png" % seed))
print("sheet in", os.path.abspath(OUT))
