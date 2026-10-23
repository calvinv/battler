"""The base in the cel shaded look: model_base.py -- <name>
(The user, 2026-10-23: "lets work on the buildings they come out of"; FLUX concepts art/_gen/concepts/bld_sheet_s801.png,
picks 2 = HQ core, 8 = hover bay, 11 = mech bay, 15 = wheel bay.) Same flat slate + team paint + warm lights as the
units (model_hover.py). Each wing's door is a rig part ("door") the game slides up while a unit rolls out; parts named
spin_* turn slowly (game/BuildingRig.cs).
Names: hq factory_lighttank factory_ranger factory_tank connector port factory_mortar factory_artillery factory_mobilebase factory_air outpost extractor powerplant
       module_speed module_armour module_attack module_build module_energy module_twin module_napalm module_shield module_magnet module_blank
Run: blender --background --python tools/art/model_base.py -- hq"""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *
import math

k.recolour(steel=(70, 76, 86), plate=(58, 63, 72), mid=(88, 94, 104), dark=(38, 41, 47), black=(20, 22, 26),
           glow=(255, 190, 70), glass_lit=(90, 232, 255), white=(206, 212, 220), hazard=(58, 63, 72))


def ring_of(a, x, r, z, count, fn):
    for i in range(count):
        t = i * math.tau / count
        fn(a + math.cos(t) * r, x + math.sin(t) * r, t)


def light_bar(a0, a1, x0, x1, z0, z1):
    """A small lit window strip in a dark frame."""
    k.box(a0, a1, x0, x1, z0, z1, "dark")
    k.box(a0 - 0.02, a1 + 0.02, x0 + 0.08, x1 - 0.08, z0 + 0.08, z1 - 0.08, "glow")


def base_slab(r, h=0.7):
    """An octagonal foundation, a lit marker at each corner."""
    hexagon(0, 0, r, 0.0, h, "mid", r1=r - 0.5, sides=8, turn=math.pi / 8)
    ring_of(0, 0, r - 0.8, h, 8, lambda a, x, t: k.drum(a, x, 0.28, h, h + 0.15, "glow", 6))


def hq():
    """HQ core (concept 2): a low octagonal hub in two stepped tiers with sloped facets, lit window strips and team
    panels round it, a ring of vents, and on top a raised drum with a big amber eye that turns (spin_eye)."""
    k.begin(0.0)
    base_slab(17.0)
    t = math.pi / 8
    hexagon(0, 0, 14.0, 0.7, 5.5, "steel", r1=13.0, sides=8, turn=t)                     # the lower tier
    hexagon(0, 0, 13.2, 5.5, 6.4, "plate", r1=11.4, sides=8, turn=t)                     # its sloped shoulder
    hexagon(0, 0, 10.6, 6.4, 9.8, "steel", r1=9.8, sides=8, turn=t)                      # the upper tier
    hexagon(0, 0, 10.0, 9.8, 10.6, "plate", r1=8.2, sides=8, turn=t)
    for i in range(8):                                                                     # each lower face: a window
        th = i * math.tau / 8                                                              # strip, a team panel, buttresses
        c, s = math.cos(th), math.sin(th)
        def at(rad, w):
            return rad * c - w * s, rad * s + w * c
        for w in (-3.2, 3.2):                                                              # buttresses at the corners of the face
            p0, p1 = at(13.9, w), at(15.3, w)
            tube_between((p0[0], p0[1], 0.7), (p0[0], p0[1], 5.0), 0.55, "plate", 6)
            tube_between((p1[0], p1[1], 0.7), (p0[0], p0[1], 4.0), 0.45, "dark", 6)
        # A window strip (a thin box turned onto the face).
        for w0 in (-2.0, 0.5):
            pa, pb = at(13.65, w0), at(13.65, w0 + 1.5)
            tube_between((pa[0], pa[1], 3.2), (pb[0], pb[1], 3.2), 0.32, "glow", 4)
        pa, pb = at(13.1, -2.4), at(13.1, 2.4)
        tube_between((pa[0], pa[1], 5.9), (pb[0], pb[1], 5.9), 0.38, "paint", 4)             # team band on the shoulder
        pa, pb = at(10.45, -1.6), at(10.45, 1.6)
        tube_between((pa[0], pa[1], 8.2), (pb[0], pb[1], 8.2), 0.3, "glow", 4)
    # The gate at the front.
    k.box(12.6, 14.4, -2.4, 2.4, 0.7, 4.6, "black")
    k.box(14.0, 14.5, -2.8, 2.8, 4.6, 5.2, "paint")
    # Vents round the top.
    ring_of(0, 0, 9.0, 10.6, 8, lambda a, x, th: k.box(a - 0.5, a + 0.5, x - 0.5, x + 0.5, 10.6, 11.0, "dark"))
    # The eye: a drum, a ring of amber segments under a dark visor that turns, a glowing lens on top.
    k.drum(0, 0, 6.0, 10.6, 12.2, "mid", 16, r1=5.4)
    with part("spin_eye", (0, 0, 12.2)):
        k.drum(0, 0, 5.2, 12.2, 13.4, "dark", 16, r1=4.6)
        for i in range(6):
            th = i * math.tau / 6
            a, x = math.cos(th) * 4.9, math.sin(th) * 4.9
            k.box(a - 0.5, a + 0.5, x - 0.5, x + 0.5, 12.5, 13.1, "glow")
        k.drum(0, 0, 3.6, 13.4, 14.4, "plate", 12, r1=2.8)
    k.drum(0, 0, 2.6, 14.4, 15.0, "glow", 20, r1=2.2)
    k.drum(0, 0, 1.2, 15.0, 15.3, "white", 16, r1=0.8)
    for s in (1, -1):                                                                      # two short antennas
        k.drum(-3.5, s * 7.0, 0.18, 10.6, 15.5, "dark", 6)
        k.drum(-3.5, s * 7.0, 0.35, 15.5, 15.9, "glow", 6)
    finish_toon("hq", 40.0)


def wing_door(a, x0, x1, z0, z1, mat="dark", ribs=True):
    """A shutter door across the front at `a`, a rig part the game lifts."""
    with part("door", (a, 0, z0)):
        k.box(a - 0.35, a, x0, x1, z0, z1, mat)
        if ribs:
            z = z0 + 0.6
            while z < z1 - 0.3:
                k.box(a, a + 0.08, x0 + 0.2, x1 - 0.2, z, z + 0.18, "black")
                z += 0.9
        k.box(a, a + 0.1, x0 + 0.2, x1 - 0.2, z0 + 0.1, z0 + 0.4, "paint")


def factory_lighttank():
    """Hover bay (concept 8): a low wide hangar, its whole front a shutter, orange-lit inside, team panels on its
    roof, a glowing launch pad laid out in front with landing marks."""
    k.begin(0.0)
    plate(-9.0, 10.5, -8.0, 8.0, 0.0, 0.6, "mid", inset=0.3)                               # foundation + pad
    k.box(2.0, 10.0, -4.6, 4.6, 0.6, 0.7, "dark")                                          # the launch pad
    for s in (1, -1):
        k.box(2.4, 9.6, s * 4.3 - 0.15, s * 4.3 + 0.15, 0.7, 0.75, "glass_lit")
        for a in (3.5, 6.0, 8.5):
            k.box(a - 0.6, a + 0.6, s * 2.2 - 0.12, s * 2.2 + 0.12, 0.7, 0.75, "paint")
    # The hangar: back wall, side walls, a thick roof with an overhang at the front.
    plate(-8.5, 2.0, -7.5, 7.5, 0.6, 7.4, "steel", inset=0.3, front=0.0, back=0.8)
    plate(-8.8, 2.6, -7.8, 7.8, 7.4, 8.6, "plate", inset=0.5, front=0.0, back=0.6)
    k.box(-0.5, 2.2, -5.4, 5.4, 0.6, 6.9, "black")                                         # the bay (dark inside)
    k.box(-0.4, -0.2, -5.0, 5.0, 6.2, 6.5, "glow")                                         # a light along its top
    for s in (1, -1):                                                                      # the door frame, hazard posts
        plate(1.4, 2.6, *sorted((s * 5.4, s * 7.8)), 0.6, 7.4, "plate", inset=0.15)
        for z in (1.5, 3.0, 4.5, 6.0):
            k.box(2.6, 2.68, *sorted((s * 5.6, s * 7.0)), z, z + 0.5, "paint")
    wing_door(2.2, -5.4, 5.4, 0.6, 6.9)
    # Roof: team panels, a vent box, a small sensor with a light.
    for i, a in enumerate((-6.5, -3.2, 0.1)):
        k.box(a, a + 2.6, -6.4, -3.6, 8.6, 8.75, "paint")
        k.box(a, a + 2.6, 3.6, 6.4, 8.6, 8.75, "paint" if i != 1 else "dark")
    plate(-6.0, -1.0, -1.8, 1.8, 8.6, 9.8, "mid", inset=0.3)
    for a in (-5.2, -4.0, -2.8, -1.6):
        k.box(a, a + 0.6, -1.4, 1.4, 9.8, 9.9, "black")
    k.box(-8.0, -7.0, 5.5, 6.5, 8.6, 10.0, "dark")
    k.box(-7.8, -7.2, 5.7, 6.3, 10.0, 10.3, "glow")
    for s in (1, -1):                                                                      # side lights
        light_bar(-6.0, -1.0, *sorted((s * 7.5, s * 7.6)), 4.8, 5.4)
    finish_toon("factory_lighttank", 24.0)


def factory_ranger():
    """Mech bay (concept 11): a tall narrow assembly tower, a stepped upper block, a tall doorway at the foot of its
    front, a buttress with a lit column up one side, a mast and a slowly turning sensor dish on the roof."""
    k.begin(0.0)
    plate(-7.0, 8.5, -6.5, 6.5, 0.0, 0.6, "mid", inset=0.3)
    k.box(3.6, 5.6, -2.6, 2.6, 0.6, 0.9, "plate")
    k.box(5.6, 7.0, -2.4, 2.4, 0.6, 0.75, "plate")
    # The tower: a lower block, a narrower upper block, a cap.
    plate(-4.6, 3.6, -4.4, 4.4, 0.6, 10.0, "steel", inset=0.25)
    plate(-4.2, 2.6, -3.6, 3.6, 10.0, 16.0, "plate", inset=0.3)
    plate(-3.8, 2.0, -3.0, 3.0, 16.0, 17.2, "mid", inset=0.4)
    for s in (1, -1):                                                                      # team bands
        k.box(-4.2, 3.4, *sorted((s * 4.25, s * 4.45)), 7.4, 8.0, "paint")
        k.box(-3.8, 2.4, *sorted((s * 3.45, s * 3.65)), 13.8, 14.3, "paint")
    k.box(3.3, 3.62, -3.8, 3.8, 9.0, 9.6, "paint")
    # The doorway: a deep frame, dark inside, a lit strip over it.
    k.box(3.0, 3.7, -2.0, 2.0, 0.6, 8.0, "black")
    for s in (1, -1):
        k.box(3.4, 4.1, *sorted((s * 2.0, s * 2.6)), 0.6, 8.4, "plate")
    k.box(3.4, 4.1, -2.6, 2.6, 8.0, 8.6, "plate")
    k.box(4.1, 4.15, -1.6, 1.6, 8.15, 8.4, "glow")
    wing_door(3.7, -2.0, 2.0, 0.6, 8.0)
    # Window slits up the upper block.
    for z in (11.0, 12.4):
        light_bar(2.55, 2.65, -2.2, -0.6, z, z + 0.6)
        light_bar(2.55, 2.65, 0.6, 2.2, z, z + 0.6)
    # A buttress up the left side with a lit column, pipes up the right.
    plate(-3.0, 1.6, 4.4, 6.0, 0.6, 12.0, "plate", inset=0.2, front=0.6)
    k.box(1.0, 1.1, 4.8, 5.6, 1.5, 11.0, "glow")
    for a in (-2.6, -1.8):
        k.drum(a, -4.7, 0.3, 0.6, 15.0, "dark", 8)
    # Roof: a mast with a light, a sensor dish that turns.
    k.drum(-2.0, 1.4, 0.2, 17.2, 22.5, "dark", 6)
    k.drum(-2.0, 1.4, 0.4, 22.5, 22.9, "glow", 6)
    with part("spin_dish", (0.3, -0.8, 17.2)):
        k.drum(0.3, -0.8, 0.6, 17.2, 18.4, "mid", 8)
        tube_between((0.3, -0.8, 18.6), (1.6, -0.8, 19.6), 1.4, "steel", 12, 0.3)
        k.drum(1.6, -0.8, 0.2, 19.4, 19.9, "glow", 6)
    finish_toon("factory_ranger", 26.0)


def factory_tank():
    """Wheel bay (concept 15): a domed round workshop of stacked rings with team bands, a big arched doorway with a
    roll shutter and a ramp down from it, a hatch ring and vent on the crown."""
    k.begin(0.0)
    base_slab(10.5, 0.6)
    # The dome: stacked tapering rings, seams between them.
    rings = [(9.0, 0.6), (9.0, 3.0), (8.6, 5.0), (7.6, 7.0), (6.0, 8.6), (3.6, 9.8), (2.2, 10.2)]
    for (r0, z0), (r1, z1) in zip(rings, rings[1:]):
        k.drum(0, 0, r0, z0, z1, "steel" if z0 < 7 else "plate", 24, r1=r1)
    for (r, z) in rings[1:5]:
        k.drum(0, 0, r + 0.12, z - 0.12, z + 0.12, "dark", 24)
    k.drum(0, 0, 9.12, 3.6, 4.4, "paint", 24, r1=9.0)                                      # a team band round it
    # Ribs over the dome.
    for i in range(8):
        th = i * math.tau / 8 + math.pi / 8
        if abs(math.cos(th)) > 0.85 and math.cos(th) > 0:
            continue                                                                       # (not over the door)
        pts = [(math.cos(th) * (r + 0.25), math.sin(th) * (r + 0.25), z) for r, z in rings[:6]]
        for p0, p1 in zip(pts, pts[1:]):
            tube_between(p0, p1, 0.32, "mid", 6)
    # The arched doorway: a frame standing out of the dome, an arch over it, the shutter, the ramp.
    plate(6.2, 9.8, -5.4, 5.4, 0.6, 6.2, "plate", inset=0.2, front=0.3)
    k.upright([(9.3, 6.2), (9.8, 6.2), (9.6, 7.6), (7.0, 8.4)], -4.6, 4.6, "plate")
    k.box(8.8, 9.9, -3.9, 3.9, 0.6, 5.8, "black")
    for s in (1, -1):
        for z in (1.2, 2.6, 4.0):
            k.box(9.8, 9.88, *sorted((s * 4.1, s * 5.1)), z, z + 0.6, "paint")
        k.box(9.85, 9.95, *sorted((s * 4.0, s * 4.15)), 0.9, 5.6, "glow")
    k.box(9.85, 9.95, -3.8, 3.8, 5.85, 6.0, "glow")
    wing_door(9.9, -3.9, 3.9, 0.6, 5.8)
    k.upright([(9.9, 0.0), (9.9, 1.4), (14.5, 0.0)], -3.6, 3.6, "mid")                       # the ramp
    for a in (10.8, 11.8, 12.8, 13.8):
        z = 1.4 - (a - 9.9) * 1.4 / 4.6
        k.box(a, a + 0.3, -3.3, 3.3, z - 0.05, z + 0.08, "dark")
    for s in (1, -1):
        k.box(10.0, 14.2, *sorted((s * 3.5, s * 3.7)), 0.0, 0.5, "paint")
    # Crown: a hatch ring and a vent stack; side lights.
    k.drum(0, 0, 2.4, 10.2, 10.8, "mid", 16)
    k.drum(0, 0, 1.6, 10.8, 11.0, "glow", 16)
    k.drum(-3.4, 2.8, 0.9, 8.4, 11.6, "dark", 10)
    k.drum(-3.4, 2.8, 1.0, 11.6, 11.9, "plate", 10)
    for s in (1, -1):
        light_bar(-1.0, 1.0, *sorted((s * 8.9, s * 9.05)), 1.6, 2.2)
    finish_toon("factory_tank", 26.0)


def connector():
    """A wing's link to its base (the user, 2026-10-23: "make the main bases modular": wings snap onto the HQ): an
    armoured corridor 10 long from a = 0 (the base end) to a = 10 (the wing end), ribbed, a team stripe and two
    conduits along its roof, lit strips down its sides, glowing chevrons pointing out toward the wing. The game
    stretches it to the gap."""
    k.begin(0.0)
    k.box(0, 10, -2.7, 2.7, 0.0, 0.5, "mid")
    plate(0, 10, -2.3, 2.3, 0.5, 3.6, "steel", inset=0.45, front=0.0, back=0.0)
    for a in (0.0, 3.3, 6.6, 9.4):                                                         # ribs
        plate(a, a + 0.6, -2.55, 2.55, 0.5, 3.85, "plate", inset=0.5, front=0.0, back=0.0)
    k.box(0, 10, -0.5, 0.5, 3.6, 3.7, "paint")
    for x in (-1.2, 1.2):
        tube_between((0, x, 3.85), (10, x, 3.85), 0.28, "dark", 8)
    for s in (1, -1):
        k.box(0.8, 9.2, *sorted((s * 2.27, s * 2.33)), 1.6, 1.85, "glow")
    for a in (1.6, 4.9, 8.0):                                                              # chevrons on the roof
        for s in (1, -1):
            k.solid([(a, 0, 3.72), (a + 0.6, 0, 3.72), (a - 0.4, s * 0.9, 3.72), (a - 1.0, s * 0.9, 3.72)],
                    [(a, 0, 3.8), (a + 0.6, 0, 3.8), (a - 0.4, s * 0.9, 3.8), (a - 1.0, s * 0.9, 3.8)], "glow")
    finish_toon("connector", 14.0)


def port():
    """An empty docking port on a base's wall: a short stub of the connector's corridor standing out from the wall,
    capped with a big hatch framed in hazard bars and a lamp each side, waiting for a wing."""
    k.begin(0.0)
    k.box(0, 5.0, -2.9, 2.9, 0.0, 0.5, "mid")
    plate(0, 4.2, -2.3, 2.3, 0.5, 3.6, "steel", inset=0.45, front=0.0, back=0.0)
    plate(1.4, 2.0, -2.55, 2.55, 0.5, 3.85, "plate", inset=0.5, front=0.0, back=0.0)
    k.box(0, 4.2, -0.5, 0.5, 3.6, 3.7, "paint")
    for x in (-1.2, 1.2):
        tube_between((0, x, 3.85), (4.2, x, 3.85), 0.28, "dark", 8)
    # The cap: a collar bigger than the corridor, a round hatch in it, hazard bars round the hatch.
    plate(4.0, 5.0, -2.9, 2.9, 0.5, 4.4, "plate", inset=0.2, front=0.15, back=0.0)
    k.tube(4.95, 5.15, 0, 2.3, 1.45, "dark", 20)
    k.tube(5.15, 5.25, 0, 2.3, 1.15, "black", 20)
    k.box(5.2, 5.3, -0.08, 0.08, 1.2, 3.4, "dark")
    for i in range(8):
        t = i * math.tau / 8 + math.pi / 8
        c, d = math.cos(t), math.sin(t)
        k.box(4.98, 5.08, c * 1.75 - 0.22, c * 1.75 + 0.22, 2.3 + d * 1.75 - 0.22, 2.3 + d * 1.75 + 0.22, "paint" if i % 2 else "glow")
    for s_ in (1, -1):
        k.box(4.9, 5.2, s_ * 2.4 - 0.25, s_ * 2.4 + 0.25, 3.9, 4.6, "glow")
    finish_toon("port", 12.0)


def bay_hall(back, front, half, high, door_half, door_high, roof=True):
    """A wing's hall with a bay: foundation, walls, a thick roof overhanging the front, a door frame with team bars,
    a dark bay inside, a light along its top, the shutter door (a rig part). Returns the roof height."""
    plate(back - 0.6, front + 0.4, -half - 0.6, half + 0.6, 0.0, 0.6, "mid", inset=0.3)
    plate(back, front, -half, half, 0.6, high, "steel", inset=0.3, front=0.0, back=0.8)
    top = high
    if roof:
        plate(back - 0.3, front + 0.6, -half - 0.3, half + 0.3, high, high + 1.2, "plate", inset=0.5, front=0.0, back=0.6)
        top = high + 1.2
    k.box(front - 2.5, front + 0.2, -door_half, door_half, 0.6, door_high, "black")
    k.box(front - 2.4, front - 2.2, -door_half + 0.4, door_half - 0.4, door_high - 0.7, door_high - 0.4, "glow")
    for s_ in (1, -1):
        plate(front - 1.0, front + 0.6, *sorted((s_ * door_half, s_ * (door_half + 2.0))), 0.6, high, "plate", inset=0.15)
        z = 1.5
        while z < door_high - 0.5:
            k.box(front + 0.6, front + 0.68, *sorted((s_ * (door_half + 0.2), s_ * (door_half + 1.4))), z, z + 0.5, "paint")
            z += 1.5
    wing_door(front + 0.2, -door_half, door_half, 0.6, door_high)
    for s_ in (1, -1):
        light_bar(back + 1.5, back + 5.0, *sorted((s_ * half, s_ * (half + 0.1))), high * 0.6, high * 0.6 + 0.6)
    return top


def pad(a0, a1, half):
    """A lit apron in front of a bay, landing marks in team paint."""
    k.box(a0, a1, -half, half, 0.6, 0.7, "dark")
    for s_ in (1, -1):
        k.box(a0 + 0.4, a1 - 0.4, s_ * (half - 0.3) - 0.15, s_ * (half - 0.3) + 0.15, 0.7, 0.75, "glass_lit")
        a = a0 + 1.4
        while a < a1 - 1.0:
            k.box(a - 0.5, a + 0.5, s_ * (half * 0.45) - 0.12, s_ * (half * 0.45) + 0.12, 0.7, 0.75, "paint")
            a += 2.4


def factory_mortar():
    """Mortar bay: a squat square bay, and on its roof a rack of three stubby launch tubes tilted up and out,
    ammunition bins beside them."""
    k.begin(0.0)
    top = bay_hall(-7.5, 2.5, 7.0, 6.2, 5.4, 5.6)
    pad(2.5, 9.0, 5.6)
    plate(-6.5, 0.5, -5.0, 5.0, top, top + 0.8, "mid", inset=0.4)
    for x in (-2.6, 0.0, 2.6):
        base = (-3.4, x, top + 0.8)
        tip = (-0.6, x, top + 5.0)
        tube_between(base, tip, 1.0, "dark", 14)
        mid = ((base[0] + tip[0]) / 2, x, (base[2] + tip[2]) / 2)
        tube_between((mid[0] - 0.2, x, mid[2] - 0.3), (mid[0] + 0.2, x, mid[2] + 0.3), 1.15, "paint", 14)
        tube_between(tip, (tip[0] + 0.18, x, tip[2] + 0.27), 0.75, "black", 14)
    for x in (-5.0, 4.0):
        plate(-6.0, -4.0, x, x + 1.2, top + 0.8, top + 2.0, "plate", inset=0.15)
        k.box(-5.6, -4.4, x + 0.2, x + 1.0, top + 2.0, top + 2.08, "glow")
    finish_toon("factory_mortar", 22.0)


def factory_artillery():
    """Artillery works: a long low hall, and over its pad a tall gantry crane on two legs with a long gun barrel
    slung under its beam, lamps on the legs."""
    k.begin(0.0)
    top = bay_hall(-9.5, 2.5, 7.0, 6.6, 5.4, 6.0)
    pad(2.5, 10.0, 5.6)
    for x in (-3.5, 0.0, 3.5):
        k.box(-8.0, -2.0, x - 0.8, x + 0.8, top, top + 0.5, "dark")
    for s_ in (1, -1):
        plate(8.6, 9.8, *sorted((s_ * 6.4, s_ * 7.6)), 0.6, 11.0, "plate", inset=0.2)
        k.box(9.85, 9.95, *sorted((s_ * 6.7, s_ * 7.3)), 3.0, 9.0, "glow")
    for s_ in (1, -1):                                                                     # rails back over the roof
        plate(-4.0, 10.4, *sorted((s_ * 6.4, s_ * 7.6)), 11.0, 12.0, "steel", inset=0.15, front=0.0, back=0.0)
    plate(8.6, 9.8, -7.6, 7.6, 11.0, 12.4, "steel", inset=0.2)                             # the crossbeam
    for x in (-4.0, 0.0, 4.0):
        k.box(9.8, 9.9, x - 0.9, x + 0.9, 11.4, 12.0, "paint")
    plate(0.6, 2.2, -7.6, 7.6, 11.0, 12.0, "plate", inset=0.15)                            # a trolley beam
    for (a0, x0) in ((1.0, -0.6),):
        k.box(a0, a0 + 0.8, x0, x0 + 1.2, 9.9, 11.0, "mid")
    tube_between((1.0, 0, 9.4), (9.5, 0, 9.4), 0.55, "dark", 12)
    tube_between((8.6, 0, 9.4), (9.9, 0, 9.4), 0.7, "mid", 12)
    for a in (2.5, 7.0):
        tube_between((a, 0, 10.0), (a, 0, 11.0), 0.12, "black", 6)
    finish_toon("factory_artillery", 26.0)


def factory_mobilebase():
    """Utility yard: a wide low garage with three doors (the mobile bases are too big for a bay: they roll out of
    the yard), a crane turning over the yard, a radio mast, stacked supply crates."""
    k.begin(0.0)
    plate(-8.5, 10.5, -9.0, 9.0, 0.0, 0.6, "mid", inset=0.3)
    plate(-8.0, -1.0, -8.5, 8.5, 0.6, 6.0, "steel", inset=0.3, back=0.8, front=0.0)
    plate(-8.3, -0.7, -8.8, 8.8, 6.0, 7.0, "plate", inset=0.4, front=0.0)
    for x in (-4.5, 0.0, 4.5):
        k.box(-1.3, -0.9, x - 1.8, x + 1.8, 0.6, 4.6, "dark")
        for z in (1.2, 2.2, 3.2):
            k.box(-0.9, -0.85, x - 1.6, x + 1.6, z, z + 0.15, "black")
        k.box(-0.95, -0.85, x - 1.8, x + 1.8, 4.6, 4.9, "paint")
    pad(-1.0, 9.5, 7.0)
    for i, (a, x, z) in enumerate(((6.6, -7.8, 0.6), (8.2, -7.8, 0.6), (7.4, -7.8, 2.0), (6.6, 6.4, 0.6))):
        plate(a, a + 1.4, x, x + 1.4, z, z + 1.4, "plate" if i % 2 else "mid", inset=0.1)
        k.box(a + 0.3, a + 1.1, x + 1.4, x + 1.45, z + 0.5, z + 0.9, "paint")
    with part("spin_crane", (-3.0, 5.0, 7.0)):
        k.drum(-3.0, 5.0, 1.0, 7.0, 8.2, "mid", 10)
        tube_between((-3.0, 5.0, 8.0), (5.5, 5.0, 10.0), 0.45, "plate", 8)
        tube_between((5.3, 5.0, 9.9), (5.3, 5.0, 6.5), 0.08, "black", 4)
        k.box(4.9, 5.7, 4.6, 5.4, 6.0, 6.5, "glow")
    k.drum(-6.5, -6.5, 0.2, 7.0, 13.0, "dark", 6)
    k.drum(-6.5, -6.5, 0.4, 13.0, 13.4, "glow", 6)
    finish_toon("factory_mobilebase", 28.0)


def factory_air():
    """Air pad: a raised octagonal landing deck with a ring of lights, a team circle and an H, a hangar block behind
    it with a control cab and a turning radar, fuel tanks."""
    k.begin(0.0)
    hexagon(1.5, 0, 8.6, 0.0, 1.4, "plate", r1=8.0, sides=8, turn=math.pi / 8)
    hexagon(1.5, 0, 7.6, 1.4, 1.5, "dark", sides=8, turn=math.pi / 8)
    k.drum(1.5, 0, 5.2, 1.5, 1.56, "paint", 32)
    k.drum(1.5, 0, 4.5, 1.5, 1.58, "dark", 32)
    for x in (-1.6, 1.6):
        k.box(-0.2, 3.2, x - 0.3, x + 0.3, 1.58, 1.62, "white")
    k.box(1.2, 1.8, -1.6, 1.6, 1.58, 1.62, "white")
    for i in range(8):
        t = i * math.tau / 8
        k.drum(1.5 + math.cos(t) * 6.9, math.sin(t) * 6.9, 0.3, 1.5, 1.75, "glow", 6)
    plate(-9.5, -5.0, -6.5, 6.5, 0.0, 5.0, "steel", inset=0.3)
    k.box(-5.4, -5.0, -4.5, 4.5, 0.6, 4.0, "black")
    plate(-9.0, -6.0, 3.0, 6.0, 5.0, 8.5, "plate", inset=0.3)
    k.box(-6.3, -5.9, 3.4, 5.6, 6.6, 7.6, "glass_lit")
    k.box(-9.2, -5.4, -6.2, 6.2, 4.2, 4.6, "paint")
    for x in (-4.6, -2.4):
        k.drum(-10.8, x, 1.0, 0.0, 3.4, "mid", 12)
        k.drum(-10.8, x, 1.05, 2.4, 2.7, "paint", 12)
    k.drum(-7.5, 4.5, 0.15, 8.5, 11.0, "dark", 6)
    with part("spin_radar", (-7.5, 4.5, 11.0)):
        k.box(-7.7, -7.3, 3.0, 6.0, 11.0, 11.6, "mid")
    finish_toon("factory_air", 26.0)


def outpost():
    """Outpost (a mobile base set up in the field): a small hexagonal hub in two tiers, team bands, a light twin gun
    on top facing out, a radar dish turning on a mast behind it, stabiliser legs braced out round it."""
    k.begin(0.0)
    base_slab(12.5, 0.6)
    hexagon(0, 0, 10.0, 0.6, 4.5, "steel", r1=9.2, sides=6, turn=math.pi / 6)
    hexagon(0, 0, 9.4, 4.5, 5.3, "plate", r1=7.6, sides=6, turn=math.pi / 6)
    hexagon(0, 0, 7.2, 5.3, 7.4, "steel", r1=6.6, sides=6, turn=math.pi / 6)
    for i in range(6):
        t = i * math.tau / 6
        c, d = math.cos(t), math.sin(t)
        tube_between((c * 9.0, d * 9.0, 3.4), (c * 12.0, d * 12.0, 0.6), 0.5, "dark", 6)    # stabiliser legs
        k.drum(c * 12.0, d * 12.0, 0.9, 0.6, 1.0, "plate", 8)
        t2 = t + math.pi / 6
        c2, d2 = math.cos(t2), math.sin(t2)
        tube_between((c2 * 9.55 - d2 * 1.6, d2 * 9.55 + c2 * 1.6, 2.8), (c2 * 9.55 + d2 * 1.6, d2 * 9.55 - c2 * 1.6, 2.8), 0.3, "glow", 4)
        tube_between((c2 * 8.3 - d2 * 2.0, d2 * 8.3 + c2 * 2.0, 4.9), (c2 * 8.3 + d2 * 2.0, d2 * 8.3 - c2 * 2.0, 4.9), 0.35, "paint", 4)
    # The gun: a turret block with two short barrels, facing out (forward).
    hexagon(1.0, 0, 3.0, 7.4, 8.6, "plate", r1=2.6, sides=8, turn=math.pi / 8)
    plate(0.0, 3.0, -1.6, 1.6, 8.6, 10.0, "mid", inset=0.3, front=0.6)
    for x in (-0.7, 0.7):
        k.tube(3.0, 7.0, x, 9.3, 0.28, "dark", 10)
        k.tube(6.6, 7.1, x, 9.3, 0.36, "dark", 10)
    k.box(2.9, 3.0, -1.2, 1.2, 9.6, 9.8, "glow")
    k.drum(-4.0, 0, 0.3, 7.4, 11.5, "dark", 6)
    with part("spin_radar", (-4.0, 0, 11.5)):
        k.drum(-4.0, 0, 0.6, 11.5, 12.0, "mid", 8)
        k.box(-4.4, -3.6, -2.4, 2.4, 12.0, 13.2, "plate")
        k.box(-3.62, -3.55, -2.0, 2.0, 12.3, 12.9, "glow")
    finish_toon("outpost", 30.0)


def extractor():
    """Extractor: an octagonal deck over the deposit, a four-legged derrick tapering up over a glowing bore, a drill
    head turning in it (spin_drill), two ore hoppers and a conveyor."""
    k.begin(0.0)
    base_slab(9.5, 0.6)
    hexagon(0, 0, 7.6, 0.6, 2.2, "steel", r1=7.0, sides=8, turn=math.pi / 8)
    k.drum(0, 0, 3.0, 2.2, 2.35, "glow", 20)                                               # the glowing bore
    k.drum(0, 0, 2.4, 2.2, 2.4, "black", 20)
    for c in ((1, 1), (1, -1), (-1, 1), (-1, -1)):                                          # the derrick legs
        tube_between((c[0] * 3.6, c[1] * 3.6, 2.2), (c[0] * 1.0, c[1] * 1.0, 13.0), 0.35, "plate", 6)
    for z, r in ((6.0, 2.85), (9.5, 1.95)):                                                  # cross bracing rings
        hexagon(0, 0, r + 0.2, z, z + 0.4, "dark", sides=4, turn=math.pi / 4)
    plate(-1.6, 1.6, -1.6, 1.6, 13.0, 14.0, "mid", inset=0.3)
    k.box(-0.4, 0.4, -1.7, 1.7, 13.2, 13.8, "paint")
    k.drum(0, 0, 0.3, 14.0, 15.2, "glow", 8)
    with part("spin_drill", (0, 0, 2.4)):
        k.drum(0, 0, 0.5, 2.4, 12.5, "dark", 8)
        for z in (3.5, 6.0, 8.5, 11.0):
            for i in range(3):
                t = i * math.tau / 3 + z
                tube_between((0, 0, z), (math.cos(t) * 1.3, math.sin(t) * 1.3, z + 0.6), 0.2, "mid", 6)
    for x in (-5.0, 5.0):                                                                  # ore hoppers
        k.drum(-4.0, x, 1.6, 2.2, 5.6, "steel", 8, r1=1.9)
        k.drum(-4.0, x, 1.95, 5.6, 5.8, "paint", 8)
        k.drum(-4.0, x, 1.6, 5.8, 5.85, "black", 8)
    tube_between((-4.0, -5.0, 6.0), (-1.0, 0, 3.0), 0.4, "mid", 6)
    tube_between((-4.0, 5.0, 6.0), (-1.0, 0, 3.0), 0.4, "mid", 6)
    finish_toon("extractor", 24.0)


def powerplant():
    """Power plant on a vent: a round reactor drum ringed with glowing coils, two turbine fans turning in housings
    on its top, heavy pipes down to the vent."""
    k.begin(0.0)
    base_slab(10.5, 0.6)
    k.drum(0, 0, 7.4, 0.6, 3.0, "steel", 24, r1=7.0)
    k.drum(0, 0, 6.8, 3.0, 8.5, "plate", 24, r1=6.2)
    for z in (4.0, 5.6, 7.2):
        k.drum(0, 0, 6.85 - (z - 3.0) * 0.11, z, z + 0.45, "glow", 24)
    k.drum(0, 0, 6.3, 8.5, 9.2, "mid", 24, r1=5.6)
    k.drum(0, 0, 7.15, 2.0, 2.6, "paint", 24)
    for x in (-2.6, 2.6):                                                                  # fan housings
        k.drum(0, x, 2.4, 9.2, 10.4, "dark", 16)
        k.drum(0, x, 1.9, 10.4, 10.45, "black", 16)
        with part("spin_fan" + ("L" if x > 0 else "R"), (0, x, 10.0)):
            for i in range(5):
                t = i * math.tau / 5
                k.solid([(0, x, 9.95), (math.cos(t) * 1.85, x + math.sin(t) * 1.85, 9.95), (math.cos(t + 0.5) * 1.85, x + math.sin(t + 0.5) * 1.85, 9.95)],
                        [(0, x, 10.15), (math.cos(t) * 1.85, x + math.sin(t) * 1.85, 10.15), (math.cos(t + 0.5) * 1.85, x + math.sin(t + 0.5) * 1.85, 10.15)], "mid")
            k.drum(0, x, 0.4, 9.9, 10.3, "glow", 8)
    for i in range(4):                                                                     # pipes to the vent
        t = i * math.tau / 4 + math.pi / 4
        c, d = math.cos(t), math.sin(t)
        tube_between((c * 6.9, d * 6.9, 5.0), (c * 9.2, d * 9.2, 0.6), 0.6, "dark", 8)
    finish_toon("powerplant", 26.0)


# ---- modules: links in a line (the user, 2026-10-23: "like a rube goldberg machine ... the modules should be speed,
# armour, attack, build speed, energy cost, wildcards"). The line runs through each along its length (a); each has
# its own light colour and a feature on top that says what it does. Moving parts spin_* turn (BuildingRig).

def module_core(colour):
    """A module's body: a low armoured block the line runs through, collars at both ends where the connectors meet
    it, lit strips down its sides and a lit badge on its roof, in the module's own colour."""
    k.recolour(glow=colour)
    k.box(-6.4, 6.4, -5.0, 5.0, 0.0, 0.6, "mid")
    plate(-5.6, 5.6, -4.3, 4.3, 0.6, 4.0, "steel", inset=0.5)
    for a in (-6.2, 5.2):
        plate(a, a + 1.0, -3.0, 3.0, 0.6, 4.3, "plate", inset=0.2)
        k.box(a + 0.2, a + 0.8, -3.05, 3.05, 3.0, 3.4, "paint")
    for s_ in (1, -1):
        k.box(-4.5, 4.5, *sorted((s_ * 4.25, s_ * 4.32)), 1.6, 2.1, "glow")
    return 4.0


def module_speed():
    """Speed: two turbine housings on its roof with fans turning in them, swept fins either side. Cyan."""
    k.begin(0.0)
    top = module_core((70, 225, 255))
    for a in (-2.3, 2.3):
        k.drum(a, 0, 2.0, top, top + 0.9, "plate", 16)
        k.drum(a, 0, 1.7, top + 0.9, top + 0.95, "black", 16)
        with part("spin_fan%d" % (a > 0), (a, 0, top + 1.0)):
            for i in range(6):
                t = i * math.tau / 6
                k.solid([(a, 0, top + 1.0), (a + math.cos(t) * 1.5, math.sin(t) * 1.5, top + 1.0), (a + math.cos(t + 0.45) * 1.5, math.sin(t + 0.45) * 1.5, top + 1.0)],
                        [(a, 0, top + 1.2), (a + math.cos(t) * 1.5, math.sin(t) * 1.5, top + 1.2), (a + math.cos(t + 0.45) * 1.5, math.sin(t + 0.45) * 1.5, top + 1.2)], "mid")
            k.drum(a, 0, 0.35, top + 0.9, top + 1.35, "glow", 8)
    for s_ in (1, -1):
        k.solid([(-4.0, s_ * 4.3, top - 1.2), (2.0, s_ * 4.3, top - 1.2), (-2.0, s_ * 6.2, top - 1.2)],
                [(-4.0, s_ * 4.3, top - 0.9), (2.0, s_ * 4.3, top - 0.9), (-2.0, s_ * 6.2, top - 0.9)], "plate")
    finish_toon("module_speed", 18.0)


def module_armour():
    """Armour: heavy plates stacked on its roof, each smaller, riveted, a big chevron on the front plate. Pale blue."""
    k.begin(0.0)
    top = module_core((120, 190, 255))
    plate(-4.6, 4.6, -3.6, 3.6, top, top + 1.0, "plate", inset=0.5)
    plate(-3.6, 3.6, -2.8, 2.8, top + 1.0, top + 1.9, "steel", inset=0.5)
    plate(-2.6, 2.6, -2.0, 2.0, top + 1.9, top + 2.6, "plate", inset=0.4)
    for s_ in (1, -1):
        for a in (-3.8, -1.3, 1.3, 3.8):
            k.drum(a, s_ * 3.0, 0.2, top + 0.9, top + 1.1, "dark", 6)
        k.solid([(-1.2, 0, top + 2.6), (1.2, 0, top + 2.6), (0.2, s_ * 1.4, top + 2.6), (-0.6, s_ * 1.4, top + 2.6)],
                [(-1.2, 0, top + 2.68), (1.2, 0, top + 2.68), (0.2, s_ * 1.4, top + 2.68), (-0.6, s_ * 1.4, top + 2.68)], "glow")
    finish_toon("module_armour", 18.0)


def module_attack():
    """Attack: a gun turret on its roof with two long barrels pointing out along the line, red sights. Red."""
    k.begin(0.0)
    top = module_core((255, 70, 50))
    hexagon(0, 0, 2.6, top, top + 1.0, "plate", r1=2.3, sides=8, turn=math.pi / 8)
    plate(-1.8, 1.8, -1.6, 1.6, top + 1.0, top + 2.4, "steel", inset=0.3, front=0.6)
    for x in (-0.65, 0.65):
        k.tube(1.6, 6.0, x, top + 1.7, 0.28, "dark", 10)
        k.tube(5.6, 6.2, x, top + 1.7, 0.36, "dark", 10)
        k.tube(6.2, 6.3, x, top + 1.7, 0.2, "glow", 10)
    k.box(1.2, 1.3, -1.1, 1.1, top + 1.9, top + 2.15, "glow")
    finish_toon("module_attack", 18.0)


def module_build():
    """Build speed: an assembly arm on its roof that turns, a gear at its foot, a lamp on the arm's hand. Yellow."""
    k.begin(0.0)
    top = module_core((255, 214, 64))
    k.drum(0, 0, 2.6, top, top + 0.5, "dark", 12)
    for i in range(12):
        t = i * math.tau / 12
        k.box(math.cos(t) * 2.7 - 0.25, math.cos(t) * 2.7 + 0.25, math.sin(t) * 2.7 - 0.25, math.sin(t) * 2.7 + 0.25, top, top + 0.5, "dark")
    with part("spin_arm", (0, 0, top + 0.5)):
        k.drum(0, 0, 1.2, top + 0.5, top + 1.6, "plate", 10)
        tube_between((0, 0, top + 1.4), (2.6, 0, top + 3.8), 0.4, "steel", 8)
        tube_between((2.6, 0, top + 3.8), (4.4, 0, top + 2.4), 0.3, "steel", 8)
        k.drum(2.6, 0, 0.5, top + 3.4, top + 4.2, "mid", 8)
        k.drum(4.4, 0, 0.45, top + 1.9, top + 2.5, "glow", 8)
    finish_toon("module_build", 18.0)


def module_energy():
    """Energy: three coil pylons in a row on its roof, rings round them, glowing tips. Lime."""
    k.begin(0.0)
    top = module_core((190, 255, 60))
    for a in (-3.2, 0.0, 3.2):
        k.drum(a, 0, 0.9, top, top + 0.6, "plate", 10)
        k.drum(a, 0, 0.35, top + 0.6, top + 4.2, "dark", 8)
        for z in (top + 1.2, top + 2.1, top + 3.0):
            k.drum(a, 0, 0.75, z, z + 0.25, "mid", 12)
        k.drum(a, 0, 0.55, top + 4.2, top + 4.9, "glow", 10)
    for x in (-2.4, 2.4):
        tube_between((-4.5, x, top + 0.3), (4.5, x, top + 0.3), 0.3, "glow", 6)
    finish_toon("module_energy", 18.0)


def module_twin():
    """Twin feed: two hoppers side by side on its roof feeding one split chute, a split arrow on its deck. Magenta."""
    k.begin(0.0)
    top = module_core((255, 70, 220))
    for x in (-1.9, 1.9):
        k.drum(-1.0, x, 1.7, top + 1.0, top + 3.2, "plate", 8, r1=2.0)
        k.drum(-1.0, x, 1.0, top, top + 1.0, "dark", 8, r1=1.7)
        k.drum(-1.0, x, 2.0, top + 3.2, top + 3.35, "glow", 8)
        k.drum(-1.0, x, 1.6, top + 3.2, top + 3.4, "black", 8)
    plate(1.4, 4.6, -1.2, 1.2, top, top + 0.8, "mid", inset=0.2)
    for s_ in (1, -1):
        k.solid([(2.2, 0, top + 0.8), (2.8, 0, top + 0.8), (4.4, s_ * 1.1, top + 0.8), (3.8, s_ * 1.1, top + 0.8)],
                [(2.2, 0, top + 0.88), (2.8, 0, top + 0.88), (4.4, s_ * 1.1, top + 0.88), (3.8, s_ * 1.1, top + 0.88)], "glow")
    finish_toon("module_twin", 18.0)


def module_napalm():
    """Napalm: two fat fuel tanks lying along its roof with hazard bands, a burner nozzle at the front with a flame
    lamp. Orange."""
    k.begin(0.0)
    top = module_core((255, 120, 30))
    for x in (-1.8, 1.8):
        hlathe(x, top + 1.4, [(0.6, -4.6), (1.3, -4.2), (1.4, -3.6), (1.4, 3.0), (1.3, 3.6), (0.6, 4.0)], "plate", 20)
        for a in (-2.0, 1.4):
            hlathe(x, top + 1.4, [(1.45, a), (1.45, a + 0.5)], "paint", 20)
        k.box(-3.0, 2.5, x - 0.2, x + 0.2, top, top + 0.4, "dark")
    k.tube(4.0, 5.4, 0, top + 1.0, 0.5, "dark", 10)
    k.tube(5.4, 5.6, 0, top + 1.0, 0.45, "glow", 10)
    finish_toon("module_napalm", 18.0)


def module_shield():
    """Shield coil: a domed emitter on its roof under a ring that turns, lit nodes round the ring. Teal."""
    k.begin(0.0)
    top = module_core((60, 255, 200))
    k.drum(0, 0, 2.6, top, top + 0.7, "plate", 20)
    k.drum(0, 0, 2.2, top + 0.7, top + 1.6, "steel", 20, r1=1.8)
    k.drum(0, 0, 1.8, top + 1.6, top + 2.4, "glow", 20, r1=0.9)
    k.drum(0, 0, 0.9, top + 2.4, top + 2.7, "white", 16, r1=0.4)
    with part("spin_ring", (0, 0, top + 1.3)):
        k.drum(0, 0, 3.6, top + 1.1, top + 1.5, "dark", 24, r1=3.6)
        for i in range(4):
            t = i * math.tau / 4
            k.drum(math.cos(t) * 3.6, math.sin(t) * 3.6, 0.45, top + 1.0, top + 1.7, "glow", 8)
    finish_toon("module_shield", 18.0)


def module_magnet():
    """Salvage magnet: a big horseshoe magnet raised on a pivot that turns slowly, lit poles. Purple."""
    k.begin(0.0)
    top = module_core((180, 90, 255))
    k.drum(0, 0, 1.6, top, top + 1.0, "plate", 12)
    with part("spin_magnet", (0, 0, top + 1.0)):
        k.drum(0, 0, 0.6, top + 1.0, top + 2.4, "dark", 8)
        pts = [(math.cos(t) * 2.4, 0, top + 4.2 + math.sin(t) * 2.4) for t in [math.pi + i * math.pi / 8 for i in range(9)]]
        for p0, p1 in zip(pts, pts[1:]):
            tube_between(p0, p1, 0.7, "paint", 10)
        for x in (-2.4, 2.4):
            tube_between((x, 0, top + 4.2), (x, 0, top + 5.6), 0.72, "steel", 10)
            tube_between((x, 0, top + 5.6), (x, 0, top + 6.2), 0.74, "glow", 10)
        tube_between((0, 0, top + 2.3), (0, 0, top + 1.8), 0.6, "mid", 8)
    finish_toon("module_magnet", 18.0)


def module_blank():
    """Blank: nothing but line, a low plain junction box (no lit feature: it does nothing) with a team stripe and a
    hatch on its roof; it only carries the line on toward a resource."""
    k.begin(0.0)
    k.recolour(glow=(150, 156, 166))   # (grey: not a light)
    k.box(-6.0, 6.0, -4.0, 4.0, 0.0, 0.5, "mid")
    plate(-5.2, 5.2, -3.4, 3.4, 0.5, 3.2, "steel", inset=0.6)
    for a in (-6.0, 5.0):
        plate(a, a + 1.0, -2.9, 2.9, 0.5, 3.8, "plate", inset=0.2)
    k.box(-3.5, 3.5, -0.4, 0.4, 3.2, 3.3, "paint")
    k.box(-1.6, 1.6, 1.0, 2.2, 3.2, 3.35, "dark")
    finish_toon("module_blank", 16.0)


globals()[sys.argv[sys.argv.index("--") + 1]]()
