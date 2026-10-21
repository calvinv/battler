"""Battler's buildings: model_buildings.py -- <factory_tank|factory_artillery|hq|tower|outpost|extractor|deposit>
The same clean modern look as the units (unit_parts.py; the user, 2026-10-20: "a more modern tech look like supreme
commander"), each with its own outline from above: the tank factory a long hangar with sloped armour walls and an
open bay; the artillery yard an open assembly cradle under a bridge crane; the HQ a tiered octagonal command centre
round a lit core tower; the watch tower a hex pylon with a twin-gun turret; the outpost a hex bunker that fits its
pad, with a radar mast; the extractor a pump over the deposit with four pistons round a glowing core; the deposit a
cluster of dark rock split by glowing green ore (nobody's: no team paint).
Front (+a) is where units roll out, toward the enemy. "paint" (team colour) goes on roofs, where the camera looks.
Run: blender --background --python tools/art/model_buildings.py -- factory_tank
-> art/units/<name>.glb and <name>.png (the game loads them like the units); views in art/_gen/ships_iso/work."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *      # the palette, Blender's kit as k, and the shared pieces


def factory_tank():
    """The land factory: a long hangar with sloped armour walls, an open bay at the front with a lit gantry, team
    panels down the roof, two assembly arms over it."""
    k.begin(0.0)
    apron(12.5, 10.5)
    plate(-11, 9, -9, 9, 0.5, 8.0, "steel", inset=1.6, front=0.4, back=1.2)          # the hangar
    plate(-9.6, 8.4, -7.4, 7.4, 8.0, 9.0, "plate", inset=0.6)                          # its roof
    for s in (1, -1):
        k.box(-8.6, 6.4, s * 4.2 - 1.4, s * 4.2 + 1.4, 9.0, 9.25, "paint")                # team panels on the roof
        strip(-9.0, 7.8, s * 8.0 - 0.1, s * 8.0 + 0.1, 5.0, "glow", 0.2)                  # lit strips down the walls
        # assembly arms: a post on the roof and a jib reaching out over the bay
        k.box(-2 - 0.7, -2 + 0.7, s * 5.5 - 0.7, s * 5.5 + 0.7, 9.0, 13.0, "mid")
        tube_between((-2, s * 5.5, 12.6), (7.5, s * 2.5, 11.0), 0.4, "dark", 6)
        k.box(7.0, 8.0, s * 2.5 - 0.5, s * 2.5 + 0.5, 9.6, 11.0, "hazard")
    k.box(8.6, 9.4, -5.5, 5.5, 0.5, 7.0, "black")                                      # the open bay
    k.box(8.6, 9.6, -6.4, 6.4, 7.0, 7.8, "plate")                                       # its lintel
    strip(9.5, 9.7, -5.2, 5.2, 7.2, "glow", 0.3)                                       # lit over the door
    for s in (1, -1):
        k.box(8.6, 10.2, s * 5.5 - 0.5 * (1 if s > 0 else -1) - 0.4, s * 5.5 + 0.4, 0.5, 7.0, "hazard")   # door posts
    vents(-10.0, -4.0, -3.0, 3.0, 9.0, 6)
    lens(9.7, -7.0, 6.0, 0.4)
    finish_realistic("factory_tank", 34.0, serial="0" + str(len("factory_tank")))


def factory_lighttank():
    """The light vehicle works: a low, wide garage with two bays side by side at the front, a flat roof with a team
    stripe and a radar mast; quick, small work. Lower and wider than the land factory."""
    k.begin(0.0)
    apron(12, 11)
    plate(-9, 8, -9.5, 9.5, 0.5, 5.5, "steel", inset=1.0, front=0.3, back=0.8)         # the garage
    plate(-7.8, 7.2, -8.2, 8.2, 5.5, 6.2, "plate", inset=0.4)
    k.box(-7.0, 5.6, -1.2, 1.2, 6.2, 6.45, "paint")                                     # team stripe down the roof
    for s in (1, -1):
        k.box(7.6, 8.4, s * 4.6 - 3.0, s * 4.6 + 3.0, 0.5, 4.6, "black")                 # the two bays
        strip(8.3, 8.5, s * 4.6 - 2.8, s * 4.6 + 2.8, 4.7, "glow", 0.25)
        k.box(-6.5, 5.0, s * 5.4 - 1.5, s * 5.4 + 1.5, 6.2, 6.45, "paint")
        strip(-8.0, 6.0, s * 8.9 - 0.05, s * 8.9 + 0.05, 3.2, "glow", 0.18)
    k.box(7.6, 8.8, -0.7, 0.7, 0.5, 5.2, "hazard")                                     # the post between the bays
    k.drum(-6, -6.5, 0.25, 6.2, 12.0, "dark", 6)                                        # radar mast
    k.box(-7.2, -4.8, -6.7, -6.3, 11.0, 12.6, "white")
    k.drum(-6, -6.5, 0.3, 12.0, 12.5, "glow", 6)
    vents(-8.0, -3.0, 2.0, 6.0, 6.2, 5)
    finish_realistic("factory_lighttank", 34.0, serial="0" + str(len("factory_lighttank")))


def factory_mortar():
    """The mortar works: a compact block with a steeply sloped roof (a team panel on its slope), a test-firing pit
    out front with a stubby tube standing in it, ready rounds stacked by the door. Tall at the back, open in front."""
    k.begin(0.0)
    apron(12, 10)
    k.upright([(-10, 0.5), (1, 0.5), (1, 6.0), (-10, 11.0)], -7, 7, "steel")           # the block, its roof raked
    k.upright([(-9.4, 10.7), (0.4, 6.2), (0.4, 6.6), (-9.4, 11.1)], -5.5, 5.5, "paint")  # team panel on the slope
    for s in (1, -1):
        strip(-9.6, 0.6, s * 7.0 - 0.05, s * 7.0 + 0.05, 3.0, "glow", 0.2)
    k.box(0.8, 1.4, -3.5, 3.5, 0.5, 4.8, "black")                                      # the door
    strip(1.3, 1.5, -3.3, 3.3, 4.9, "glow", 0.25)
    hexagon(6.5, 0, 4.0, 0.5, 1.2, "dark", r1=3.6)                                      # the test pit's rim
    hexagon(6.5, 0, 3.0, 1.2, 1.3, "black")
    tube_between((6.5, 0, 1.3), (8.0, 0, 6.0), 0.8, "mid", 10)                           # a stubby tube in it
    tube_between((7.6, 0, 4.8), (7.8, 0, 5.4), 0.9, "glow", 10)
    for i in range(3):                                                                  # ready rounds
        k.drum(2.6 + i * 1.0, -6.0, 0.4, 0.5, 2.2, "hazard", 6, 0.28)
    finish_realistic("factory_mortar", 34.0, serial="0" + str(len("factory_mortar")))


def factory_spider():
    """The spider works: a hexagonal assembly floor under a ring of four robotic arms on tall posts, a squat control
    block at the back with a team roof, lit seams in the floor. Open, not a shed: it builds walkers."""
    k.begin(0.0)
    apron(12, 11)
    hexagon(1.0, 0, 7.5, 0.5, 1.4, "plate", r1=7.1)                                    # the assembly floor
    hexagon(1.0, 0, 6.0, 1.4, 1.5, "paint")
    hexagon(1.0, 0, 3.2, 1.5, 1.6, "glow")
    plate(-11.0, -6.0, -6.0, 6.0, 0.5, 6.0, "steel", inset=0.6)                         # control block
    plate(-10.4, -6.6, -5.0, 5.0, 6.0, 6.4, "paint", inset=0.3)
    strip(-6.1, -5.9, -4.5, 4.5, 4.0, "glow", 0.3)
    for i in range(4):                                                                  # robotic arms on posts
        ang = math.pi / 4 + i * math.pi / 2
        pa, px = 1.0 + math.cos(ang) * 8.5, math.sin(ang) * 8.5
        plate(pa - 0.8, pa + 0.8, px - 0.8, px + 0.8, 0.5, 9.0, "mid", inset=0.2)
        k.drum(pa, px, 1.0, 9.0, 9.8, "dark", 8)
        elbow = (pa + (1.0 - pa) * 0.45, px * 0.55, 10.5)
        tube_between((pa, px, 9.4), elbow, 0.45, "plate", 4)
        tube_between(elbow, (1.0 + math.cos(ang) * 2.2, math.sin(ang) * 2.2, 5.5), 0.35, "plate", 4)
        k.drum(1.0 + math.cos(ang) * 2.2, math.sin(ang) * 2.2, 0.3, 4.9, 5.5, "glow", 6)   # welding tips
    lens(11.0, 0.0, 1.0, 0.45)
    finish_realistic("factory_spider", 34.0, serial="0" + str(len("factory_spider")))


def factory_artillery():
    """The artillery yard: a raised assembly cradle in the open, four angular pylons, a bridge crane spanning them,
    a barrel being fitted, team colour on the cradle."""
    k.begin(0.0)
    apron(12, 11)
    plate(-9, 9, -6, 6, 0.5, 2.0, "plate", inset=0.6)                                   # the cradle
    k.box(-7.5, 7.5, -4.4, 4.4, 2.0, 2.2, "paint")
    for a in (-8.5, 8.5):
        for s in (1, -1):
            plate(a - 1.4, a + 1.4, s * 9 - 1.4, s * 9 + 1.4, 0.5, 11.0, "steel", inset=0.5)   # pylons
            strip(a - 0.2, a + 0.2, s * 9 - 1.0, s * 9 + 1.0, 8.0, "glow", 0.3)
    for s in (1, -1):
        k.box(-9.6, 9.6, s * 9 - 0.8, s * 9 + 0.8, 11.0, 12.2, "mid")                    # the crane's rails
    k.box(-1.2, 1.2, -10, 10, 12.2, 13.2, "plate")                                      # the bridge
    k.box(-1.0, 1.0, -1.4, 1.4, 9.6, 12.2, "dark")                                      # the hoist
    strip(-0.8, 0.8, -1.0, 1.0, 9.5, "glow", 0.1)
    tube_between((-6, 0, 3.0), (6, 0, 3.0), 0.6, "mid", 8)                              # a barrel being fitted
    for a in (-2.0, 2.0):
        tube_between((a - 0.4, 0, 3.0), (a + 0.4, 0, 3.0), 0.9, "glow", 8)
    for i in range(3):                                                                  # crates of shells
        plate(-10.6, -8.6, -3 + i * 2.2, -1.4 + i * 2.2, 0.5, 2.0, "dark", inset=0.15)
    finish_realistic("factory_artillery", 34.0, serial="0" + str(len("factory_artillery")))


def hq():
    """The command centre: a tiered octagon with sloped walls, team panels round its upper tier, a lit core tower in
    the middle with a ring of light, antenna masts, a bay door at the front."""
    k.begin(0.0)
    hexagon(0, 0, 18.5, 0.0, 0.6, "mid", r1=18.0, turn=math.pi / 8, sides=8)            # foundation
    hexagon(0, 0, 16.5, 0.6, 6.5, "steel", r1=14.0, turn=math.pi / 8, sides=8)          # lower tier
    hexagon(0, 0, 13.0, 6.5, 11.0, "plate", r1=10.0, turn=math.pi / 8, sides=8)         # upper tier
    hexagon(0, 0, 10.4, 11.0, 11.4, "paint", r1=10.0, turn=math.pi / 8, sides=8)        # its roof in team colour
    hexagon(0, 0, 14.2, 6.3, 6.7, "glow", turn=math.pi / 8, sides=8)                    # a lit ring at the step
    hexagon(0, 0, 4.0, 11.4, 22.0, "steel", r1=3.0, sides=8)                            # the core tower
    hexagon(0, 0, 3.4, 15.0, 15.6, "glow", sides=8)                                     # its light rings
    hexagon(0, 0, 3.2, 19.0, 19.6, "glow", sides=8)
    hexagon(0, 0, 3.4, 22.0, 23.0, "plate", r1=2.0, sides=8)
    for s in (1, -1):                                                                   # antenna masts
        k.drum(-7, s * 6, 0.25, 11.4, 26.0, "dark", 6)
        k.drum(-7, s * 6, 0.45, 26.0, 26.6, "glow", 6)
        k.box(-9, -5, s * 6 - 0.15, s * 6 + 0.15, 22.0, 22.3, "dark")
    k.box(13.6, 15.4, -5, 5, 0.6, 5.0, "black")                                        # the bay door
    strip(15.0, 15.6, -5, 5, 5.0, "glow", 0.3)
    for s in (1, -1):
        lens(15.6, s * 8.0, 3.6, 0.7)
        k.box(-4, 4, s * 12.6 - 0.3, s * 12.6 + 0.3, 7.0, 10.0, "dark")                 # side hatches
    finish_realistic("hq", 46.0, serial="0" + str(len("hq")))


def tower():
    """A watch tower: a hex foot, a tapering hex pylon with lit bands, an angular turret with twin guns, a sensor."""
    k.begin(0.0)
    hexagon(0, 0, 7.0, 0, 1.5, "mid", r1=6.4)
    hexagon(0, 0, 4.2, 1.5, 18.0, "steel", r1=2.8)                                      # the pylon
    for z in (6, 11, 15):
        hexagon(0, 0, 4.2 - (z - 1.5) * 0.085 + 0.25, z, z + 0.4, "glow")
    hexagon(0, 0, 4.8, 18.0, 19.4, "plate", r1=4.4)                                     # the platform
    hexagon(0, 0, 4.9, 19.4, 19.8, "paint")                                             # team rim
    plate(-2.8, 2.6, -2.4, 2.4, 19.8, 22.8, "steel", inset=0.5, front=1.2)              # the turret
    for s in (1, -1):
        k.tube(2.0, 8.8, s * 0.8, 21.4, 0.32, "mid", 8)                                  # twin guns
        k.tube(8.0, 9.2, s * 0.8, 21.4, 0.45, "dark", 8)
    lens(2.4, 1.6, 22.2, 0.4)
    k.drum(-1.8, -1.2, 0.1, 22.8, 26.5, "dark", 4)
    k.drum(-1.8, -1.2, 0.2, 26.5, 26.9, "glow", 4)
    finish_realistic("tower", 34.0, serial="0" + str(len("tower")))


def outpost():
    """An outpost: a hexagonal bunker that fits its pad, sloped walls with lit seams, a team-coloured roof, a radar
    mast with a dish, and a small turret at the front."""
    k.begin(0.0)
    hexagon(0, 0, 12.0, 0.0, 4.5, "steel", r1=9.0)                                       # the bunker
    hexagon(0, 0, 9.2, 4.5, 5.2, "plate", r1=8.6)
    hexagon(0, 0, 7.0, 5.2, 5.5, "paint")                                               # team roof
    hexagon(0, 0, 10.6, 2.4, 2.8, "glow", r1=10.2)                                      # a lit band round the walls
    k.drum(-3.5, -3.0, 0.35, 5.2, 13.0, "dark", 6)                                       # radar mast
    k.box(-5.5, -1.5, -3.2, -2.8, 12.0, 14.2, "white")                                   # the radar
    k.drum(-3.5, -3.0, 0.35, 14.2, 14.7, "glow", 6)
    plate(1.0, 5.0, -2.0, 2.0, 5.2, 7.2, "steel", inset=0.4, front=0.9)                  # turret
    k.tube(4.6, 10.0, 0, 6.4, 0.3, "mid", 6)
    lens(11.0, 0.0, 2.0, 0.5)
    finish_realistic("outpost", 34.0, serial="0" + str(len("outpost")))


def extractor():
    """A metal extractor sat on a deposit (the user, 2026-10-20: resources), after Supreme Commander's mass
    extractors: an octagonal pump housing, four pistons leaning in round a glowing green core, team panels."""
    k.begin(0.0)
    hexagon(0, 0, 9.0, 0.0, 1.0, "mid", r1=8.6, sides=8, turn=math.pi / 8)
    hexagon(0, 0, 7.6, 1.0, 5.0, "steel", r1=6.0, sides=8, turn=math.pi / 8)            # pump housing
    hexagon(0, 0, 2.6, 5.0, 11.0, "green", r1=2.0, sides=8)                             # the core: ore drawn up
    hexagon(0, 0, 3.2, 11.0, 12.0, "plate", r1=2.4, sides=8)                            # its cap
    for i in range(4):                                                                  # pistons and their panels
        ang = math.pi / 4 + i * math.pi / 2
        ca, sa = math.cos(ang), math.sin(ang)
        tube_between((ca * 6.4, sa * 6.4, 1.0), (ca * 3.0, sa * 3.0, 10.0), 0.7, "dark", 6)
        tube_between((ca * 5.0, sa * 5.0, 4.6), (ca * 3.6, sa * 3.6, 8.2), 1.0, "mid", 6)
        k.box(ca * 5.6 - 1.2, ca * 5.6 + 1.2, sa * 5.6 - 1.2, sa * 5.6 + 1.2, 5.0, 5.3, "paint")
    hexagon(0, 0, 7.0, 3.0, 3.4, "glow", r1=6.6, sides=8, turn=math.pi / 8)
    finish_realistic("extractor", 34.0, serial="0" + str(len("extractor")))


def deposit():
    """A metal deposit, nobody's: a broken outcrop of dark rock split by glowing green ore, as Supreme Commander shows
    its mass. No team paint: it belongs to whoever builds on it."""
    k.begin(0.0)
    rocks = [(0, 0, 4.5, 4.0), (6, 4, 3.2, 3.0), (-6, 5, 3.6, 3.6), (5, -6, 3.0, 2.8), (-5, -6, 3.8, 3.2),
             (10, -1, 2.2, 1.8), (-10, 0, 2.6, 2.2), (1, 10, 2.4, 2.0), (0, -11, 2.2, 1.8)]
    for i, (a, x, r, h) in enumerate(rocks):
        k.drum(a, x, r, 0, h, "dark" if i % 2 else "mid", 5 + i % 3, r * 0.45)
    for a, x, r, h in ((2.5, 1.5, 1.1, 7.0), (-2.5, 2.5, 1.0, 6.0), (3, -3, 0.9, 5.4), (-3.5, -3, 1.0, 5.8),
                       (8, 2, 0.7, 4.2), (-8, 3, 0.8, 4.6), (0, 6.5, 0.8, 4.0), (1, -8, 0.7, 3.8)):
        k.drum(a, x, r, 0, h, "green", 6, 0.12)                                         # the ore: glowing shards
    hexagon(0, 0, 3.0, 0.0, 0.3, "green", sides=6)                                      # a glow pooled at its heart
    finish_realistic("deposit", 34.0, serial="0" + str(len("deposit")))


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"factory_tank": factory_tank, "factory_artillery": factory_artillery, "factory_lighttank": factory_lighttank, "factory_mortar": factory_mortar, "factory_spider": factory_spider,
 "hq": hq, "tower": tower, "outpost": outpost, "extractor": extractor, "deposit": deposit}[which]()
