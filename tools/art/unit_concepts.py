"""More units, FLUX, in the hover tank / mech / battle wheel look: unit_concepts.py [--seed 901]
(The user, 2026-10-23: "lets create a few more units, come up with some ideas then make concepts".) Five that remake
the units still in the old dieselpunk look (artillery, mortar, scout, interceptor, bomber) and three new roles
(repair, shield, swarm carrier). Three tries each.
-> art/_gen/concepts/unit_<key>_<try>_s<seed>.png and unit_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
IDEAS = [
    ("longleg", "LONG LEG (artillery)", "a tall three-legged tripod walker robot with long thin spindly legs, a small armoured body slung "
                                        "high between them, and a very long rail cannon barrel reaching far forward from the body"),
    ("lobber", "LOBBER (mortar)", "a squat six-legged crab-like robot, low and wide, carrying one fat stubby mortar tube on its back "
                                  "pointing steeply up at the sky, ammunition drums on its sides"),
    ("skitter", "SKITTER (scout)", "a tiny round scout robot balancing on one single small wheel, a ball-shaped body with one big "
                                   "glowing round eye lens and a tall thin bendy radio antenna"),
    ("dart", "DART (interceptor)", "a small flying drone with two ducted rotor fans on short stubby wings, a sharp pointed nose with "
                                   "twin small guns, swept tail fins, hovering in the air above its own shadow"),
    ("blimp", "BLIMP (bomber)", "a fat armoured flying blimp robot, a round bulbous armoured gas bag hull with small propellers on "
                                "the sides and a bomb bay hatch underneath, floating high above its own shadow"),
    ("mender", "MENDER (repair)", "a small tracked repair carrier robot with a launch pad on its back, and two or three tiny "
                                  "hovering repair drones flying out from it, each drone with little rotors, a welding torch arm "
                                  "and a glowing green repair light"),   # (the user: "Mender should have repair drones that fly over and repair")
    ("bulwark", "BULWARK (shield)", "a slow heavy four-legged walker robot carrying a tall dish-shaped shield emitter on its back that "
                                    "projects a faint glowing transparent dome over itself, thick armour plates"),
    ("hive", "HIVE (swarm carrier)", "a boxy six-wheeled carrier truck robot with a honeycomb hatch rack on its back, small insect-like "
                                     "drones flying out of the open hatches"),
]
WHAT = ("A cute cartoonish unmanned battle robot for a strategy game, big in the picture, filling most of the frame: "
        "{idea}. Its own design, not like any real vehicle.")
COLOURS = "Dark slate grey armour panels with orange stripes and small warm amber lights."
STYLE = ("Seen from above at a three-quarter angle, isolated on a plain flat dark background, centred, the whole robot "
         "in frame. Clean pixel art game sprite: simple chunky shapes, a limited palette, flat cel shading in two or "
         "three tones, crisp dark outlines. No text, no letters, no people, no rider.")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 901
TRIES, S, COLS = 3, 300, 6
font = ImageFont.truetype("arialbd.ttf", 17)
rows = (len(IDEAS) * TRIES + COLS - 1) // COLS
sheet = Image.new("RGB", (S * COLS, 40 + S * rows), (14, 14, 20))
d = ImageDraw.Draw(sheet)
d.text((12, 8), "NEW UNITS (FLUX): eight ideas, three tries each", font=font, fill=(255, 240, 200))
n = 0
for c, (key, label, idea) in enumerate(IDEAS):
    for r in range(TRIES):
        path = os.path.join(OUT, "unit_%s_%d_s%d.png" % (key, r, seed))
        if not os.path.exists(path):
            fluxgen.image("%s %s %s" % (WHAT.format(idea=idea), COLOURS, STYLE), seed + c * 10 + r).convert("RGB").save(path)
            print("done", key, r, flush=True)
        x, y = (n % COLS) * S, 40 + (n // COLS) * S
        sheet.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
        tag = "%d %s" % (n + 1, label)
        d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
        d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
        n += 1
sheet.save(os.path.join(OUT, "unit_sheet_s%d.png" % seed))
print("sheet", os.path.abspath(os.path.join(OUT, "unit_sheet_s%d.png" % seed)))
