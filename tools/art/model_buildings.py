"""Battler's buildings: model_buildings.py -- <factory_tank|factory_scout|factory_artillery|hq|tower|outpost>
(The user, 2026-10-19: "Please work on some factory models, dont over think it just get some stuff in".) The same gritty
dieselpunk look as the units (model_units.py), each with its own outline from above: the tank works a long sawtooth-roofed
shed with stacks; the scout bay a low stepped hangar with a radio mast; the artillery yard an open walled yard under a
gantry crane; the HQ a tiered bunker with a reactor dome and a radio tower; the watch tower a gun platform on a tall post.
Front (+a) is where units roll out, toward the enemy. "paint" (team colour) goes on roofs, where the camera looks.
Run: blender --background --python tools/art/model_buildings.py -- factory_tank
-> art/units/<name>.glb and <name>.png (the game loads them like the units); views in art/_gen/ships_iso/work."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *      # the palette, Blender's kit as k, and the shared pieces


def pad(half_a, half_x, h=0.6):
    """A concrete apron the building stands on, its edge picked out in a darker kerb."""
    k.box(-half_a, half_a, -half_x, half_x, 0.0, h, "mid")
    k.box(-half_a - 0.3, half_a + 0.3, -half_x - 0.3, half_x + 0.3, 0.0, h * 0.6, "dark")


def door(a, half_x, z0, z1):
    """A big shutter door on a front face at `a`: dark, with slats, a hazard frame."""
    k.box(a - 0.2, a + 0.15, -half_x - 0.6, half_x + 0.6, z0, z1 + 0.6, "hazard")
    k.box(a - 0.1, a + 0.3, -half_x, half_x, z0, z1, "black")
    for i in range(int((z1 - z0) / 0.9)):
        k.box(a + 0.1, a + 0.35, -half_x + 0.2, half_x - 0.2, z0 + 0.5 + i * 0.9, z0 + 0.75 + i * 0.9, "dark")


def factory_tank():
    """The tank works: a long shed with a sawtooth roof (team colour on the slopes), a big door, two stacks."""
    k.begin(0.0)
    pad(12.5, 10.5)
    k.box(-11, 9, -9, 9, 0.6, 8.5, "steel")                       # the shed
    for s in (1, -1):
        rivets(-10.5, 8.5, s * 9, 7.8, 12)
        k.box(-10, 8, min(s * 9, s * 9.25), max(s * 9, s * 9.25), 2, 6.5, "plate")   # flank plates
    for i in range(3):                                               # sawtooth roof: three teeth across
        a0 = -11 + i * 6.66
        k.upright([(a0, 8.5), (a0 + 6.66, 8.5), (a0 + 6.66, 12.5)], -9, 9, "paint")
        k.box(a0 + 6.2, a0 + 6.66, -8.4, 8.4, 8.6, 12.3, "glass_lit" if i == 1 else "dark")   # the glazed face
    door(9, 5.5, 0.6, 7)
    for s in (1, -1):
        stack(-9, s * 6.5, 8.5, 16.5, 0.9)
    k.box(-12.5, -11, -4, 4, 0.6, 5, "dark")                         # boiler house at the back
    eye(9.4, -7.5, 7.5, 0.45)
    k.finish("factory_tank", 34.0, UNITS)


def factory_scout():
    """The scout bay: a low, wide hangar stepped like a quonset, an open front, a radio mast with a dish."""
    k.begin(0.0)
    pad(11, 10)
    steps = [(9, 4.0), (7.5, 5.6), (5.5, 6.8), (3.0, 7.5)]           # half-width, height: a stepped round roof
    for i, (hx, z) in enumerate(steps):
        k.box(-9, 8, -hx, hx, 0.6 if i == 0 else steps[i - 1][1], z, "paint" if i == len(steps) - 1 else "steel")
    for i in range(5):                                               # ribs over the roof
        a = -8 + i * 3.8
        k.box(a, a + 0.4, -9.2, 9.2, 0.6, 4.2, "dark")
        k.box(a, a + 0.4, -7.7, 7.7, 4.0, 5.8, "dark")
    k.box(8, 8.4, -6, 6, 0.6, 4.6, "black")                          # the open front
    k.box(8, 8.6, -7, 7, 4.6, 5.2, "hazard")
    k.drum(-6.5, 6.5, 0.4, 0.6, 15.5, "dark", 6, 0.25)               # radio mast and dish
    k.drum(-6.5, 6.5, 2.0, 13.5, 13.9, "mid", 12, 1.5)
    k.drum(-6.5, 6.5, 0.3, 15.5, 16.0, "ember", 6)
    stack(-8, -6.5, 4, 9.5, 0.6)
    k.finish("factory_scout", 34.0, UNITS)


def factory_artillery():
    """The artillery yard: an open yard behind low walls, a gantry crane over it, racks of shells, a barrel waiting."""
    k.begin(0.0)
    pad(12, 11)
    for s in (1, -1):                                                # low walls down the sides and the back
        k.box(-11, 11, min(s * 9.5, s * 10.5), max(s * 9.5, s * 10.5), 0.6, 3.5, "steel")
        rivets(-10.5, 10.5, s * 10.5, 3.0, 10)
    k.box(-11, -10, -9.5, 9.5, 0.6, 3.5, "steel")
    k.box(-9, 9, -8.5, 8.5, 0.6, 0.9, "paint")                        # the yard floor in team colour
    for a in (-7, 6):                                                # gantry crane: two frames and a beam
        for s in (1, -1):
            k.box(a - 0.5, a + 0.5, s * 9.5 - 0.5, s * 9.5 + 0.5, 0.6, 12, "hazard")
        k.box(a - 0.6, a + 0.6, -10, 10, 11.5, 12.6, "dark")
    k.box(-7, 6, -0.6, 0.6, 12.0, 12.8, "dark")
    k.box(-1.5, 1, -1, 1, 9, 11.5, "mid")                             # the hoist
    tube_between((-6, 3, 1.6), (6, 3, 1.6), 0.55, "mid", 8)           # a howitzer barrel on trestles
    for a in (-4, 3):
        k.box(a - 0.4, a + 0.4, 2, 4, 0.9, 1.3, "dark")
    for i in range(3):                                               # shell racks
        for j in range(4):
            k.drum(-6 + j * 1.0, -5 - i * 1.0, 0.35, 0.9, 2.5, "rust", 6, 0.22)
    k.finish("factory_artillery", 34.0, UNITS)


def hq():
    """The HQ: a tiered command bunker, a reactor dome in team colour, stacks, and a tall radio tower."""
    k.begin(0.0)
    pad(17, 17)
    k.box(-15, 15, -15, 15, 0.6, 7, "steel", nose=0.94, tail=0.94, sits=True)     # lower tier, battered walls
    k.box(-11, 11, -11, 11, 7, 12, "plate", nose=0.92, tail=0.92, sits=True)       # upper tier
    for s in (1, -1):
        rivets(-14, 14, s * 15, 6.2, 16)
        k.box(-12, 12, min(s * 15, s * 15.3), max(s * 15, s * 15.3), 2, 5, "dark")  # armour bands
    k.drum(2, 0, 7, 12, 13, "dark", 16)
    k.dome(2, 0, 6.5, 13, 5.5, "paint", 3, 16)                        # reactor dome
    k.studs(2, 0, 6.8, 12.6, 13.2, "ember", 12, 0.35)                 # glowing vents round it
    door(15.1, 5, 0.6, 5.5)
    for s in (1, -1):
        stack(-9, s * 8, 12, 21, 1.1)
    for i in range(4):                                               # radio tower: a tapering lattice of legs
        x = (i % 2 * 2 - 1) * 1.4
        aa = (i // 2 * 2 - 1) * 1.4
        tube_between((-8 + aa, x + 8, 12), (-8 + aa * 0.3, 8 + x * 0.3, 27), 0.25, "dark", 4)
    for h in (16, 20, 24):
        k.box(-9.5, -6.5, 6.5, 9.5, h, h + 0.3, "dark")
    k.drum(-8, 8, 0.4, 27, 28, "ember", 6)
    eye(15.2, -9, 9, 0.8)
    eye(15.2, 9, 9, 0.8)
    k.finish("hq", 46.0, UNITS)


def tower():
    """A watch tower: a concrete foot, a tapering post, a platform with a gun and a searchlight eye."""
    k.begin(0.0)
    k.drum(0, 0, 7, 0, 2, "mid", 8, 6.5)
    k.drum(0, 0, 4, 2, 18, "steel", 8, 2.6)                           # the post
    for z in (6, 11, 15):
        k.drum(0, 0, 4 - (z - 2) * 0.0875 + 0.3, z, z + 0.6, "dark", 8)   # bands
    k.drum(0, 0, 5, 18, 19.5, "plate", 8)                             # platform
    k.drum(0, 0, 5.2, 19.5, 20.5, "paint", 8)                         # its rail in team colour
    k.drum(0, 0, 2.6, 20.5, 23.5, "steel", 8, 2.2)                    # gun turret
    k.tube(1.8, 8.5, 0, 22, 0.4, "mid", 8)
    k.tube(7.6, 8.9, 0, 22, 0.6, "dark", 8)
    eye(2.5, 1.6, 23, 0.6)
    k.drum(-1, -1.2, 0.15, 23.5, 27, "black", 4)                      # aerial
    k.finish("tower", 34.0, UNITS)


def outpost():
    """An outpost (a sub-base out in the field): a ring of sandbags round a low bunker with a team-coloured roof, a
    radar dish on a mast, and a small gun turret."""
    k.begin(0.0)
    for i in range(12):                                              # the sandbag ring, a gap at the front
        ang = (i + 0.5) * math.tau / 12
        if abs(math.atan2(math.sin(ang), math.cos(ang))) < 0.3:
            continue
        a, x = math.cos(ang) * 11, math.sin(ang) * 11
        k.drum(a, x, 1.6, 0, 1.4, "rust", 6, 1.3)
        k.drum(a, x, 1.3, 1.4, 2.4, "plate", 6, 0.9)
    k.box(-6, 6, -6, 6, 0, 4.5, "steel", nose=0.85, tail=0.85, sits=True)          # the bunker
    k.box(-5, 5, -5, 5, 4.5, 5.2, "paint")                            # its roof in team colour
    for s in (1, -1):
        rivets(-5.5, 5.5, s * 6, 3.5, 7)
    k.box(5.6, 6.3, -2.5, 2.5, 1.2, 2.2, "black")                     # firing slit
    k.drum(-3, -3, 0.3, 5.2, 12, "dark", 6, 0.2)                      # radar mast and dish
    k.drum(-2.6, -3, 2.2, 11.5, 11.9, "mid", 12, 1.6)
    k.drum(-3, -3, 0.25, 12, 12.5, "ember", 6)
    k.drum(2, 2, 2, 5.2, 7, "steel", 8, 1.7)                          # gun turret
    k.tube(3.5, 8.5, 2, 6.2, 0.3, "mid", 6)
    eye(6.35, -3.5, 3.2, 0.5)
    k.finish("outpost", 34.0, UNITS)


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"factory_tank": factory_tank, "factory_scout": factory_scout, "factory_artillery": factory_artillery,
 "hq": hq, "tower": tower, "outpost": outpost}[which]()
