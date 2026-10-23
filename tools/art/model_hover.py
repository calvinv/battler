"""Simple futuristic units in the cel shaded look: model_hover.py -- <name>
(The user, 2026-10-23: "the shell shaded look we used in void wake ... simple but futuristic builds"; the light tank from
FLUX concept art/_gen/concepts/hover2_slate_0_s301.png, "the barrel needs a bit of work".)
Flat colours, few big chamfered shapes, no weathering: the game draws them with toon light bands and ink outlines
(game/toon_unit.gdshader). Moving parts are tagged for the rig (<name>_rig.glb, game/UnitRig.cs).
Names: hovertank
Run: blender --background --python tools/art/model_hover.py -- hovertank"""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *
import math

# Dark slate armour in three steps, near-black trim, team paint for the chevrons, cyan hover light, a hot yellow tip.
k.recolour(steel=(70, 76, 86), plate=(58, 63, 72), mid=(88, 94, 104), dark=(38, 41, 47), black=(20, 22, 26),
           glow=(255, 214, 64), glass_lit=(90, 232, 255), white=(206, 212, 220))


def xs(s, a, b):
    return tuple(sorted((s * a, s * b)))


def hovertank():
    """Hover light tank: a wide slab hull floating on a cyan glow, ribbed side pods with team chevrons, a raised
    engine block at the back with a cyan vent, a stacked octagonal turret and a clean laser barrel (a mount block,
    a straight barrel, a team band, a glowing emitter)."""
    k.begin(0.0)
    zb = 2.2                                                                                # hull underside: it floats
    # Hull and side pods.
    plate(-4.4, 4.4, -2.9, 2.9, zb, zb + 1.5, "steel", inset=0.5, front=1.0, back=0.5)
    for s in (1, -1):
        plate(-4.2, 4.0, *xs(s, 2.9, 4.2), zb - 0.15, zb + 1.25, "plate", inset=0.35, front=0.9, back=0.4)
        for i in range(5):                                                                   # round vents down the pod
            a = -3.0 + i * 1.3
            tube_between((a, s * 4.15, zb + 0.55), (a, s * 4.3, zb + 0.55), 0.3, "black", 10)
        for row in range(2):                                                                 # team chevrons on the pod
            for col in range(3):
                a0 = 2.0 + col * 0.55 - row * 0.28
                k.box(a0, a0 + 0.38, *xs(s, 3.1 + row * 0.5, 3.5 + row * 0.5), zb + 1.25, zb + 1.32, "paint")
        k.box(-4.0, 3.8, *xs(s, 3.0, 4.1), zb - 0.25, zb - 0.15, "glass_lit")              # hover light under the pod
    k.box(-3.8, 3.8, -2.4, 2.4, zb - 0.08, zb, "glass_lit")                                 # hover light under the hull
    # The raised engine block at the back: panel seams on top, a cyan vent in its rear face, a small sensor box.
    plate(-5.2, -1.8, -2.5, 2.5, zb + 1.5, zb + 2.3, "plate", inset=0.3, back=0.4)
    plate(-5.0, -2.0, -2.5, 2.5, zb, zb + 1.5, "plate", inset=0.0)
    k.box(-3.55, -3.45, -2.3, 2.3, zb + 2.3, zb + 2.34, "dark")
    k.box(-5.0, -2.0, -0.05, 0.05, zb + 2.3, zb + 2.34, "dark")
    k.box(-5.3, -5.15, -1.9, 1.9, zb + 0.5, zb + 1.1, "glass_lit")
    k.box(-5.25, -5.1, -2.1, 2.1, zb + 0.4, zb + 0.45, "dark")
    k.box(-2.6, -2.2, 1.6, 2.0, zb + 2.3, zb + 2.6, "mid")
    k.box(-2.25, -2.18, 1.7, 1.9, zb + 2.4, zb + 2.5, "glow")
    # The turret: a wide octagon, a narrower one on it, a hex cap; small team vents and lights at its front.
    tz = zb + 1.5
    with part("turret", (0.4, 0, tz)):
        hexagon(0.4, 0, 2.8, tz, tz + 0.9, "plate", r1=2.55, sides=8, turn=math.pi / 8)
        hexagon(0.4, 0, 1.8, tz + 0.9, tz + 1.7, "steel", r1=1.6, sides=8, turn=math.pi / 8)
        hexagon(0.4, 0, 0.75, tz + 1.7, tz + 2.2, "mid", r1=0.65, sides=6)
        for x in (-0.55, 0.0, 0.55):
            k.box(2.85, 2.95, x - 0.18, x + 0.18, tz + 0.3, tz + 0.55, "paint")
        k.box(2.0, 2.1, -1.35, -1.15, tz + 1.1, tz + 1.3, "glow")
        gz = tz + 1.0
        # The laser: a clean straight barrel on a mount block, a team band, a glowing emitter at the tip.
        with part("barrel", (2.2, 0, gz), parent="turret"):
            k.box(1.6, 2.9, -0.45, 0.45, gz - 0.42, gz + 0.42, "mid")
            k.tube(2.9, 3.3, 0, gz, 0.34, "dark", 12)
            k.tube(3.3, 8.2, 0, gz, 0.2, "dark", 12)
            k.tube(7.0, 7.45, 0, gz, 0.26, "paint", 12)
            k.tube(8.2, 8.45, 0, gz, 0.3, "dark", 12)
            k.tube(8.45, 8.75, 0, gz, 0.24, "glow", 12)
    mark("muzzle", (8.85, 0, gz), "barrel")
    finish_toon("hovertank", 24.0)


globals()[sys.argv[sys.argv.index("--") + 1]]()
