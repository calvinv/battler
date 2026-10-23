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

RIG = []       # moving parts: (name, pivot (a, x, z), parent name or None, first piece, end piece)
MARKS = []     # points the game wants: (name, (a, x, z), parent name or None)


class part:
    """`with part("wheel_0", (a, x, z)):` the pieces made inside move as one in the game, turning about the pivot.
    `parent`: the part it rides on (a barrel on its turret). Everything outside any part is the "body". The baked
    model stays one mesh for painting; finish_realistic then also writes <name>_rig.glb split into these parts."""

    def __init__(self, name, pivot, parent=None):
        self.name, self.pivot, self.parent = name, pivot, parent

    def __enter__(self):
        self.start = len(k.pieces)
        return self

    def __exit__(self, *exc):
        RIG.append((self.name, self.pivot, self.parent, self.start, len(k.pieces)))


def mark(name, where, parent=None):
    """A named point on the model for the game (a muzzle, an exhaust): an empty in the rig, riding `parent`."""
    MARKS.append((name, where, parent))


def _tag_parts():
    """Every piece gets a face attribute "rig": 0 for the body, i + 1 for RIG[i] (kept through the join)."""
    owner = {}
    for i, (_, _, _, a, b) in enumerate(RIG):
        for ob in k.pieces[a:b]:
            owner.setdefault(ob.name, i + 1)      # (a part nested in another keeps its own pieces)
    for ob in k.pieces:
        at = ob.data.attributes.get("rig") or ob.data.attributes.new("rig", "INT", "FACE")
        v = owner.get(ob.name, 0)
        for d in at.data:
            d.value = v


def _export_rig(name, folder):
    """<name>_rig.glb: the finished model cut into its parts (same UVs, same texture), each with its origin at its
    pivot and parented as RIG says, plus the MARKS as empties. The game animates the parts by name."""
    import bpy, bmesh
    if not RIG and not MARKS:
        return
    ship = bpy.data.objects[name]
    made = {}
    names = ["body"] + [r[0] for r in RIG]
    pivots = [(0.0, 0.0, 0.0)] + [r[1] for r in RIG]
    parents = [None] + [None if r[2] == "root" else (r[2] or "body") for r in RIG]      # ("root": not on the body, e.g. wheels, which stay on the ground while it rocks)
    for g, pname in enumerate(names):
        me = ship.data.copy()
        bm = bmesh.new()
        bm.from_mesh(me)
        layer = bm.faces.layers.int.get("rig")
        bmesh.ops.delete(bm, geom=[fc for fc in bm.faces if fc[layer] != g], context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        p = k.at(*pivots[g])
        for v in bm.verts:
            v.co -= p
        bm.to_mesh(me)
        bm.free()
        me.materials.clear()
        ob = bpy.data.objects.new(pname, me)
        bpy.context.scene.collection.objects.link(ob)
        made[pname] = (ob, p)
    for g, pname in enumerate(names):
        ob, p = made[pname]
        if parents[g]:
            par, pp = made[parents[g]]
            ob.parent = par
            ob.location = p - pp
        else:
            ob.location = p
    for mname, where, parent in MARKS:
        e = bpy.data.objects.new(mname, None)
        bpy.context.scene.collection.objects.link(e)
        par, pp = made[parent or "body"]
        e.parent = par
        e.location = k.at(*where) - pp
        made[mname] = (e, k.at(*where))
    bpy.ops.object.select_all(action="DESELECT")
    for ob, _ in made.values():
        ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath="%s/%s_rig.glb" % (folder, name), use_selection=True, export_format="GLB",
                              export_materials="NONE", export_normals=True, export_texcoords=True, export_yup=True)
    print("%s: rig with %s" % (name, ", ".join(names[1:] + [m[0] for m in MARKS])))


def hlathe(x, z, profile, mat, sides=32):
    """A round piece lying along the length (a) at (x, z), turned from a profile ((radius, a) points, back first) and
    shaded smooth: a drum, a domed nose, a barrel that's really round."""
    import bmesh, bpy
    bm = bmesh.new()
    rings = [[bm.verts.new(k.at(a, x + math.cos(i * math.tau / sides) * max(r, 0.02), z + math.sin(i * math.tau / sides) * max(r, 0.02)))
              for i in range(sides)] for r, a in profile]
    for i in range(len(rings) - 1):
        for j in range(sides):
            bm.faces.new([rings[i][j], rings[i + 1][j], rings[i + 1][(j + 1) % sides], rings[i][(j + 1) % sides]]).smooth = True
    bm.faces.new(rings[0])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("piece")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("piece", me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(k.mats[mat])
    k.pieces.append(ob)
    return ob


def wlathe(a, z, profile, mat, sides=32, caps=True):
    """A round piece across the width (x) at (a, z), turned from a profile ((radius, x) points): a wheel, a hub, an
    axle cap, shaded smooth. caps=False: the profile is a closed loop (first point again at its end) and no end
    discs are made: a ring, a tyre you can see the spokes through."""
    import bmesh, bpy
    bm = bmesh.new()
    rings = [[bm.verts.new(k.at(a + math.cos(i * math.tau / sides) * max(r, 0.02), x, z + math.sin(i * math.tau / sides) * max(r, 0.02)))
              for i in range(sides)] for r, x in profile]
    for i in range(len(rings) - 1):
        for j in range(sides):
            bm.faces.new([rings[i][j], rings[i + 1][j], rings[i + 1][(j + 1) % sides], rings[i][(j + 1) % sides]]).smooth = True
    if caps:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("piece")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("piece", me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(k.mats[mat])
    k.pieces.append(ob)
    return ob


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
    # The baked shading only looks a short way round each spot: creases and corners darken, but a gun barrel a
    # metre above the hull no longer lays a long dark stripe on it.
    import bpy
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new("bake_world")
    bpy.context.scene.world.light_settings.distance = 0.9
    _tag_parts()
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


# ---- texture detail (the user, 2026-10-20: "the units should be basic shapes but have detailed textures, boxy is
# fine"; "games like mechabellum and supreme commander is the look we're after, modern futuristic but grounded") ----

# Tiny stencil digits, 3 wide x 5 high, for the serial numbers on the flanks.
_DIGITS = {
    "0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111",
    "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001001010010",
    "8": "111101111101111", "9": "111101111001111", "-": "000000111000000",
}


def _hash(*v):
    """A repeatable 0..1 from whole numbers."""
    h = 2166136261
    for x in v:
        h = ((h ^ (int(x) & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 1274126177) & 0xFFFFFFFF
    return (h ^ (h >> 16)) / 4294967295.0


def finish_textured(name, span, serial="07", bevel=0.09):
    """Build the model with softened edges (finish_detailed), then paint detail into its texture: every face gets a
    grid of armour panels (dark seams, a rivet at each corner, each panel a slightly different shade), the lower hull
    grime, the whole a little weathered; warning chevrons on a few top panels; a stencilled serial number on each flank.
    Team paint keeps its mask (the seams show on it through its shade); lit strips are left alone.
    How: bake where on the model each texture pixel lies (its position and facing), then draw in numpy."""
    import numpy as np
    import bpy
    finish_detailed(name, span, UNITS, bevel=bevel)
    ship = bpy.data.objects[name]
    path = "%s/%s.png" % (UNITS, name)
    skin = bpy.data.images.load(path, check_existing=False)
    size = skin.size[0]   # (bake the probes at the texture's own size)
    sc = bpy.context.scene

    def bake_vector(socket):
        m = bpy.data.materials.new("probe_" + socket)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        fit = nt.nodes.new("ShaderNodeVectorMath")
        fit.operation = "MULTIPLY_ADD"     # into 0..1, so nothing's lost: positions (+-50) and facings (+-1)
        scale, offset = (0.01, 0.5) if socket == "Position" else (0.5, 0.5)
        fit.inputs[1].default_value = (scale, scale, scale)
        fit.inputs[2].default_value = (offset, offset, offset)
        emit = nt.nodes.new("ShaderNodeEmission")
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(geo.outputs[socket], fit.inputs[0])
        nt.links.new(fit.outputs[0], emit.inputs["Color"])
        nt.links.new(emit.outputs[0], out.inputs["Surface"])
        img = bpy.data.images.new("probe_" + socket, size, size, alpha=False, float_buffer=True)
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.nodes.active = tex
        ship.data.materials.clear()
        ship.data.materials.append(m)
        sc.cycles.samples = 1
        bpy.ops.object.select_all(action="DESELECT")
        ship.select_set(True)
        bpy.context.view_layer.objects.active = ship
        bpy.ops.object.bake(type="EMIT")
        a = np.array(img.pixels[:]).reshape(size, size, 4)[:, :, :3]
        return (a - offset) / scale

    pos = bake_vector("Position")
    nrm = bake_vector("Normal")
    px = np.array(skin.pixels[:]).reshape(size, size, 4)
    rgb, alpha = px[:, :, :3].copy(), px[:, :, 3]
    covered = np.linalg.norm(nrm, axis=2) > 0.5                       # texels on the model (not the gaps between)
    bright = rgb.max(axis=2)
    lit = (bright > 0.55) & ((rgb[:, :, 1] > rgb[:, :, 0] * 1.6) | (rgb[:, :, 2] > rgb[:, :, 0] * 1.6)) & (alpha > 0.5)
    work = covered & ~lit

    # Which way each texel faces, and its two coordinates across that face (a box projection).
    ax = np.abs(nrm).argmax(axis=2)
    u = np.where(ax == 0, pos[:, :, 1], pos[:, :, 0])
    v = np.where(ax == 2, pos[:, :, 1], pos[:, :, 2])
    # Panels: 2.4 long by 1.6 high on flanks, 2.0 square on decks, alternate rows staggered like brickwork.
    pu = np.where(ax == 2, 2.0, 2.4)
    pv = np.where(ax == 2, 2.0, 1.6)
    row = np.floor(v / pv)
    uu = u / pu + np.where(row % 2 == 0, 0.0, 0.5)
    col = np.floor(uu)
    fu, fv = uu - col, v / pv - row
    seam_w = 0.045
    seam = (np.minimum(fu, 1 - fu) * pu < seam_w) | (np.minimum(fv, 1 - fv) * pv < seam_w)
    # A rivet near each panel's corners.
    du = np.minimum(fu, 1 - fu) * pu
    dv = np.minimum(fv, 1 - fv) * pv
    rivet = (np.hypot(du - 0.16, dv - 0.16) < 0.055)
    # Each panel a little lighter or darker than its neighbours.
    vh = np.vectorize(lambda a, b, c: _hash(a, b, c) - 0.5)
    shade = 1.0 + 0.07 * vh(col.astype(np.int64), row.astype(np.int64), ax.astype(np.int64))
    # Grime: darker toward the ground and in soft blotches; a little lighter near the tops of things.
    z = pos[:, :, 2]
    grime = 0.78 + 0.22 * np.clip(z / 3.0, 0, 1)
    blot = np.sin(pos[:, :, 0] * 1.7 + np.sin(pos[:, :, 1] * 2.3)) * np.sin(pos[:, :, 1] * 1.3 + pos[:, :, 2] * 2.1)
    grime *= 0.95 + 0.05 * blot
    f = shade * grime
    f = np.where(seam, 0.55, f)
    f = np.where(rivet & ~seam, f * 0.72, f)
    out = rgb.copy()
    out[work] *= f[work][:, None]

    # Warning chevrons on a few top panels (not on team paint): yellow and black diagonal stripes in a band.
    top = (ax == 2) & (nrm[:, :, 2] > 0) & (alpha > 0.5) & work & ~seam
    pick = vh(col.astype(np.int64), row.astype(np.int64), np.full(col.shape, 99)) > 0.38
    band = (fv > 0.12) & (fv < 0.32) & (fu > 0.15) & (fu < 0.85)
    stripes = ((u + v) * 2.6 % 1.0) < 0.5
    chev = top & pick & band
    out[chev & stripes] = (0.62, 0.48, 0.08)
    out[chev & ~stripes] = (0.05, 0.05, 0.05)

    # A stencilled serial on each flank: 3x5 pixel digits, 0.12 units a pixel, low on the side near the middle.
    flank = (ax == 0) & work & (alpha > 0.5)
    cell = 0.12
    text = serial
    width = len(text) * 4 * cell
    gx = (pos[:, :, 1] * np.sign(nrm[:, :, 0]) + width / 2) / cell   # (read the right way round on either side)
    gz = (pos[:, :, 2] - 1.3) / cell
    inside = flank & (gx >= 0) & (gx < len(text) * 4) & (gz >= 0) & (gz < 5)
    ch = np.clip((gx // 4).astype(int), 0, len(text) - 1)
    cx = (gx % 4).astype(int)
    cz = 4 - np.clip(gz.astype(int), 0, 4)
    on = np.zeros(gx.shape, bool)
    for i, c in enumerate(text):
        bits = _DIGITS.get(c, "0" * 15)
        lut = np.array([b == "1" for b in bits]).reshape(5, 3)
        sel = inside & (ch == i) & (cx < 3)
        on[sel] = lut[cz[sel], cx[sel]]
    out[on] = out[on] * 0.35

    px[:, :, :3] = np.clip(out, 0, 1)
    skin.pixels = px.ravel()
    skin.filepath_raw = path
    skin.file_format = "PNG"
    skin.save()
    print("%s: texture detail painted" % name)
    # The preview views again, with the painted texture on (finish rendered them before it was painted).
    look = bpy.data.materials.new("look_detail")
    look.use_nodes = True
    nt = look.node_tree
    nt.nodes.clear()
    pic = nt.nodes.new("ShaderNodeTexImage")
    shown = skin.copy()
    shown.alpha_mode = "NONE"
    pic.image = shown
    bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(pic.outputs["Color"], bsdf.inputs["Color"])
    nt.links.new(bsdf.outputs[0], outn.inputs["Surface"])
    ship.data.materials.clear()
    ship.data.materials.append(look)
    cam = bpy.data.objects["cam"]
    sc.cycles.samples = 24
    for label, place in (("quarter", (34, -40, 38)), ("close", (20, -24, 22))):
        place = tuple(c * span / 40.0 for c in place)
        cam.location = place
        cam.data.ortho_scale = span if label == "quarter" else span * 0.6
        cam.rotation_euler = (Vector((0, 0, 0)) - Vector(place)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = "%s/_%s_textured_%s.png" % (k.WORK, name, label)
        bpy.ops.render.render(write_still=True)


# ---- realistic weathering (the user, 2026-10-21: "Can you texture these models so they look realistic") ------------

def _vnoise3(p, scale, seed=0):
    """Smooth value noise in 3D, 0..1, at each of the points p (H x W x 3), `scale` features per unit."""
    import numpy as np
    q = p * scale + seed * 17.13
    i = np.floor(q)
    f = q - i
    f = f * f * (3 - 2 * f)

    def h(dx, dy, dz):
        v = (i[..., 0] + dx) * 127.1 + (i[..., 1] + dy) * 311.7 + (i[..., 2] + dz) * 74.7
        return (np.sin(v) * 43758.5453) % 1.0

    def lerp(a, b, t):
        return a + (b - a) * t

    x0 = lerp(lerp(h(0, 0, 0), h(1, 0, 0), f[..., 0]), lerp(h(0, 1, 0), h(1, 1, 0), f[..., 0]), f[..., 1])
    x1 = lerp(lerp(h(0, 0, 1), h(1, 0, 1), f[..., 0]), lerp(h(0, 1, 1), h(1, 1, 1), f[..., 0]), f[..., 1])
    return lerp(x0, x1, f[..., 2])


def _fbm3(p, scale, seed=0, octaves=4):
    s, a, total = 0.0, 0.5, 0.0
    for o in range(octaves):
        s = s + a * _vnoise3(p, scale * (2 ** o), seed + o)
        total += a
        a *= 0.5
    return s / total


def finish_realistic(name, span, style="coalition", serial="07", folder=None, bevel=0.06, grit=1.7, panels=True):
    """Build the model (softened edges, baked shading), then weather its texture so it looks like real worn
    machinery. Baked probes give each texel its place on the model, its facing, and how sharp a corner it is on
    (Cycles pointiness), and from those:
      both:      panel seams and rivets, each panel a touch different; large blotchy colour variation; grime toward
                 the ground; dust settled on the tops; dirt streaks running down the sides; edges worn to bright bare
                 metal (and the paint chipped there, so team colour wears off the corners); creases darkened.
      coalition: warning chevrons on a few deck panels and a stencilled serial on each flank.
      lattice:   rust patches (heavier low down), oil stains, rust streaks.
    Lit strips and eyes are left alone."""
    import numpy as np
    import bpy
    folder = folder or UNITS
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new("bake_world")
    bpy.context.scene.world.light_settings.distance = 0.9
    finish_detailed(name, span, folder, bevel=bevel)
    ship = bpy.data.objects[name]
    path = "%s/%s.png" % (folder, name)
    skin = bpy.data.images.load(path, check_existing=False)
    size = skin.size[0]
    sc = bpy.context.scene

    def bake_probe(socket, scale, offset):
        m = bpy.data.materials.new("probe_" + socket)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        fit = nt.nodes.new("ShaderNodeVectorMath")
        fit.operation = "MULTIPLY_ADD"
        fit.inputs[1].default_value = (scale, scale, scale)
        fit.inputs[2].default_value = (offset, offset, offset)
        emit = nt.nodes.new("ShaderNodeEmission")
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        src = geo.outputs[socket]
        if socket == "Pointiness":
            comb = nt.nodes.new("ShaderNodeCombineXYZ")
            nt.links.new(src, comb.inputs[0])
            src = comb.outputs[0]
        nt.links.new(src, fit.inputs[0])
        nt.links.new(fit.outputs[0], emit.inputs["Color"])
        nt.links.new(emit.outputs[0], out.inputs["Surface"])
        img = bpy.data.images.new("probe_" + socket, size, size, alpha=False, float_buffer=True)
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.nodes.active = tex
        ship.data.materials.clear()
        ship.data.materials.append(m)
        sc.cycles.samples = 1
        bpy.ops.object.select_all(action="DESELECT")
        ship.select_set(True)
        bpy.context.view_layer.objects.active = ship
        bpy.ops.object.bake(type="EMIT")
        a = np.array(img.pixels[:]).reshape(size, size, 4)[:, :, :3]
        return (a - offset) / scale

    pos = bake_probe("Position", 0.01, 0.5)
    nrm = bake_probe("Normal", 0.5, 0.5)
    sharp = bake_probe("Pointiness", 1.0, 0.0)[:, :, 0]       # 0.5 flat, more on outer corners, less in creases

    px = np.array(skin.pixels[:]).reshape(size, size, 4)
    rgb, alpha = px[:, :, :3].copy(), px[:, :, 3].copy()
    covered = np.linalg.norm(nrm, axis=2) > 0.5
    bright = rgb.max(axis=2)
    lit = (alpha > 0.5) & (((bright > 0.55) & ((rgb[:, :, 1] > rgb[:, :, 0] * 1.6) | (rgb[:, :, 2] > rgb[:, :, 0] * 1.6)))
                           | ((rgb[:, :, 0] > 0.95) & (rgb[:, :, 1] < 0.75) & (rgb[:, :, 2] < 0.3)))
    work = covered & ~lit
    painted = alpha < 0.5
    z = pos[:, :, 2]
    up = nrm[:, :, 2]
    lattice = style == "lattice"

    # Panels: a staggered grid on each face (box projection), seams and rivets, each panel its own shade.
    ax = np.abs(nrm).argmax(axis=2)
    u = np.where(ax == 0, pos[:, :, 1], pos[:, :, 0])
    v = np.where(ax == 2, pos[:, :, 1], pos[:, :, 2])
    grow = 1.3 if lattice else 1.0
    pu = np.where(ax == 2, 2.2, 2.6) * grow
    pv = np.where(ax == 2, 2.2, 1.7) * grow
    row = np.floor(v / pv)
    uu = u / pu + np.where(row % 2 == 0, 0.0, 0.5)
    col = np.floor(uu)
    fu, fv = uu - col, v / pv - row
    seam = (np.minimum(fu, 1 - fu) * pu < 0.04) | (np.minimum(fv, 1 - fv) * pv < 0.04)
    if not panels:      # (detail as geometry instead: painted hairline seams alias in game. The user, 2026-10-22.)
        seam = np.zeros_like(seam)
    du, dv = np.minimum(fu, 1 - fu) * pu, np.minimum(fv, 1 - fv) * pv
    rivet = (np.hypot(du - 0.14, dv - 0.14) < 0.045) & panels
    panel_shade = 1.0 if not panels else 1.0 + 0.06 * (((np.sin(col * 12.9898 + row * 78.233 + ax * 37.719) * 43758.5453) % 1.0) - 0.5)

    big = _fbm3(pos, 0.35, 1)
    mid = _fbm3(pos, 1.6, 2)
    fine = _vnoise3(pos, 9.0, 3)
    out = rgb * (panel_shade * (0.92 + 0.16 * big) * (0.95 + 0.1 * fine))[:, :, None]
    # Gritty (the user, 2026-10-22: "gritty look"): the whole surface a little dirtier and uneven, rust specks and
    # oil stains even on the Coalition.
    out *= (1.0 - 0.12 * (grit - 1.0) * (1.0 - mid))[:, :, None]
    speck = np.clip((_vnoise3(pos, 7.0, 8) - (0.93 - 0.03 * grit)) * 10.0, 0, 1) * np.clip((big - 0.5) * 4.0, 0, 1)   # rust in patches
    out = out * (1 - speck[:, :, None] * 0.7) + np.array([0.33, 0.17, 0.08]) * speck[:, :, None] * 0.7
    stain = np.clip((_fbm3(pos, 0.8, 9) - (0.7 - 0.05 * grit)) * 5, 0, 1) * 0.45
    out *= (1 - stain)[:, :, None]

    if lattice:
        rust_amt = np.clip((mid * 0.7 + big * 0.5 - 0.55) * 3.0 + np.clip(1.5 - z, 0, 1.5) * 0.25, 0, 1)
        rust = np.array([0.40, 0.20, 0.10]) * (0.75 + 0.5 * _vnoise3(pos, 5.0, 4))[:, :, None]
        out = out * (1 - rust_amt[:, :, None] * 0.85) + rust * rust_amt[:, :, None] * 0.85
        oil = np.clip((_fbm3(pos, 0.9, 5) - 0.62) * 5, 0, 1) * 0.6
        out *= (1 - oil)[:, :, None]

    grime = np.clip(1.0 - z / (2.2 * grit), 0, 1) * (0.5 + 0.5 * mid) * min(1.0, 0.65 * grit)
    dirt = np.array([0.24, 0.21, 0.17])
    out = out * (1 - grime[:, :, None] * 0.55) + dirt * grime[:, :, None] * 0.55
    dust = np.clip((up - 0.6) * 2.5, 0, 1) * np.clip((mid - 0.3) * 2.0, 0, 1) * 0.3 * grit
    out = out * (1 - dust[:, :, None]) + np.array([0.62, 0.57, 0.48]) * dust[:, :, None]
    side = np.abs(up) < 0.4
    across = np.where(np.abs(nrm[:, :, 0]) > np.abs(nrm[:, :, 1]), pos[:, :, 1], pos[:, :, 0])
    streak_n = _vnoise3(np.stack([across * 9.0, z * 0.6, across * 0.0], axis=2), 1.0, 6)
    streak = side * np.clip((streak_n - 0.62 + 0.06 * grit) * 3.0, 0, 1) * min(0.7, 0.28 * grit) * np.clip((mid - 0.35) * 3.0, 0, 1)
    streak_col = np.array([0.30, 0.16, 0.08]) if lattice else np.array([0.18, 0.17, 0.15])
    out = out * (1 - streak[:, :, None]) + streak_col * streak[:, :, None]

    crease = np.clip((0.5 - sharp) * 4.0, 0, 1) * min(0.7, 0.4 * grit)
    out *= (1 - crease)[:, :, None]
    wear = np.clip((sharp - 0.58) * 6.0, 0, 1) * np.clip((mid - 0.55 + 0.05 * grit) * 4.0, 0, 1) * 0.8   # (real corners only, in patches)
    metal = np.array([0.6, 0.6, 0.61]) * (0.95 + 0.1 * mid)[:, :, None]
    out = out * (1 - wear[:, :, None]) + metal * wear[:, :, None]
    chipped = painted & (wear > 0.5)
    alpha = np.where(chipped, 1.0, alpha)

    out = np.where(seam[:, :, None], out * 0.6, out)
    out = np.where((rivet & ~seam)[:, :, None], out * 0.75, out)

    if not lattice and panels:
        top = (ax == 2) & (up > 0) & ~painted & work & ~seam
        pick = ((np.sin(col * 3.1 + row * 7.7 + 99) * 43758.5453) % 1.0) > 0.7
        band = (fv > 0.15) & (fv < 0.33) & (fu > 0.2) & (fu < 0.8)
        stripes = ((u + v) * 2.6 % 1.0) < 0.5
        chev = top & pick & band
        out[chev & stripes] = np.array([0.62, 0.48, 0.08]) * (0.85 + 0.25 * fine[chev & stripes])[:, None]
        out[chev & ~stripes] = 0.06
        flank = (ax == 0) & work & ~painted
        cell = 0.12
        width = len(serial) * 4 * cell
        gx = (pos[:, :, 1] * np.sign(nrm[:, :, 0]) + width / 2) / cell
        gz = (pos[:, :, 2] - 1.2) / cell
        inside = flank & (gx >= 0) & (gx < len(serial) * 4) & (gz >= 0) & (gz < 5)
        ch = np.clip((gx // 4).astype(int), 0, len(serial) - 1)
        cx = (gx % 4).astype(int)
        cz = 4 - np.clip(gz.astype(int), 0, 4)
        on = np.zeros(gx.shape, bool)
        for i, c in enumerate(serial):
            lut = np.array([b == "1" for b in _DIGITS.get(c, "0" * 15)]).reshape(5, 3)
            sel = inside & (ch == i) & (cx < 3)
            on[sel] = lut[cz[sel], cx[sel]]
        out[on] = out[on] * 0.3

    px[:, :, :3] = np.where(work[:, :, None], np.clip(out, 0, 1), rgb)
    px[:, :, 3] = np.where(work, alpha, px[:, :, 3])
    skin.pixels = px.ravel()
    skin.filepath_raw = path
    skin.file_format = "PNG"
    skin.save()
    print("%s: weathered (%s)" % (name, style))
    look = bpy.data.materials.new("look_weathered")
    look.use_nodes = True
    nt = look.node_tree
    nt.nodes.clear()
    pic = nt.nodes.new("ShaderNodeTexImage")
    shown = skin.copy()
    shown.alpha_mode = "NONE"
    pic.image = shown
    bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
    outn = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(pic.outputs["Color"], bsdf.inputs["Color"])
    nt.links.new(bsdf.outputs[0], outn.inputs["Surface"])
    ship.data.materials.clear()
    ship.data.materials.append(look)
    cam = bpy.data.objects["cam"]
    sc.cycles.samples = 24
    for label, place, zoom in (("quarter", (34, -40, 38), 1.0), ("close", (20, -24, 22), 0.6)):
        place = tuple(c * span / 40.0 for c in place)
        cam.location = place
        cam.data.ortho_scale = span * zoom
        cam.rotation_euler = (Vector((0, 0, 0)) - Vector(place)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = "%s/_%s_real_%s.png" % (k.WORK, name, label)
        bpy.ops.render.render(write_still=True)
    _export_rig(name, folder)
