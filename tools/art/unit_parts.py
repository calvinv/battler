"""Pieces Battler's models share (tools/art/model_units.py, model_buildings.py): the gritty dieselpunk palette and the
parts built from ai-tools' kit (C:/dev/ai-tools/blender/ship_kit.py). Importing this sets up Blender and the palette.
`a` along a model (front +), `x` across, `z` up from the ground; "paint" is the team colour."""
import math, os, sys
sys.path.insert(0, "C:/dev/ai-tools/blender")
os.environ.setdefault("ART_PROJECT", "C:/dev/battler")
import ship_kit as k
from mathutils import Vector

UNITS = k.PROJECT + "/art/units"
os.makedirs(UNITS, exist_ok=True)

os.makedirs(UNITS, exist_ok=True)

# Worn olive drab and grey steel (ship_kit's greys renamed by job): steel = olive, plate = darker olive panels,
# mid = bare grey steel, dark = gunmetal, black = tracks and shadow, rust, and the team colour in "paint".
k.recolour(steel=(104, 108, 74), plate=(86, 90, 62), mid=(112, 110, 102), dark=(62, 62, 58), black=(30, 28, 26),
           rust=(124, 70, 40), ember=(255, 176, 64), paint=(70, 110, 190), paint_dark=(50, 80, 150))


# ---- shared pieces ------------------------------------------------------------------------------------------------

def tube_between(p0, p1, r, mat, sides=8, r1=None):
    """A round bar from p0 to p1 ((a, x, z) each) in any direction: a raised gun barrel, a strut."""
    r1 = r if r1 is None else r1
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0).normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    u = d.cross(up).normalized()
    v = d.cross(u)
    ring = lambda c, rr: [tuple(c + (u * math.cos(i * math.tau / sides) + v * math.sin(i * math.tau / sides)) * rr)
                          for i in range(sides)]
    return k.solid(ring(p0, r), ring(p1, r1), mat)


def wheel(a, z, r, x0, x1, mat, sides=10):
    """A wheel standing on its edge, its axle across: between x0 and x1."""
    pts = lambda x: [(a + math.cos(i * math.tau / sides) * r, x, z + math.sin(i * math.tau / sides) * r) for i in range(sides)]
    return k.solid(pts(x0), pts(x1), mat)


def track(side, inner, outer, a0, a1, h, wheels, skirt=True):
    """One track unit on `side` (+1 or -1) from x=inner to outer: the belt seen from the side (rounded ends), tread
    bars on top, road wheels and a drive sprocket on its outer face, and a mudguard skirt over it."""
    x0, x1 = sorted((side * inner, side * outer))
    xo = side * outer
    belt = [(a0 + 1.0, 0.0), (a1 - 1.4, 0.0), (a1, h * 0.6), (a1 - 0.5, h), (a0 + 0.6, h), (a0, h * 0.5)]
    k.upright(belt, x0, x1, "black")
    for i in range(int((a1 - a0 - 1.2) / 0.7)):      # tread bars across the top run
        a = a0 + 0.8 + i * 0.7
        k.box(a, a + 0.28, x0 - 0.05, x1 + 0.05, h, h + 0.14, "dark")
    span = (a1 - a0 - 2.6) / max(wheels - 1, 1)
    for i in range(wheels):
        a = a0 + 1.3 + i * span
        wheel(a, h * 0.36, h * 0.3, xo, xo + side * 0.25, "mid")
        wheel(a, h * 0.36, h * 0.12, xo + side * 0.25, xo + side * 0.4, "dark", 6)
    wheel(a1 - 0.9, h * 0.62, h * 0.3, xo, xo + side * 0.3, "dark", 8)      # the drive sprocket, at the front
    if skirt:
        k.box(a0 + 0.4, a1 - 0.6, min(xo, xo + side * 0.3), max(xo, xo + side * 0.3), h * 0.62, h + 0.25, "plate")
        for i in range(4):      # rust where the skirt plates meet
            a = a0 + 1.5 + i * (a1 - a0 - 3.0) / 3
            k.box(a - 0.15, a + 0.15, min(xo, xo + side * 0.36), max(xo, xo + side * 0.36), h * 0.62, h + 0.25, "rust")


def eye(a, x, z, r, facing=1):
    """A sensor eye: a dark housing and a glowing lens looking along `a` (the machines have no crews)."""
    k.tube(a - facing * 0.6, a, x, z, r * 1.35, "black", 8)
    k.tube(a, a + facing * 0.12, x, z, r, "ember", 8)


def stack(a, x, z0, z1, r=0.42):
    """An exhaust stack standing up, with a sooty lip."""
    k.drum(a, x, r, z0, z1, "dark", 8)
    k.drum(a, x, r * 1.25, z1, z1 + 0.35, "black", 8)


def rivets(a0, a1, x, z, count):
    """A row of rivet heads along a flank at `x` (its sign says which side they stand out on)."""
    side = 1 if x > 0 else -1
    for i in range(count):
        a = a0 + (a1 - a0) * (i + 0.5) / count
        k.box(a - 0.12, a + 0.12, min(x, x + side * 0.12), max(x, x + side * 0.12), z - 0.12, z + 0.12, "dark")


# ---- the units ----------------------------------------------------------------------------------------------------

