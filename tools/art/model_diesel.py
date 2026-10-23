"""Battler's Coalition in chunky low-poly dieselpunk: model_diesel.py -- <name>
(The user, 2026-10-22: "change the aesthetic to dieselpunk ... simplify it a lot more just to test out, let's focus on
1 civ"; chunky low-poly picked.) Few big shapes per model, with the dieselpunk tells that read from RTS distance:
riveted slab armour (the weathering bakes the rivets), exhaust stacks, big road wheels, propeller aircraft; olive drab
and gunmetal, soot and rust; warm amber lamps (the game makes them glow) instead of cyan light. Team colour on the
roofs. No crew. Same sizes as the models they replace, so nothing in play changes.
Names: lighttank tank mortar artillery ranger flier bomber scout mobilebase | hq factory_lighttank factory_tank
factory_mortar factory_artillery factory_ranger factory_air factory_mobilebase extractor powerplant turret flak
bombard wall outpost
Run: blender --background --python tools/art/model_diesel.py -- tank   (one per run)"""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *
import math

# Olive drab and gunmetal, sooty blacks, amber lamps ("glow": the game lights it), faded yellow markings.
k.recolour(steel=(104, 110, 78), plate=(86, 90, 64), mid=(78, 76, 68), dark=(44, 42, 38), black=(26, 24, 22),
           glow=(255, 168, 48), hazard=(196, 160, 64), white=(206, 200, 182))
GRIT = 2.3


def xs(s, a, b):
    """The span between a and b on side s (+1 left, -1 right), low to high."""
    return tuple(sorted((s * a, s * b)))


def wheels(s, a0, a1, n, r, x, w=1.0):
    """Big road wheels down side s, centred `x` out: chunky black tyres with a gunmetal hub."""
    for i in range(n):
        a = a0 if n == 1 else a0 + (a1 - a0) * i / (n - 1)
        tube_between((a, s * (x - w / 2), r), (a, s * (x + w / 2), r), r, "black", 10)
        tube_between((a, s * (x + w / 2), r), (a, s * (x + w / 2 + 0.12), r), r * 0.45, "mid", 8)


def tracked(s, inner, outer, a0, a1, h):
    """A track run on side s: a black belt with rounded ends, road wheels on its outer face, a mudguard over it."""
    x0, x1 = xs(s, inner, outer)
    r = h / 2
    k.box(a0 + r, a1 - r, x0, x1, 0.0, h, "black")
    for a in (a0 + r, a1 - r):
        tube_between((a, x0, r), (a, x1, r), r, "black", 10)
    n = max(3, int((a1 - a0) / (h * 0.9)))
    wheels(s, a0 + r, a1 - r, n, r * 0.8, outer - 0.3, 0.5)
    k.box(a0 - 0.2, a1 + 0.2, x0 - 0.1, x1 + 0.1, h, h + 0.35, "plate")


def stack(a, x, z0, h, r=0.45):
    """An exhaust stack: a gunmetal pipe with a sooty cap."""
    k.drum(a, x, r, z0, z0 + h, "mid", 8)
    k.drum(a, x, r * 1.25, z0 + h, z0 + h + 0.4, "dark", 8)


def barrel(a0, a1, z, r, x=0.0):
    """A round gun barrel along a, with a muzzle brake."""
    k.tube(a0, a1, x, z, r, "dark", 10)
    k.tube(a1 - 0.9, a1 + 0.15, x, z, r * 1.5, "dark", 10)


def lamp(a, x, z, r=0.3):
    """A headlamp facing forward (amber: the game lights it)."""
    k.tube(a - 0.1, a + 0.15, x, z, r, "glow", 8)


def prop(a, x, z, r):
    """A spinning propeller seen as a dark disc, a spinner in front."""
    tube_between((a, x, z), (a + 0.08, x, z), r, "dark", 14)
    tube_between((a, x, z), (a + 0.6, x, z), r * 0.18, "plate", 8, r * 0.02)


# ---- units ----------------------------------------------------------------------------------------------------

def spoked_wheel(a, s, z, r, inner, outer, spokes=10, lugs=30):
    """A tall spoked wheel on side s: a lugged black tyre, a thin gunmetal rim, round spokes, a bolted hub."""
    x0, x1 = xs(s, inner, outer)
    xm = (x0 + x1) / 2
    out = 1 if s > 0 else -1
    wlathe(a, z, [(r * 0.8, x0), (r * 0.93, x0), (r, x0 + 0.12), (r, x1 - 0.12), (r * 0.93, x1), (r * 0.8, x1),
                  (r * 0.8, x0)], "black", 32, caps=False)
    wlathe(a, z, [(r * 0.81, xm - 0.18), (r * 0.81, xm + 0.18), (r * 0.72, xm + 0.18), (r * 0.72, xm - 0.18),
                  (r * 0.81, xm - 0.18)], "plate", 32, caps=False)
    for i in range(spokes):
        t = i * math.tau / spokes
        tube_between((a + math.cos(t) * r * 0.2, xm, z + math.sin(t) * r * 0.2),
                      (a + math.cos(t) * r * 0.74, xm, z + math.sin(t) * r * 0.74), 0.07, "plate", 6)
    wlathe(a, z, [(r * 0.24, xm - out * 0.3), (r * 0.24, xm + out * 0.3), (r * 0.15, xm + out * 0.45)], "dark", 16)
    for i in range(6):
        t = i * math.tau / 6
        p = (a + math.cos(t) * r * 0.17, xm + out * 0.33, z + math.sin(t) * r * 0.17)
        tube_between(p, (p[0], p[1] + out * 0.08, p[2]), 0.04, "steel", 6)
    for i in range(lugs):
        t = i * math.tau / lugs
        q = lambda xx: (a + math.cos(t) * (r + 0.06), xx, z + math.sin(t) * (r + 0.06))
        tube_between(q(x0 + 0.04), q(x1 - 0.04), 0.065, "black", 4)


def lighttank():
    """Light tank, the Girder Crab (FLUX concept flux_crab_1_s91, picked by the user 2026-10-22): an open lattice
    chassis on six tall spoked wheels, a diesel engine with a big ribbed flywheel at the front, an armoured box at
    the back carrying a turntable with a double-ended swivel cannon and twin optics; lamps on stalks, hoses."""
    k.begin(0.0)
    zc = 1.45                                                                               # wheel centres
    for a in (-3.3, 0.0, 3.3):
        for s in (1, -1):
            spoked_wheel(a, s, zc, 1.45, 2.35, 3.15)
        tube_between((a, -2.5, zc), (a, 2.5, zc), 0.16, "dark", 8)                          # axle
    # Lattice chassis: two girders (top and bottom chords, posts, diagonals) and cross beams.
    for s in (1, -1):
        x = s * 1.75
        for zz in (1.15, 2.15):
            k.box(-4.4, 4.8, *xs(s, 1.55, 1.95), zz - 0.12, zz + 0.12, "dark")
        for i in range(9):
            a0 = -4.3 + i * 1.1
            tube_between((a0, x, 1.15), (a0, x, 2.15), 0.07, "dark", 4)
            if i < 8:
                tube_between((a0, x, 1.15 if i % 2 == 0 else 2.15), (a0 + 1.1, x, 2.15 if i % 2 == 0 else 1.15), 0.06, "dark", 4)
    for a in (-4.3, -1.6, 1.6, 4.7):
        k.box(a - 0.15, a + 0.15, -1.75, 1.75, 1.9, 2.25, "dark")
    k.box(4.5, 5.0, -1.9, 1.9, 1.0, 1.6, "plate")                                           # front bumper
    # The engine at the front: block, cylinder heads, a big ribbed flywheel facing forward, exhaust.
    k.box(1.4, 4.2, -1.5, 1.1, 2.25, 3.5, "mid")
    for i in range(3):
        k.box(1.7 + i * 0.85, 2.3 + i * 0.85, -1.3, 0.9, 3.5, 3.85, "dark")
        k.drum(2.0 + i * 0.85, -0.2, 0.16, 3.85, 4.1, "steel", 8)
    hlathe(-0.2, 2.9, [(0.95, 4.2), (1.05, 4.3), (1.05, 4.75), (0.9, 4.85), (0.4, 4.9)], "dark", 32)
    for i in range(16):
        t = i * math.tau / 16
        tube_between((4.32, -0.2 + math.cos(t) * 1.08, 2.9 + math.sin(t) * 1.08), (4.72, -0.2 + math.cos(t) * 1.08, 2.9 + math.sin(t) * 1.08), 0.06, "black", 4)
    hlathe(-0.2, 2.9, [(0.3, 4.85), (0.3, 5.05)], "steel", 12)
    hose([(3.6, -1.4, 3.7), (3.2, -1.9, 3.2), (1.6, -1.9, 3.0), (1.2, -1.7, 3.5)], 0.13, "dark")
    hose([(2.0, 1.1, 3.0), (1.3, 1.6, 2.8), (0.8, 1.9, 3.2)], 0.11, "black")
    k.drum(2.4, 1.3, 0.22, 3.4, 4.4, "mid", 10)                                             # stubby exhaust
    k.drum(2.4, 1.3, 0.28, 4.4, 4.6, "black", 10)
    # The armoured box at the back: riveted plate, a team stripe, corner caps.
    k.box(-4.3, 1.3, -1.95, 1.95, 2.25, 4.5, "steel")
    for s in (1, -1):
        k.box(-3.0, -0.6, *xs(s, 1.95, 2.0), 3.6, 3.9, "paint")
        for a in [-4.1 + i * 0.6 for i in range(10)]:
            tube_between((a, s * 1.95, 4.35), (a, s * 2.04, 4.35), 0.05, "mid", 6, 0.03)
            tube_between((a, s * 1.95, 2.4), (a, s * 2.04, 2.4), 0.05, "mid", 6, 0.03)
    for a in (-4.0, 1.0):
        for s in (1, -1):
            k.drum(a, s * 1.65, 0.18, 4.5, 4.85, "dark", 8)
    # Turntable and the swivel gun house: a long barrel forward, a short heavy one aft, twin optics on a post.
    k.drum(-1.5, 0, 1.7, 4.5, 4.7, "mid", 32)
    k.drum(-1.5, 0, 1.45, 4.7, 4.85, "plate", 32)
    gz = 5.45
    k.box(-2.5, -0.3, -0.85, 0.85, 4.85, 6.05, "steel")
    k.box(-2.4, -0.4, -0.75, 0.75, 6.05, 6.15, "paint")
    hlathe(0, gz, [(0.42, -0.4), (0.42, 0.4), (0.27, 0.45), (0.27, 4.5), (0.35, 4.55), (0.35, 5.0), (0.2, 5.02)], "dark", 20)
    hlathe(0, gz, [(0.2, -5.0), (0.36, -4.98), (0.36, -4.5), (0.3, -4.45), (0.3, -2.6), (0.42, -2.5)], "dark", 20)
    k.drum(-1.8, 0, 0.14, 6.15, 6.6, "dark", 8)
    for x in (-0.28, 0.28):
        hlathe(x, 6.8, [(0.24, -2.3), (0.27, -2.2), (0.27, -1.3), (0.24, -1.25)], "dark", 16)
        hlathe(x, 6.8, [(0.18, -1.27), (0.18, -1.2)], "black", 12)
    k.box(-2.0, -1.6, -0.45, 0.45, 6.55, 6.7, "dark")
    # Lamps on stalks at the front corners; a hose from the engine up to the gun.
    for s in (1, -1):
        tube_between((4.6, s * 1.6, 2.2), (4.6, s * 1.6, 3.3), 0.08, "dark", 6)
        lamp(4.7, s * 1.6, 3.4, 0.26)
        k.box(4.4, 4.7, *xs(s, 1.35, 1.85), 3.62, 3.7, "dark")
    hose([(1.2, 0.9, 3.6), (0.4, 1.0, 4.9), (-0.4, 0.7, 5.3)], 0.1, "black")
    finish_realistic("lighttank", 24.0, grit=1.0, panels=False, bevel=0.035)


def rivet_ring(a, x, z, r, n=28, mat="mid", lo=-0.6, hi=3.75):
    """Rivet heads round a drum at a, `r` out from its axis (x, z), skipping the hidden underside."""
    for i in range(n):
        t = lo + (hi - lo) * i / (n - 1)
        c, d = math.cos(t), math.sin(t)
        tube_between((a, x + c * r, z + d * r), (a, x + c * (r + 0.1), z + d * (r + 0.1)), 0.09, mat, 6, 0.05)


def arc_plate(a, x0, x1, z, r0, r1, t0, t1, mat, steps=16):
    """A curved plate over a wheel (a mudguard), one smooth piece: radii r0..r1 from (a, z), angles t0..t1, across x0..x1."""
    import bmesh, bpy
    bm = bmesh.new()
    loops = []
    for i in range(steps + 1):
        t = t0 + (t1 - t0) * i / steps
        c, d = math.cos(t), math.sin(t)
        loops.append([bm.verts.new(k.at(a + c * rr, xx, z + d * rr)) for rr, xx in ((r0, x0), (r0, x1), (r1, x1), (r1, x0))])
    for i in range(steps):
        for j in range(4):
            f = bm.faces.new([loops[i][j], loops[i][(j + 1) % 4], loops[i + 1][(j + 1) % 4], loops[i + 1][j]])
            f.smooth = j in (1, 3)      # (the curved inner and outer faces; the edges stay crisp)
    bm.faces.new(loops[0])
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("piece")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("piece", me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(k.mats[mat])
    k.pieces.append(ob)


def hose(points, r=0.14, mat="black"):
    """A hose through points ((a, x, z) each)."""
    for p, q in zip(points, points[1:]):
        tube_between(p, q, r, mat, 8)


def lug_tyre(a, s, z, r, inner, outer, lugs=18):
    """A big tyre on side s: smooth black carcass, a dished gunmetal rim, a hub with a bolt circle, chevron lugs."""
    x0, x1 = xs(s, inner, outer)
    xm = (x0 + x1) / 2
    wlathe(a, z, [(r * 0.62, x0), (r * 0.9, x0), (r, x0 + 0.18), (r, x1 - 0.18), (r * 0.9, x1), (r * 0.62, x1)], "black", 32)
    face = x1 if s > 0 else x0
    out = 1 if s > 0 else -1
    wlathe(a, z, [(r * 0.64, face - out * 0.3), (r * 0.64, face + out * 0.02), (r * 0.5, face + out * 0.06),
                  (r * 0.3, face - out * 0.05)], "mid", 32)
    wlathe(a, z, [(r * 0.3, face - out * 0.1), (r * 0.3, face + out * 0.2), (r * 0.2, face + out * 0.32)], "dark", 20)
    for i in range(8):
        t = i * math.tau / 8
        p = (a + math.cos(t) * r * 0.25, face + out * 0.18, z + math.sin(t) * r * 0.25)
        tube_between(p, (p[0], p[1] + out * 0.1, p[2]), 0.06, "steel", 6)
    for i in range(lugs):
        t = i * math.tau / lugs
        R = r + 0.08
        e0 = (a + math.cos(t) * R, x0 + 0.05, z + math.sin(t) * R)
        m = (a + math.cos(t + 0.12) * R, xm, z + math.sin(t + 0.12) * R)
        e1 = (a + math.cos(t) * R, x1 - 0.05, z + math.sin(t) * R)
        tube_between(e0, m, 0.16, "black", 4)
        tube_between(m, e1, 0.16, "black", 4)


def tank():
    """Tank, the Gun Drum (FLUX concept flux_drum_1_s91, picked by the user 2026-10-22): no familiar tank. A big
    riveted drum lying along a girder chassis on four huge lugged tyres, a rust band round its middle, a domed nose
    with a stubby cannon (team-coloured muzzle ring), a hatch box with amber lamps on top, a machine gun slung on its
    left flank, exhaust and hoses at the back. Detail is geometry (smooth round parts, rivet heads, leaf springs), not
    painted seams (the user: "less lines, it looks a bit aliased")."""
    k.recolour(brass_dark=(146, 78, 38))                                                    # (rust, for the band)
    k.begin(0.0)
    Z, R = 5.3, 2.8                                                                         # the drum's axis height and radius
    # Running gear: four lugged tyres, mudguards, axles with leaf springs, the girder chassis.
    for a in (-4.6, 4.4):
        tube_between((a, -3.0, 2.35), (a, 3.0, 2.35), 0.34, "dark", 12)
        wlathe(a, 2.35, [(0.75, -0.6), (0.75, 0.6)], "dark", 16)                              # diff housing
        for s in (1, -1):
            lug_tyre(a, s, 2.35, 2.3, 2.85, 4.6)
            arc_plate(a, *xs(s, 2.7, 4.85), 2.35, 2.62, 2.74, math.radians(25), math.radians(155), "plate", 20)
            for h, l in ((0.0, 1.5), (0.16, 1.2), (0.32, 0.9), (0.48, 0.6)):
                k.box(a - l, a + l, *xs(s, 1.55, 2.45), 2.7 + h, 2.86 + h, "dark")          # leaf spring
    for s in (1, -1):
        k.box(-5.6, 5.4, *xs(s, 1.5, 2.5), 3.3, 3.9, "dark")                               # chassis rail
        k.box(-5.6, 5.4, *xs(s, 1.6, 2.4), 3.9, 4.0, "mid")
    for a in (-5.4, -1.8, 1.6, 5.2):
        k.box(a - 0.25, a + 0.25, -1.6, 1.6, 3.35, 3.85, "dark")                           # cross members
    for a in (-1.8, 1.6):
        k.box(a - 0.5, a + 0.5, -1.2, 1.2, 3.85, Z - R + 0.4, "dark")                      # drum saddles
    # The drum: one smooth turned body, back cap to domed nose.
    nose = [(R * math.cos(t * math.pi / 2 / 8), 3.9 + 2.5 * math.sin(t * math.pi / 2 / 8)) for t in range(1, 9)]
    hlathe(0, Z, [(2.1, -6.7), (2.55, -6.55), (2.75, -6.3), (R, -6.0), (R, 3.9)] + nose, "steel", 40)
    hlathe(0, Z, [(R + 0.06, -1.6), (R + 0.06, 1.4)], "brass_dark", 40)                     # rust band
    for a0, a1 in ((-5.75, -5.45), (-3.55, -3.25), (3.45, 3.95)):
        hlathe(0, Z, [(R + 0.12, a0), (R + 0.12, a1)], "mid", 40)                          # rib rings
        rivet_ring((a0 + a1) / 2, 0, Z, R + 0.12)
    for a in (-1.45, 1.25):
        rivet_ring(a, 0, Z, R + 0.06, 32)
    hlathe(0, Z, [(1.25, -6.85), (1.25, -6.68)], "plate", 24)                              # back hatch
    tube_between((-6.9, -0.6, Z), (-6.9, 0.6, Z), 0.07, "dark", 6)
    # The stubby cannon out of the nose, its muzzle ring in team colour, a bolted face, the bore.
    gx, gz = 0.55, 4.7
    hlathe(gx, gz, [(1.15, 5.4), (1.15, 6.3), (0.9, 6.45), (0.78, 6.45), (0.78, 7.55), (1.0, 7.6), (1.0, 8.15),
                    (1.12, 8.2), (1.12, 8.65), (0.98, 8.72)], "dark", 32)
    hlathe(gx, gz, [(1.04, 7.62), (1.04, 8.13)], "paint", 32)
    hlathe(gx, gz, [(0.55, 8.7), (0.55, 8.78)], "black", 24)
    for i in range(10):
        t = i * math.tau / 10
        tube_between((8.68, gx + math.cos(t) * 0.86, gz + math.sin(t) * 0.86), (8.8, gx + math.cos(t) * 0.86, gz + math.sin(t) * 0.86), 0.07, "steel", 6)
    # On top: a hatch box with louvres, a team-coloured lid, an amber lamp strip, an aerial; grab rails.
    top = Z + R
    k.box(-4.4, -1.2, -1.3, 1.3, top - 0.5, top + 0.55, "plate")
    k.box(-4.3, -1.3, -1.2, 1.2, top + 0.55, top + 0.72, "paint")
    k.box(-1.25, -1.12, -0.95, 0.95, top - 0.1, top + 0.3, "glow")
    for i in range(4):
        for s in (1, -1):
            k.box(-4.0 + i * 0.7, -3.6 + i * 0.7, *xs(s, 1.3, 1.38), top - 0.2, top + 0.35, "dark")   # louvres
    k.drum(-3.8, -0.9, 0.16, top + 0.72, top + 0.95, "dark", 8)
    tube_between((-3.8, -0.9, top + 0.95), (-3.8, -0.9, top + 4.4), 0.05, "dark", 4)
    for s in (1, -1):
        rx, rz = s * 1.65, Z + math.sqrt(R * R - 1.65 * 1.65)
        for a in (-0.4, 1.4, 3.0):
            tube_between((a, rx, rz - 0.05), (a, rx, rz + 0.4), 0.06, "dark", 6)
        tube_between((-0.4, rx, rz + 0.4), (3.0, rx, rz + 0.4), 0.06, "dark", 6)
    # Left flank: a slung machine gun (receiver, perforated cooling jacket, ammo drum) on a bracket, with a lamp.
    k.box(-2.6, 0.2, 2.45, 2.85, 3.6, 4.4, "dark")
    k.box(-2.9, 0.4, 2.85, 3.75, 3.2, 4.2, "mid")
    hlathe(3.3, 3.7, [(0.3, 0.4), (0.3, 3.4)], "dark", 16)
    for a in (0.8, 1.4, 2.0, 2.6, 3.2):
        hlathe(3.3, 3.7, [(0.34, a), (0.34, a + 0.12)], "black", 16)
    hlathe(3.3, 3.7, [(0.12, 3.4), (0.12, 4.7), (0.2, 4.75), (0.2, 4.95)], "dark", 10)
    k.drum(-1.6, 4.05, 0.5, 4.2, 4.75, "dark", 16)
    lamp(0.5, 3.3, 2.95, 0.32)
    k.box(0.35, 0.65, 2.95, 3.65, 3.3, 3.4, "dark")                                         # its hood
    # Right flank: a toolbox with hinges, two jerry cans.
    k.box(-3.2, 0.4, -3.55, -2.75, 3.3, 4.3, "plate")
    for a in (-2.6, -0.4):
        k.box(a - 0.15, a + 0.15, -3.6, -3.5, 4.0, 4.32, "dark")
    for a in (1.0, 2.0):
        k.box(a, a + 0.8, -3.45, -2.75, 3.3, 4.6, "mid")
        k.box(a + 0.3, a + 0.5, -3.5, -3.4, 3.4, 4.5, "dark")
    # Back: exhaust stack with a silencer and a heat shield, hoses down to the gun and the chassis.
    sx, sz = 2.0, Z + 1.9
    k.drum(-5.5, sx, 0.34, sz, sz + 3.4, "mid", 16)
    k.drum(-5.5, sx, 0.55, sz + 0.6, sz + 1.9, "dark", 16)
    k.drum(-5.5, sx, 0.42, sz + 3.4, sz + 3.7, "black", 16)
    k.box(-5.0, -4.9, sx - 0.5, sx + 0.5, sz + 0.5, sz + 2.0, "plate")
    hose([(-5.4, 2.35, sz + 0.4), (-4.6, 2.95, sz - 1.0), (-3.4, 3.2, 4.6), (-2.8, 3.3, 4.2)])
    hose([(-6.2, -1.4, 4.2), (-6.0, -2.0, 3.6), (-5.4, -2.0, 3.6)])
    hose([(-6.1, 1.2, Z - 1.2), (-6.4, 1.6, 4.3), (-5.8, 2.0, 3.9)], 0.12, "dark")
    # Front: headlamps with hoods on the chassis, tow hooks.
    for s in (1, -1):
        lamp(5.75, s * 1.9, 3.6, 0.3)
        k.box(5.5, 5.9, *xs(s, 1.55, 2.25), 3.95, 4.05, "dark")
        k.box(5.4, 5.9, *xs(s, 0.9, 1.2), 3.0, 3.5, "dark")
        k.box(-6.1, -5.6, *xs(s, 0.9, 1.2), 3.0, 3.5, "dark")
    finish_realistic("tank", 30.0, serial="12", grit=1.0, panels=False, bevel=0.04)      # (clean: the paint pass adds the wear)


def mortar():
    """Mortar: a half-track, big wheels at the front and a track behind, a fat mortar tube standing in its open bay."""
    k.begin(0.0)
    for s in (1, -1):
        wheels(s, 3.0, 3.0, 1, 1.3, 2.9, 0.9)
        tracked(s, 2.4, 4.2, -5.0, 1.0, 2.2)
    plate(-5.2, 5.0, -3.2, 3.2, 1.0, 4.0, "steel", inset=0.4, front=1.8, back=0.4)
    plate(1.4, 4.0, -2.2, 2.2, 4.0, 4.2, "paint", inset=0.15)
    hexagon(-2.0, 0, 1.6, 4.0, 4.6, "mid", r1=1.4)
    tube_between((-2.0, 0, 4.4), (-0.6, 0, 9.8), 0.95, "dark", 12)
    tube_between((-1.6, 0, 6.0), (-1.4, 0, 6.6), 1.1, "mid", 12)
    stack(3.6, -2.4, 4.0, 1.6, 0.3)
    for s in (1, -1):
        lamp(5.1, s * 2.2, 3.0)
    finish_realistic("mortar", 26.0, grit=GRIT)


def artillery():
    """Artillery: a tracked self-propelled howitzer, the gun house at the back, a very long barrel raised high."""
    k.begin(0.0)
    for s in (1, -1):
        tracked(s, 3.2, 5.2, -7.4, 6.8, 2.5)
    plate(-7.8, 7.4, -3.8, 3.8, 1.0, 4.0, "steel", inset=0.4, front=2.2, back=0.5)
    plate(-7.0, -0.6, -3.2, 3.2, 4.0, 6.6, "plate", inset=0.6, front=1.4)
    plate(-6.0, -2.0, -2.2, 2.2, 6.6, 6.85, "paint", inset=0.15)
    tube_between((-3.4, 0, 5.8), (9.2, 0, 14.6), 0.55, "dark", 12)
    tube_between((-1.8, 0, 6.9), (1.8, 0, 9.4), 0.85, "mid", 12)                            # recoil sleeve
    tube_between((8.2, 0, 13.9), (9.7, 0, 15.0), 0.9, "dark", 12)                           # muzzle brake
    for s in (1, -1):
        plate(-9.2, -7.6, *xs(s, 2.2, 3.6), 0.0, 1.0, "plate", inset=0.15, back=0.6)        # spades
        stack(5.6, s * 2.6, 4.0, 1.8, 0.35)
    finish_realistic("artillery", 36.0, grit=GRIT)


def ranger():
    """Ranger: a squat two-legged diesel walker, a boxy body with an exhaust on its back and a rifle on its arm."""
    k.begin(0.0)
    for s in (1, -1):
        k.box(-0.6, 0.8, *xs(s, 0.7, 1.7), 1.4, 3.2, "plate")                              # thigh
        k.box(-0.4, 0.9, *xs(s, 0.75, 1.65), 0.3, 1.6, "dark")                              # shin
        k.box(-0.9, 1.3, *xs(s, 0.6, 1.8), 0.0, 0.35, "black")                              # foot
    plate(-1.4, 1.6, -1.6, 1.6, 3.0, 5.2, "steel", inset=0.3, front=0.6)
    plate(-0.9, 0.9, -1.0, 1.0, 5.2, 5.35, "paint", inset=0.1)
    k.box(0.2, 1.2, -0.5, 0.5, 5.35, 6.0, "mid")                                            # sensor head
    lamp(1.25, 0, 5.7, 0.18)
    stack(-1.7, 0.8, 3.6, 2.6, 0.25)
    k.tube(0.0, 4.4, -1.9, 4.0, 0.2, "dark", 8)                                             # rifle
    k.box(-0.6, 0.8, -2.15, -1.65, 3.6, 4.3, "mid")
    finish_realistic("ranger", 14.0, grit=GRIT)


def flier():
    """Flier: a propeller fighter: a tapered fuselage, straight wings, a big prop disc, a tail. Flies."""
    k.begin(0.0)
    tube_between((-4.8, 0, 1.4), (4.0, 0, 1.4), 0.45, "steel", 10, 0.95)                   # fuselage
    k.tube(4.0, 5.0, 0, 1.4, 0.95, "dark", 10)                                              # engine cowling
    prop(5.0, 0, 1.4, 2.0)
    for s in (1, -1):
        k.slab([(1.6, s * 0.8), (0.4, s * 5.8), (-0.8, s * 5.8), (-0.6, s * 0.8)] if s > 0 else
               [(1.6, -0.8), (-0.6, -0.8), (-0.8, -5.8), (0.4, -5.8)], 1.0, 1.3, "plate")   # wings
        k.slab([(0.9, s * 3.6), (0.6, s * 5.0), (-0.6, s * 5.0), (-0.5, s * 3.6)] if s > 0 else
               [(0.9, -3.6), (-0.5, -3.6), (-0.6, -5.0), (0.6, -5.0)], 1.3, 1.36, "paint")  # wing markings
        k.slab([(-3.8, s * 0.3), (-4.6, s * 2.2), (-5.2, s * 2.2), (-4.8, s * 0.3)] if s > 0 else
               [(-3.8, -0.3), (-4.8, -0.3), (-5.2, -2.2), (-4.6, -2.2)], 1.3, 1.45, "plate")  # tailplane
        k.box(3.0, 3.6, *xs(s, 0.85, 1.05), 1.5, 1.7, "dark")                               # exhaust stubs
    k.upright([(-3.6, 1.6), (-5.0, 1.6), (-5.2, 3.4), (-4.4, 3.4)], -0.1, 0.1, "plate")     # fin
    finish_realistic("flier", 18.0, grit=GRIT)


def bomber():
    """Bomber: a twin-engined propeller bomber: a long fuselage, broad wings with two engines, a twin tail. Flies."""
    k.begin(0.0)
    tube_between((-5.2, 0, 1.6), (4.6, 0, 1.6), 0.6, "steel", 10, 1.3)
    tube_between((4.6, 0, 1.6), (5.8, 0, 1.6), 1.3, "dark", 10, 0.4)                       # nose
    for s in (1, -1):
        k.slab([(1.4, s * 1.0), (0.6, s * 8.0), (-1.6, s * 8.0), (-1.6, s * 1.0)] if s > 0 else
               [(1.4, -1.0), (-1.6, -1.0), (-1.6, -8.0), (0.6, -8.0)], 1.2, 1.6, "plate")   # wings
        k.slab([(0.8, s * 5.0), (0.6, s * 6.6), (-1.4, s * 6.6), (-1.4, s * 5.0)] if s > 0 else
               [(0.8, -5.0), (-1.4, -5.0), (-1.4, -6.6), (0.6, -6.6)], 1.6, 1.66, "paint")
        k.tube(-1.2, 2.6, s * 3.6, 1.4, 0.75, "mid", 10)                                    # engine nacelle
        prop(2.6, s * 3.6, 1.4, 1.7)
        k.slab([(-4.4, s * 0.4), (-4.8, s * 2.6), (-5.6, s * 2.6), (-5.4, s * 0.4)] if s > 0 else
               [(-4.4, -0.4), (-5.4, -0.4), (-5.6, -2.6), (-4.8, -2.6)], 1.6, 1.75, "plate")   # tailplane
        k.upright([(-4.6, 1.75), (-5.6, 1.75), (-5.8, 3.4), (-5.1, 3.4)], s * 2.4 - 0.1, s * 2.4 + 0.1, "plate")   # twin fins
    k.box(-1.4, 1.6, -0.6, 0.6, 0.6, 0.9, "black")                                          # bomb bay
    finish_realistic("bomber", 24.0, grit=GRIT)


def scout():
    """Scout: a light four-wheeled scout car with a tall radio mast. Unarmed."""
    k.begin(0.0)
    for s in (1, -1):
        wheels(s, -2.2, 2.2, 2, 1.0, 1.9, 0.7)
    plate(-3.0, 3.4, -1.6, 1.6, 0.8, 2.4, "steel", inset=0.3, front=1.2)
    plate(-2.0, 1.0, -1.0, 1.0, 2.4, 2.55, "paint", inset=0.1)
    k.drum(-1.8, -0.8, 0.12, 2.4, 5.6, "dark", 4)                                           # radio mast
    k.drum(-1.8, -0.8, 0.3, 5.6, 5.9, "glow", 6)
    for s in (1, -1):
        lamp(3.45, s * 1.0, 1.9, 0.22)
    finish_realistic("scout", 18.0, grit=GRIT)


def mobilebase():
    """Mobile base: a big tracked crawler, a cabin up front, a folded crane on its deck, two chimneys. Unarmed."""
    k.begin(0.0)
    for s in (1, -1):
        tracked(s, 5.0, 7.4, -10.5, 10.5, 3.2)
    plate(-11.2, 11.4, -6.2, 6.2, 1.4, 5.4, "steel", inset=0.6, front=2.6, back=1.0)
    plate(5.0, 10.0, -4.6, 4.6, 5.4, 8.4, "plate", inset=0.6, front=1.0)                   # cabin
    plate(-9.0, 3.6, -4.8, 4.8, 5.4, 5.7, "paint", inset=0.2)
    tube_between((-9.0, -3.0, 6.2), (5.0, -3.0, 7.8), 0.5, "dark", 6)                       # folded crane
    k.box(-9.6, -8.4, -4.0, -2.0, 5.4, 7.2, "hazard")
    for s in (1, -1):
        stack(-6.0, s * 3.6, 5.4, 3.6, 0.6)
        lamp(11.5, s * 3.4, 3.6, 0.4)
    finish_realistic("mobilebase", 42.0, serial="22", grit=GRIT)


# ---- buildings --------------------------------------------------------------------------------------------------

def hall(chimneys=1, door=5.0):
    """A wing's workshop: a riveted hall with a sawtooth roof, a team band over the big front door, chimneys."""
    apron(11.5, 9.5)
    plate(-9, 8, -7.5, 7.5, 0.5, 6.5, "steel", inset=0.8, front=0.3, back=0.5)
    for i in range(3):                                                                      # sawtooth roof
        a = -8.0 + i * 5.3
        k.upright([(a, 6.5), (a + 5.0, 6.5), (a + 5.0, 8.6)], -6.8, 6.8, "plate")
        k.box(a + 4.8, a + 5.05, -6.6, 6.6, 6.5, 8.4, "dark")
    k.box(7.6, 8.3, -7.0, 7.0, 5.2, 6.4, "paint")                                           # team band
    k.box(7.8, 8.4, -door, door, 0.5, 5.0, "black")                                         # the door
    for s in (1, -1):
        k.box(7.8, 8.6, s * door - 0.5, s * door + 0.5, 0.5, 5.0, "hazard")
    for i in range(chimneys):
        stack(-6.5 + i * 3.0, 5.2, 6.5, 6.0, 0.8)
    lamp(8.4, 0, 5.6, 0.35)


def factory_lighttank():
    k.begin(0.0)
    hall(1, 4.0)
    finish_realistic("factory_lighttank", 34.0, serial="31", grit=GRIT)


def factory_tank():
    k.begin(0.0)
    hall(2, 5.5)
    finish_realistic("factory_tank", 34.0, serial="32", grit=GRIT)


def factory_mortar():
    k.begin(0.0)
    hall(1, 4.5)
    tube_between((-2.0, -3.0, 8.4), (-1.0, -3.0, 12.0), 0.9, "dark", 10)                   # a mortar tube on the roof
    finish_realistic("factory_mortar", 34.0, serial="33", grit=GRIT)


def factory_artillery():
    k.begin(0.0)
    hall(1, 5.5)
    for s in (1, -1):                                                                       # a gantry crane over the yard
        k.box(8.6, 9.4, s * 7.0 - 0.4, s * 7.0 + 0.4, 0.5, 10.5, "mid")
    k.box(8.6, 9.4, -7.4, 7.4, 10.5, 11.3, "hazard")
    finish_realistic("factory_artillery", 34.0, serial="34", grit=GRIT)


def factory_ranger():
    k.begin(0.0)
    hall(1, 3.5)
    k.drum(-4.0, -5.0, 0.2, 6.5, 13.0, "dark", 6)                                           # radio mast
    k.drum(-4.0, -5.0, 0.4, 13.0, 13.4, "glow", 6)
    finish_realistic("factory_ranger", 34.0, serial="35", grit=GRIT)


def factory_mobilebase():
    k.begin(0.0)
    hall(1, 6.5)
    k.box(-1.0, 0.4, -4.6, -3.2, 8.6, 13.0, "mid")                                          # crane over the door
    tube_between((-0.3, -3.9, 12.6), (9.5, -1.0, 11.4), 0.4, "dark", 6)
    k.box(9.0, 10.0, -1.5, -0.5, 9.4, 11.4, "hazard")
    finish_realistic("factory_mobilebase", 34.0, serial="18", grit=GRIT)


def factory_air():
    """The airfield wing: a round-roofed hangar beside a landing strip with amber edge lamps and a windsock mast."""
    k.begin(0.0)
    apron(12.0, 10.0)
    arc = [(-10 + 0, 0.5)] + [(-4.5 + 5.5 * math.cos(math.pi * i / 8), 0.5 + 7.0 * math.sin(math.pi * i / 8)) for i in range(9)]
    k.upright([(a, z) for a, z in arc[1:]], -8.0, 8.0, "steel")                            # the hangar's arched roof
    k.box(-9.6, 0.6, -6.0, 6.0, 7.0, 7.4, "paint")
    k.box(0.8, 1.4, -6.0, 6.0, 0.5, 5.5, "black")                                          # its open end
    k.box(2.5, 10.5, -6.5, 6.5, 0.5, 0.9, "mid")                                            # the strip
    for z in (-6.2, 6.2):
        for a in (3.5, 6.0, 8.5):
            k.drum(a, z, 0.25, 0.9, 1.15, "glow", 6)
    k.drum(-8.0, 7.5, 0.25, 0.5, 9.0, "dark", 6)                                            # windsock mast
    tube_between((-8.0, 7.5, 8.6), (-6.4, 7.8, 8.2), 0.35, "hazard", 6, 0.15)
    finish_realistic("factory_air", 34.0, serial="52", grit=GRIT)


def hq():
    """HQ: a squat fortified command bunker, a raised command block with a team roof, three chimneys, radio masts."""
    k.begin(0.0)
    hexagon(0, 0, 18.5, 0.0, 0.6, "mid", r1=18.0, turn=math.pi / 8, sides=8)              # foundation
    plate(-14, 14, -12, 12, 0.6, 9.0, "steel", inset=2.0, front=1.5, back=1.5)             # the bunker
    plate(-8, 6, -7, 7, 9.0, 15.0, "plate", inset=1.0)                                       # command block
    k.box(-6.5, 4.5, -5.5, 5.5, 15.0, 15.3, "paint")
    for i, a in enumerate((-12.0, -9.0, -6.0)):
        stack(a, 9.0, 9.0, 8.0 + i, 1.0)
    for s in (1, -1):
        k.drum(2.0, s * 5.0, 0.25, 15.3, 26.0, "dark", 6)                                   # radio masts
        k.drum(2.0, s * 5.0, 0.5, 26.0, 26.5, "glow", 6)
        lamp(14.5, s * 8.0, 4.0, 0.7)
    k.box(13.6, 15.0, -5, 5, 0.6, 6.0, "black")                                            # the gate
    k.box(13.6, 15.2, -6, 6, 6.0, 7.0, "hazard")
    finish_realistic("hq", 46.0, serial="02", grit=GRIT)


def extractor():
    """Extractor: a pumpjack nodding over the deposit, a squat engine house and a storage tank, an exhaust stack."""
    k.begin(0.0)
    hexagon(0, 0, 9.0, 0.0, 1.0, "mid", r1=8.6, sides=8, turn=math.pi / 8)
    for s in (1, -1):                                                                       # the A-frame
        tube_between((-1.0, s * 2.0, 1.0), (0.0, s * 0.6, 9.0), 0.4, "dark", 6)
        tube_between((1.0, s * 2.0, 1.0), (0.0, s * 0.6, 9.0), 0.4, "dark", 6)
    k.box(-6.0, 6.0, -0.5, 0.5, 9.0, 9.9, "steel")                                          # the walking beam
    plate(5.6, 7.8, -0.9, 0.9, 6.6, 10.6, "plate", inset=0.3, front=1.2)                   # the horse head
    tube_between((6.8, 0, 6.6), (6.8, 0, 1.0), 0.15, "black", 4)                           # the rod down the well
    plate(-7.6, -3.4, -2.6, 2.6, 1.0, 5.0, "plate", inset=0.5)                             # the engine house
    k.box(-7.0, -4.0, -2.0, 2.0, 5.0, 5.2, "paint")
    stack(-6.6, 1.6, 5.0, 3.4, 0.4)
    k.drum(2.0, 5.6, 2.2, 1.0, 5.6, "steel", 12)                                           # storage tank
    k.drum(2.0, 5.6, 2.2, 5.6, 5.8, "paint", 12)
    lamp(-3.3, 0, 3.6, 0.35)
    finish_realistic("extractor", 34.0, serial="09", grit=GRIT)


def powerplant():
    """Power plant: a riveted diesel generator hall on its vent, two tall chimneys, radiator fins, a cable mast."""
    k.begin(0.0)
    apron(10.5, 9.0)
    plate(-8, 7, -6, 6, 0.5, 7.0, "steel", inset=1.0, front=0.4, back=0.6)
    plate(-7, 6, -5, 5, 7.0, 8.2, "plate", inset=0.8)
    k.box(-5.0, 3.0, -3.4, 3.4, 8.2, 8.45, "paint")
    for x in (-5.5, -2.0):
        stack(x, 4.0, 7.0, 8.5, 1.0)
    for i in range(6):
        a = -6.5 + i * 2.2
        k.box(a, a + 1.4, -7.2, -6.0, 0.5, 5.5, "mid")
    k.drum(5.0, -3.5, 0.4, 8.2, 12.5, "dark", 6)
    k.drum(5.0, -3.5, 0.9, 12.0, 12.4, "glow", 8)
    k.box(7.0, 7.8, -2.5, 2.5, 0.5, 5.0, "hazard")
    finish_realistic("powerplant", 30.0, serial="41", grit=GRIT)


def sandbags(r, n, z=0.0):
    """A ring of sandbag blocks round a gun pit."""
    for i in range(n):
        ang = math.tau * i / n
        ca, sa = math.cos(ang) * r, math.sin(ang) * r
        k.box(ca - 0.9, ca + 0.9, sa - 0.9, sa + 0.9, z, z + 1.2, "plate")


def turret():
    """Turret: a concrete pillbox ringed with sandbags, a squat gun turret on top with a long barrel."""
    k.begin(0.0)
    sandbags(6.0, 12)
    hexagon(0, 0, 4.6, 0.0, 3.2, "mid", r1=4.0, sides=8)
    plate(-2.8, 3.0, -2.4, 2.4, 3.2, 5.6, "steel", inset=0.5, front=1.0)
    plate(-2.0, 1.6, -1.6, 1.6, 5.6, 5.8, "paint", inset=0.15)
    barrel(2.6, 9.2, 4.4, 0.35)
    finish_realistic("turret", 22.0, serial="61", grit=GRIT)


def flak():
    """Flak: a sandbagged platform, a quad gun on a cradle pointing steeply up."""
    k.begin(0.0)
    sandbags(5.8, 12)
    hexagon(0, 0, 4.2, 0.0, 1.6, "mid", r1=4.0, sides=8)
    plate(-2.4, 2.4, -2.6, 2.6, 1.6, 4.2, "steel", inset=0.5)
    k.box(-1.6, 1.6, -1.8, 1.8, 4.2, 4.4, "paint")
    for x in (-1.5, -0.5, 0.5, 1.5):
        tube_between((0.8, x, 3.6), (5.6, x, 10.2), 0.22, "dark", 8)
    finish_realistic("flak", 22.0, serial="62", grit=GRIT)


def bombard():
    """Bombard: a howitzer in a sandbagged pit, its long barrel raised."""
    k.begin(0.0)
    sandbags(7.6, 14)
    hexagon(0, 0, 6.4, 0.0, 0.6, "mid", r1=6.2, sides=8)
    plate(-2.6, 1.8, -2.4, 2.4, 0.6, 3.6, "plate", inset=0.4)
    k.box(-2.0, 1.2, -1.6, 1.6, 3.6, 3.8, "paint")
    tube_between((-1.0, 0, 3.4), (7.0, 0, 12.6), 0.75, "dark", 12)
    tube_between((-0.4, 0, 4.0), (2.2, 0, 7.0), 1.1, "mid", 12)
    tube_between((6.2, 0, 11.7), (7.6, 0, 13.3), 1.15, "dark", 12)
    finish_realistic("bombard", 26.0, serial="63", grit=GRIT)


def wall():
    """Wall: a slab of concrete with steel tank-trap crosses along its top."""
    k.begin(0.0)
    plate(-5.4, 5.4, -5.4, 5.4, 0.0, 5.0, "mid", inset=1.0, front=1.0, back=1.0)
    k.box(-4.2, 4.2, -4.2, 4.2, 5.0, 5.2, "paint")
    for a in (-2.8, 0.0, 2.8):
        tube_between((a - 1.0, -1.0, 5.2), (a + 1.0, 1.0, 7.0), 0.22, "dark", 4)
        tube_between((a - 1.0, 1.0, 5.2), (a + 1.0, -1.0, 7.0), 0.22, "dark", 4)
    finish_realistic("wall", 16.0, serial="64", grit=GRIT)


def outpost():
    """Outpost (a mobile base set up): a sandbagged command post, a low bunker with a radio mast and a chimney."""
    k.begin(0.0)
    sandbags(10.0, 18)
    hexagon(0, 0, 8.0, 0.0, 1.0, "mid", r1=7.6, sides=8)
    plate(-5.5, 5.5, -5.0, 5.0, 1.0, 5.0, "steel", inset=1.0, front=0.8)
    k.box(-4.0, 4.0, -3.4, 3.4, 5.0, 5.3, "paint")
    k.drum(-3.0, -3.0, 0.22, 5.0, 14.0, "dark", 6)
    k.drum(-3.0, -3.0, 0.45, 14.0, 14.4, "glow", 6)
    stack(-3.6, 3.0, 5.0, 3.0, 0.5)
    finish_realistic("outpost", 30.0, serial="71", grit=GRIT)


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
globals()[which]()
