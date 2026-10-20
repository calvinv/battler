"""Pieces Battler's models share (tools/art/model_units.py, model_buildings.py): the palette and the parts, built from
ai-tools' kit (C:/dev/ai-tools/blender/ship_kit.py). Importing this sets up Blender and the palette.
`a` along a model (front +), `x` across, `z` up from the ground; "paint" is the team colour.

The look (the user, 2026-10-20: "I also want a more modern tech look like supreme commander"): clean angular armour
in cool greys, plates with sloped sides and chamfered edges, enclosed track pods rather than open wheels, team colour
in broad stripes and panels on the tops (what the camera sees), and lit strips and lenses in "glow" (the game makes
those shine: game/team_paint.gdshader). No rust, rivets or smoke stacks."""
import math, os, sys
sys.path.insert(0, "C:/dev/ai-tools/blender")
os.environ.setdefault("ART_PROJECT", "C:/dev/battler")
import ship_kit as k
from mathutils import Vector

UNITS = k.PROJECT + "/art/units"
os.makedirs(UNITS, exist_ok=True)

# Cool clean greys by job: steel = the main armour (light), plate = secondary panels, mid = machinery, dark = recesses
# and joints, black = treads and vents; glow = lit strips and lenses (cyan); hazard = deck markings; paint = team.
k.recolour(steel=(176, 181, 188), plate=(142, 148, 156), mid=(108, 114, 124), dark=(62, 67, 76), black=(26, 28, 33),
           glow=(110, 225, 255), green=(90, 235, 130), hazard=(224, 186, 70), white=(226, 230, 236),
           paint=(70, 110, 190), paint_dark=(50, 80, 150))


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


def plate(a0, a1, x0, x1, z0, z1, mat, inset=0.6, front=None, back=None):
    """An armour block with sloped sides: its top is `inset` in from its foot all round (`front` and `back` override
    the inset at the ends, for a raked glacis or tail). The basic shape of the look."""
    f = inset if front is None else front
    b = inset if back is None else back
    low = [(a0, x0, z0), (a1, x0, z0), (a1, x1, z0), (a0, x1, z0)]
    high = [(a0 + b, x0 + inset, z1), (a1 - f, x0 + inset, z1), (a1 - f, x1 - inset, z1), (a0 + b, x1 - inset, z1)]
    return k.solid(low, high, mat)


def hexagon(a, x, r, z0, z1, mat, r1=None, turn=0.0, sides=6):
    """A flat-sided plate seen from above (six sides unless told), from z0 to z1, tapering to r1 at the top."""
    r1 = r if r1 is None else r1
    ring = lambda rr, z: [(a + math.cos(turn + i * math.tau / sides) * rr, x + math.sin(turn + i * math.tau / sides) * rr, z)
                          for i in range(sides)]
    return k.solid(ring(r, z0), ring(r1, z1), mat)


def pod(side, inner, outer, a0, a1, h, lights=True):
    """An enclosed track pod on `side` (+1 or -1) from x=inner to outer: an armoured housing with raked ends over a
    dark tread band, a lit strip along its outer face."""
    x0, x1 = sorted((side * inner, side * outer))
    xo = side * outer
    k.upright([(a0 + 0.9, 0.0), (a1 - 0.9, 0.0), (a1, h * 0.45), (a1 - 0.6, h * 0.55), (a0 + 0.6, h * 0.55), (a0, h * 0.45)],
              x0 + 0.1, x1 - 0.1, "black")   # the tread
    plate(a0 + 0.3, a1 - 0.2, x0, x1, h * 0.4, h, "plate", inset=0.25, front=1.0, back=0.8)                   # housing
    if lights:
        k.box(a0 + 1.6, a1 - 1.8, min(xo, xo + side * 0.08), max(xo, xo + side * 0.08), h * 0.62, h * 0.72, "glow")


def strip(a0, a1, x0, x1, z, mat="glow", t=0.12):
    """A thin lit strip (or a seam, in "dark") lying on a surface at height z."""
    k.box(a0, a1, x0, x1, z, z + t, mat)


def lens(a, x, z, r, facing=1):
    """A sensor lens looking along `a`: a dark rim and a glowing face (the machines have no crews)."""
    k.tube(a - facing * 0.5, a, x, z, r * 1.3, "dark", 8)
    k.tube(a, a + facing * 0.1, x, z, r, "glow", 8)


def vents(a0, a1, x0, x1, z, count):
    """A grille of dark slots across a deck."""
    step = (a1 - a0) / count
    for i in range(count):
        k.box(a0 + i * step + step * 0.2, a0 + (i + 1) * step - step * 0.2, x0, x1, z, z + 0.1, "black")


def apron(half_a, half_x, h=0.5, lines=True):
    """A building's foundation: a raised slab with chamfered edges and lit corner markers; hazard lines at the front."""
    plate(-half_a, half_a, -half_x, half_x, 0.0, h, "mid", inset=0.4)
    if lines:
        for s in (1, -1):
            for c in (1, -1):
                k.box(c * half_a - (0.9 if c > 0 else -0.1) - 0.4, c * half_a - (0.9 if c > 0 else -0.1) + 0.4,
                      s * (half_x - 1.2) - 0.4, s * (half_x - 1.2) + 0.4, h, h + 0.08, "glow")


# ---- detail (the user, 2026-10-20: "can we make more detailed models of the units and buildings") ------------------

def finish_detailed(name, span, folder=None, bevel=0.09, angle=35.0):
    """k.finish, but first every piece's sharp edges are bevelled: a narrow chamfer on each edge sharper than `angle`
    degrees, so edges catch the light instead of every box meeting at a knife line. Thin pieces are left alone where
    the chamfer would eat them (clamped). Prints the triangle count, to keep an eye on the cost."""
    import bmesh
    for ob in k.pieces:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        sharp = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0.0) > math.radians(angle)]
        if sharp:
            bmesh.ops.bevel(bm, geom=sharp, offset=bevel, offset_type="OFFSET", segments=1, affect="EDGES",
                            clamp_overlap=True, profile=0.5)
        bm.to_mesh(ob.data)
        bm.free()
    tris = sum(len(p.vertices) - 2 for ob in k.pieces for p in ob.data.polygons)
    print("%s: %d triangles" % (name, tris))
    k.finish(name, span, folder)


def seam(a0, a1, x0, x1, z, t=0.06):
    """A dark panel line lying on a surface at height z (a slot between armour plates)."""
    k.box(a0, a1, x0, x1, z - 0.02, z + t, "black")


def hatch(a, x, z, r, mat="plate"):
    """A round hatch on a deck: a ring, a lid, a handle."""
    k.drum(a, x, r, z, z + 0.12, "dark", 10)
    k.drum(a, x, r * 0.85, z + 0.12, z + 0.22, mat, 10)
    k.box(a - r * 0.4, a + r * 0.4, x - 0.05, x + 0.05, z + 0.22, z + 0.32, "dark")


def stowage(a0, a1, x0, x1, z0, z1, mat="plate"):
    """A stowage box: a lid seam round its top and two clasps on its outer face."""
    plate(a0, a1, x0, x1, z0, z1, mat, inset=0.08)
    seam(a0 + 0.1, a1 - 0.1, x0 + 0.1, x1 - 0.1, z1 - 0.25, 0.04)
    for a in (a0 + (a1 - a0) * 0.3, a0 + (a1 - a0) * 0.7):
        k.box(a - 0.08, a + 0.08, x0 - 0.04, x1 + 0.04, z1 - 0.45, z1 - 0.2, "dark")


def bolts(a0, a1, x, z, count, side=1, size=0.07):
    """A row of bolt heads on an upright face at `x`, standing out to `side` (+1 or -1)."""
    for i in range(count):
        a = a0 + (a1 - a0) * (i + 0.5) / count
        x0, x1 = sorted((x, x + side * size))
        k.box(a - size, a + size, x0, x1, z - size, z + size, "dark")


def road_wheel(a, x, z, r, side):
    """A road wheel facing out to `side`: a tyre, a dished hub, a cap."""
    from mathutils import Vector
    xo = x + side * 0.0
    tube_between((a, xo, z), (a, xo + side * 0.45, z), r, "black", 12)
    tube_between((a, xo + side * 0.45, z), (a, xo + side * 0.5, z), r * 0.75, "mid", 10)
    tube_between((a, xo + side * 0.5, z), (a, xo + side * 0.62, z), r * 0.3, "dark", 8)


def pod_detail(side, inner, outer, a0, a1, h):
    """Detail on a track pod (see pod): a drive sprocket at the front and an idler at the back showing below the
    housing, road wheels between them, a bolt row along the housing, and seams down its skirt plates."""
    xo = side * outer
    x_in = side * (inner + 0.2)
    for a, r, mat in ((a1 - 0.9, h * 0.26, "mid"), (a0 + 0.9, h * 0.24, "mid")):   # sprocket and idler
        tube_between((a, x_in, h * 0.32), (a, xo + side * 0.05, h * 0.32), r, mat, 10)
        tube_between((a, xo + side * 0.05, h * 0.32), (a, xo + side * 0.15, h * 0.32), r * 0.4, "dark", 8)
    n = max(3, int((a1 - a0 - 2.6) / 1.5))
    for i in range(n):                                                                  # road wheels, low down
        a = a0 + 1.7 + i * (a1 - a0 - 3.4) / max(n - 1, 1)
        tube_between((a, xo - side * 0.3, h * 0.24), (a, xo + side * 0.02, h * 0.24), h * 0.2, "dark", 8)
    bolts(a0 + 1.0, a1 - 1.0, xo, h * 0.88, int(a1 - a0), side=side, size=0.06)
    for i in range(1, 4):                                                               # skirt plate seams
        a = a0 + (a1 - a0) * i / 4
        x0, x1 = sorted((xo, xo + side * 0.05))
        k.box(a - 0.04, a + 0.04, x0, x1, h * 0.45, h * 0.98, "black")
