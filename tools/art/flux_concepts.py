"""Automaton concepts with FLUX schnell (practical, really-built looks): flux_concepts.py [--seed 91] [--tries 2]
Patchwork war craft, automaton bases, and one base growing from the middle out in four stages.
-> art/_gen/concepts/flux_<key>_<try>_s<seed>.png and flux_craft / flux_bases / flux_growth sheets."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluxgen

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "_gen", "concepts")

CRAFT_STYLE = ("It is full-size, heavy and battle-worn, unmanned, cobbled together by automated factories from "
               "mismatched salvaged industrial parts so it looks like no familiar vehicle: welded steel plate, rivets, "
               "hydraulic rams, hoses, exhaust stacks trailing soot, chipped olive drab paint, rust and oil stains, a "
               "couple of amber lamps, a blue painted stripe. 1940s diesel-era engineering, practical and believable. "
               "Seen whole from a high three-quarter angle on bare dirt, daylight, sharp photo. No people, no text.")
CRAFT = [
    ("drum", "GUN DRUM", "A war machine whose body is a big riveted steel drum on a six-wheeled bogie, a short cannon "
                         "out of the front of the drum."),
    ("strider", "PISTON STRIDER", "A war machine with a boxy riveted furnace body on four hydraulic piston legs with "
                                  "flat feet, a machine gun slung under the front."),
    ("crab", "GIRDER CRAB", "A low war machine: a diesel engine block and fuel tank on a wide girder frame with four "
                            "big spoked steel wheels splayed out, a cannon on a swivel on top."),
    ("roller", "ROLLER", "A war machine pushing a huge ribbed iron roller drum in front of an armoured engine house, "
                         "a mortar tube on the back."),
    ("mono", "MONOWHEEL", "A war machine built inside one giant iron wheel taller than it is long, outrigger skids "
                          "either side, a long gun sticking out forward."),
    ("snail", "IRON SNAIL", "A slow war machine with a coiled riveted boiler shell on its back crawling on one wide "
                            "track, two gun barrels poking out of the coil."),
]
BASE_STYLE = ("Gritty diesel-era industry: riveted steel, girders, corrugated roofs, soot-black smokestacks, rust, oil "
              "stains, a few amber lamps, blue painted roofs on the core. Seen from high above at a steep angle, the "
              "whole base in frame on bare brown dirt, sharp aerial photo. No people, no text.")
BASES = [
    ("hive", "HIVE", "An unmanned automated war factory that builds itself: a central domed furnace with smokestacks, "
                     "rings of small hexagonal steel assembly sheds added outward around it, the outer ring still "
                     "scaffolding with crane arms, small war machines driving out from the edge."),
    ("web", "WEB", "An unmanned automated war factory that builds itself: a central furnace core with long covered "
                   "conveyor lines running out like spokes to smaller assembly sheds and oil pumps, small war machines "
                   "riding the conveyors outward."),
    ("crust", "CRUST", "An unmanned automated war factory spreading over the ground like a crust: old heavy "
                       "smokestacks in the middle, newer lighter sheds and cranes towards the edge, small war "
                       "machines rolling out of open bays."),
    ("tree", "BRANCHES", "An unmanned automated war factory that branches like a tree: a trunk furnace in the middle, "
                         "thick pipes forking outward to assembly pods, half-built frames at the tips, small war "
                         "machines leaving."),
]
GROWTH = [
    ("g1", "1 CORE", "only the central domed furnace core with smokestacks and a crane beginning to build, on empty dirt."),
    ("g2", "2 FIRST RING", "the central domed furnace core with a first ring of six small hexagonal assembly sheds "
                           "joined around it, a small war machine driving out."),
    ("g3", "3 REACHING OUT", "the central domed furnace core, two rings of assembly sheds, and pipelines reaching out to "
                             "an oil pump and a power plant that are growing sheds of their own, war machines leaving."),
    ("g4", "4 SPRAWL", "the central domed furnace core with many rings of assembly sheds sprawling outward, pipelines to "
                       "oil pumps and power plants on every side, gun nests around the edge, columns of war machines "
                       "leaving."),
]
GROWTH_LEAD = "An unmanned automated war factory that grows outward from its middle, seen from straight above: "

args = sys.argv[1:]
seed = int(args[args.index("--seed") + 1]) if "--seed" in args else 91
tries = int(args[args.index("--tries") + 1]) if "--tries" in args else 2
os.makedirs(OUT, exist_ok=True)
font = ImageFont.truetype("arialbd.ttf", 20)
S = 320


def sheet(title, items, make, name, same_seed=False):
    img = Image.new("RGB", (S * len(items), 40 + S * tries), (14, 14, 20))
    d = ImageDraw.Draw(img)
    d.text((12, 8), title, font=font, fill=(255, 240, 200))
    for c, (key, label, what) in enumerate(items):
        for r in range(tries):
            path = os.path.join(OUT, "flux_%s_%d_s%d.png" % (key, r, seed))
            if not os.path.exists(path):
                fluxgen.image(make(what), seed + r * 100 + (0 if same_seed else c)).save(path)
                print("done", key, r, flush=True)
            x, y = c * S, 40 + r * S
            img.paste(Image.open(path).convert("RGB").resize((S, S), Image.Resampling.LANCZOS), (x, y))
            tag = "%s %s" % (label, "abcd"[r])
            d.text((x + 9, y + 7), tag, font=font, fill=(0, 0, 0))
            d.text((x + 8, y + 6), tag, font=font, fill=(255, 255, 255))
    img.save(os.path.join(OUT, "%s_s%d.png" % (name, seed)))


sheet("FLUX: PATCHWORK WAR CRAFT", CRAFT, lambda w: w + " " + CRAFT_STYLE, "flux_craft")
sheet("FLUX: AUTOMATON BASES", BASES, lambda w: w + " " + BASE_STYLE, "flux_bases")
sheet("FLUX: ONE BASE GROWING FROM THE MIDDLE OUT (same seed per row)", GROWTH,
      lambda w: GROWTH_LEAD + w + " " + BASE_STYLE.replace("at a steep angle", "straight down"), "flux_growth", same_seed=True)
print("sheets in", os.path.abspath(OUT))
