"""Concepts: automaton bases that sprawl out from the middle spawning vehicles, and patchwork "frankenstein" war
craft that look like no one familiar machine: automaton_concepts.py [--seed 61]
(The user, 2026-10-22: "automaton bases that sprawl out from the middle while spawning vehicles ... the tank is too
commonly understood, think of frankenstein craft that don't look like anything specific".)
-> art/_gen/concepts/auto_<key>_<try>_s<seed>.png, auto_craft_sheet_s<seed>.png, auto_base_sheet_s<seed>.png."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, r"C:\dev\ai-tools\comfy")
import pixelgen as pg

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")
WORLD = ("a world where unmanned automaton war factories build themselves and spit out war machines, gritty "
         "dieselpunk, riveted and welded steel, soot, oil, rust, chipped olive and gunmetal paint, smoking exhausts, "
         "amber indicator lamps, blue team markings")

CRAFT = [
    ("boiler", "BOILER CRAWLER", "a boiler drum body rolling on mismatched wheels of three sizes and one short track, "
                                 "a crane arm grafted on top swinging a cannon"),
    ("piston", "PISTON STRIDER", "a riveted furnace body carried on a mix of caterpillar track and clanking piston "
                                 "legs, a gun barrel jutting out of its side, pipes everywhere"),
    ("engines", "ENGINE HEAP", "a wide low heap of welded engine blocks, radiators and pipes rolling on huge spiked "
                               "iron drums, a cluster of short barrels on a swivel"),
    ("monowheel", "MONOWHEEL", "a lopsided machine built inside one giant iron wheel, steadied by two outrigger skids, "
                               "a long gun and a periscope sticking out"),
    ("hull", "HULL GRAFT", "the riveted bow section of a ship cut off and welded onto caterpillar treads, turbine fans "
                           "on the back and a turret from something else grafted on top"),
    ("drum", "GUN DRUM", "a squat armoured drum rolling on its own rim, cannons poking out of portholes around it, "
                         "exhaust stacks on the axle hubs"),
]
BASES = [
    ("core", "FURNACE CORE", "an automaton war factory growing outward from a central furnace core, assembly halls "
                             "budding off it like growths, open bays with new war machines rolling out"),
    ("cranes", "SELF-BUILDING", "a self-building steel factory organism, a smokestack core in the middle, branching "
                                "girder arms, robotic crane arms welding new segments on at its edges, scaffolds"),
    ("rings", "RINGS + SPOKES", "a sprawling automaton machine city in rings around a central reactor furnace, "
                                "conveyor belts radiating out like spokes carrying newly built war machines outward"),
    ("tree", "MACHINE TREE", "an automaton base shaped like a mechanical tree seen from above, a trunk core in the "
                             "middle, pipes branching out to factory pods, half-built frameworks at the tips"),
]
CRAFT2 = [      # round 2: armed and on the move, each a mash-up of unlike machines
    ("ram", "FURNACE RAM", "a war vehicle charging forward, a squat riveted furnace on four mismatched wheels with a "
                           "huge welded ram blade on the front and two stubby cannons poking out of its flanks"),
    ("crab", "GIRDER CRAB", "a low wide war vehicle walking sideways on six girder legs ending in small wheels, a "
                            "boiler body with a long cannon on a swivel, smokestacks"),
    ("tread", "TRIPLE TREAD", "a war vehicle on three separate caterpillar treads of different sizes bolted under a "
                              "crooked armoured engine block, a cluster of four gun barrels on a crane arm"),
    ("roller", "ROAD ROLLER", "an armoured steamroller war vehicle, a huge iron roller drum in front, a stack of "
                              "furnaces behind it, a mortar tube welded on the back"),
    ("hopper", "PISTON HOPPER", "a war vehicle that hops on two giant piston legs and a tail wheel, a riveted "
                                "pressure tank body, a gun under its chin"),
    ("snail", "IRON SNAIL", "a slow armoured war vehicle with a big coiled riveted boiler shell on its back, crawling "
                            "on a wide track, gun barrels sticking out of the coil"),
]
BASES2 = [      # round 2: seen from very high so the sprawl shows, with machines streaming out
    ("spread", "SPREADING STAIN", "an automaton war factory spreading out from a central smoking furnace core like a "
                                  "stain, dozens of small assembly halls, pipes and conveyors branching outward, "
                                  "tiny war machines streaming out from its edges"),
    ("hive", "HIVE", "an automaton war factory like a hive, a central dome furnace surrounded by many small "
                     "hexagonal assembly cells added outward in rings, the newest at the edge half-built in scaffolds, "
                     "columns of tiny war machines leaving"),
    ("spider", "WEB", "an automaton war factory in a web pattern, a central core with long conveyor lines running out "
                      "like spokes to outlying assembly halls and pumpjacks, cross-links between them, tiny war "
                      "machines on the conveyors"),
    ("crust", "CRUST", "an automaton war factory growing over the ground like a rusty crust, the oldest heavy "
                       "smokestacks in the middle, newer lighter structures and cranes towards the edge, tiny war "
                       "machines driving out of open bays"),
]
CRAFT_PROMPT = ("RTS game concept art, one single strange improvised war machine seen whole, isolated on a plain flat "
                "pale grey background, (it is {what}:1.25), (an unmanned patchwork machine cobbled together from "
                "mismatched salvaged parts, like nothing else:1.15), ({world}:1.0), (no crew, no humans, no cockpit, no "
                "windows:1.1), seen from above at a three-quarter angle, centred, the whole thing in frame, high "
                "detail, painterly realism")
BASE_PROMPT = ("RTS game concept art, (high three-quarter aerial view:1.2) of {what}, ({world}:1.0), sprawling out from "
               "the middle across bare brown dirt, (seen from very high up, the whole base small in frame:1.2) {far}, (no people:1.2), readable chunky shapes, "
               "high detail, painterly realism")
NEG = ("people, human, soldier, crew, pilot, face, anime, cartoon, (tank, military tank, modern tank, ww2 tank:1.2), "
       "car, truck, jeep, robot, humanoid, mech, animal, city street, sky, horizon, clouds, explosions, blurry, text, "
       "watermark, letters, frame, border, several views, sprite sheet, lineup, cropped, cut off, brass, victorian, "
       "flag, sleek, sci-fi, spaceship, glowing blue")

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 61
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 20)
pg.start_server()
S = 320


def sheet(title, items, prompt, name, neg):
    img = Image.new("RGB", (S * len(items), 40 + S * 2), (14, 14, 20))
    d = ImageDraw.Draw(img)
    d.text((12, 8), title, font=font, fill=(255, 240, 200))
    for c, (key, label, what) in enumerate(items):
        for r in range(2):
            path = os.path.join(OUT, "auto_%s_%d_s%d.png" % (key, r, seed))
            if os.path.exists(path):
                shot = Image.open(path).convert("RGB")
            else:
                g = pg.workflow(prompt.format(what=what, world=WORLD), 1024, 1024, seed + c * 10 + r, 30, 0.0, False)
                g["neg"]["inputs"]["text"] = neg
                shot = pg.generate(g).convert("RGB")
                shot.save(path)
                print("done", key, r, flush=True)
            x, y = c * S, 40 + r * S
            img.paste(shot.resize((S, S), Image.Resampling.LANCZOS), (x, y))
            tag = "%s %s" % (label, "ab"[r])
            d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
            d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
    img.save(os.path.join(OUT, "%s_s%d.png" % (name, seed)))


two = "--round" in args and args[args.index("--round") + 1] == "2"
if two:
    CRAFT_PROMPT = CRAFT_PROMPT.replace("strange improvised war machine", "strange improvised armed war vehicle on the move")
    BASE_PROMPT = BASE_PROMPT.replace("{far}", "many small structures")
else:
    BASE_PROMPT = BASE_PROMPT.replace("(seen from very high up, the whole base small in frame:1.2) {far},", "the whole base in frame,")
sheet("FRANKENSTEIN CRAFT: PATCHWORK WAR MACHINES" + (" (ROUND 2)" if two else ""), CRAFT2 if two else CRAFT,
      CRAFT_PROMPT, "auto_craft_sheet" + ("2" if two else ""), NEG + (", multiple views, display stand, static engine" if two else ""))
sheet("AUTOMATON BASES: SPRAWLING FROM THE MIDDLE, SPAWNING MACHINES" + (" (ROUND 2)" if two else ""), BASES2 if two else BASES,
      BASE_PROMPT, "auto_base_sheet" + ("2" if two else ""), NEG.replace("(tank, military tank, modern tank, ww2 tank:1.2), ", ""))
print("sheets in", os.path.abspath(OUT))
