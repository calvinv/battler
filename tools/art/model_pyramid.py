"""The pyramid HQ in the cel shaded look: model_pyramid.py -- <name>
(The user, 2026-10-24: "the main base is a large central pyramid and the unit leave from it, the bases expands as you
collect more resources and will have a look that kind of tells you what it is producing" ... "lets spire up and get
fatter over time". FLUX concepts art/_gen/concepts/pyr_sheet_s1101.png: 7/8 the pyramid, 11 the hangar, 14 the
battery, 17/20 the air deck, 21/24 the dish.)
  hq_t1 .. hq_t5   the pyramid at each tier: stepped, chamfered corners, a door on each face (rig parts door_0 front
                   (+a), door_1 (+x), door_2 (-x), door_3 back), its eye turning (spin_eye); from tier 2 a spire
                   (spire concept 15): a glowing core in a cage, rings floating round it (spin_ring_N)
  addon_armour addon_walkers addon_guns addon_air addon_support addon_core
                   what each tech branch bolts on (game/Pyramid.cs places them from the same numbers as SHAPE)
Run: blender --background --python tools/art/model_pyramid.py -- hq_t3"""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *
import math

k.recolour(steel=(70, 76, 86), plate=(58, 63, 72), mid=(88, 94, 104), dark=(38, 41, 47), black=(20, 22, 26),
           glow=(255, 190, 70), glass_lit=(90, 232, 255), white=(206, 212, 220), hazard=(58, 63, 72))

# Per tier: half the base's width, steps above the tall bottom one, the spire's height. Model units (x 0.18 in game).
# (game/Pyramid.cs keeps the same table.)
SHAPE = {1: (22.0, 2, 0.0), 2: (24.0, 3, 8.0), 3: (26.0, 4, 16.0), 4: (28.0, 5, 24.0), 5: (30.0, 6, 32.0)}
BASE_H, STEP_H, TOP_HALF, CHAMFER = 11.0, 3.6, 7.0, 0.35
DW, DH = 8.0, 9.5   # half a door's width, its height


def cham(half, z, c=None):
    """A square `half` across with its corners cut (`c` of each side), as an outline at height z."""
    c = half * CHAMFER if c is None else c
    pts = []
    for sa, sx in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        if sa * sx > 0:
            pts += [(sa * half, sx * (half - c), z), (sa * (half - c), sx * half, z)]
        else:
            pts += [(sa * (half - c), sx * half, z), (sa * half, sx * (half - c), z)]
    return pts


def face_pt(f, u, n):
    """Face f (0 +a front, 1 +x, 2 -x, 3 -a back): `u` along it, `n` out from the middle -> (a, x)."""
    return {0: (n, u), 3: (-n, -u), 1: (-u, n), 2: (u, -n)}[f]


def fbox(f, u0, u1, n0, n1, z0, z1, mat):
    (a0, x0), (a1, x1) = face_pt(f, u0, n0), face_pt(f, u1, n1)
    k.box(min(a0, a1), max(a0, a1), min(x0, x1), max(x0, x1), z0, z1, mat)


def ramp(f, u0, u1, n0, n1, z1, mat):
    """A ramp down from height z1 at n0 to the ground at n1."""
    def p(u, n, z):
        a, x = face_pt(f, u, n)
        return (a, x, z)
    low = [p(u0, n0, 0.0), p(u0, n1, 0.0), p(u1, n1, 0.0), p(u1, n0, 0.0)]
    high = [p(u0, n0, z1), p(u0, n1, 0.05), p(u1, n1, 0.05), p(u1, n0, z1)]
    k.solid(low, high, mat)


def heights(tier):
    half, steps, spire = SHAPE[tier]
    inset = (half - TOP_HALF) / (steps + 1)
    top = BASE_H + steps * STEP_H
    return half, steps, spire, inset, top


def ring(r0, r1, z0, z1, mat, sides=28):
    """A flat ring: r0 inside, r1 outside."""
    for i in range(sides):
        t0, t1 = i * math.tau / sides, (i + 1) * math.tau / sides
        pt = lambda r, t, z: (math.cos(t) * r, math.sin(t) * r, z)
        low = [pt(r0, t0, z0), pt(r1, t0, z0), pt(r1, t1, z0), pt(r0, t1, z0)]
        high = [pt(r0, t0, z1), pt(r1, t0, z1), pt(r1, t1, z1), pt(r0, t1, z1)]
        k.solid(low, high, mat)


def spire_core(tier, top, spire):
    """The spire from tier 2 (spire concept 15, art/_gen/concepts/spire2_cage_2_s1301.png): a glowing amber core in
    a cage of four armoured corner posts with a rib down each face, on a plinth with buttresses, a capped top with a
    stepped finial and a mast; riveted rings float round it and turn (spin_ring_N), one more from tier 4. The core's
    light in game gets stronger each tier (game/Pyramid.cs)."""
    C, P = 3.4 + 0.5 * tier, 1.1 + 0.08 * tier   # the cage's half width (fatter each tier), a corner post's width
    R = C * 1.414 + 0.3                   # the rings' inside (clear of the corners)
    z0 = top + 2.2                        # the cage's foot (on the plinth)
    z1 = z0 + spire                       # its top (under the cap)
    # The plinth and four buttresses with lit pads in front of them.
    k.solid(cham(C + 3.2, top + 0.6), cham(C + 2.6, z0), "plate")
    for f in range(4):
        fbox(f, -1.4, 1.4, C, C + 2.4, z0, z0 + 2.6, "steel")
        fbox(f, -1.2, 1.2, C + 2.4, C + 2.9, z0, z0 + 1.2, "steel")
        for u in (-C, C):
            fbox(f, u - 0.9, u + 0.9, C + 0.4, C + 1.8, z0, z0 + 0.25, "glow")
    # The core, then the cage over it: the posts, a rib down each face, a band at the foot and at the top.
    k.box(-C + 0.3, C - 0.3, -C + 0.3, C - 0.3, z0, z1, "glow")
    for sa in (1, -1):
        for sx in (1, -1):
            k.box(min(sa * C, sa * (C - P)), max(sa * C, sa * (C - P)), min(sx * C, sx * (C - P)), max(sx * C, sx * (C - P)),
                  z0, z1, "steel")
    for f in range(4):
        fbox(f, -0.4, 0.4, C - 0.4, C + 0.15, z0, z1, "plate")
        fbox(f, -C, C, C - 0.6, C + 0.2, z0, z0 + 1.6, "plate")                  # foot band
        fbox(f, -C, C, C - 0.6, C + 0.2, z1 - 1.2, z1, "plate")                  # top band
        for u in (-1.6, 1.6):                                                      # little lit vents on the top band
            fbox(f, u - 0.5, u + 0.5, C + 0.2, C + 0.3, z1 - 0.9, z1 - 0.4, "glow")
    # The cap, the stepped finial and the mast with its light.
    k.solid(cham(C + 0.7, z1, 0.8), cham(C + 0.4, z1 + 2.4, 0.8), "plate")
    k.solid(cham(C * 0.6, z1 + 2.4, 0.6), cham(C * 0.55, z1 + 3.6), "steel")
    k.drum(0, 0, 1.2, z1 + 3.6, z1 + 5.0, "mid", 10, r1=0.9)
    k.drum(0, 0, 0.3, z1 + 5.0, z1 + 8.5, "dark", 6)
    k.drum(0, 0, 0.6, z1 + 8.5, z1 + 9.2, "glow", 8)
    # The floating rings, riveted, each on its own slow turn.
    heights = [0.62] if tier < 4 else [0.38, 0.72]
    for n, h in enumerate(heights):
        zr = z0 + spire * h
        with part("spin_ring_%d" % n, (0, 0, zr)):
            ring(R + 0.2, R + 1.5, zr - 0.6, zr + 0.6, "mid")
            ring(R, R + 0.3, zr - 0.4, zr + 0.4, "dark")
            for i in range(14):
                th = i * math.tau / 14
                a, x = math.cos(th) * (R + 0.85), math.sin(th) * (R + 0.85)
                k.box(a - 0.22, a + 0.22, x - 0.22, x + 0.22, zr + 0.6, zr + 0.85, "dark")


def pyramid(tier):
    half, steps, spire, inset, top = heights(tier)
    k.begin(0.0)
    # The foundation, a little wider, lit at its corners.
    k.solid(cham(half + 2.0, 0.0), cham(half + 1.6, 0.6), "mid")
    # The tall bottom step (the doors are in it), then the steps going up, each a little in, a dark band round its foot
    # and a team band round the top of every other one.
    w = half
    k.solid(cham(w, 0.6), cham(w - 0.8, BASE_H), "steel")
    k.solid(cham(w - 0.7, BASE_H - 0.9), cham(w - 0.5, BASE_H - 0.4), "paint")
    z = BASE_H
    for i in range(steps):
        w1 = half - inset * (i + 1)
        k.solid(cham(w1, z), cham(w1 - 0.6, z + STEP_H), "steel" if i % 2 == 0 else "plate")
        k.solid(cham(w1 + 0.15, z), cham(w1 + 0.1, z + 0.5), "dark")
        if i % 2 == 1:
            k.solid(cham(w1 - 0.5, z + STEP_H - 0.8), cham(w1 - 0.45, z + STEP_H - 0.35), "paint")
        # Lit windows on each face of this step.
        for f in range(4):
            n = w1 - 0.25
            span = (w1 - w1 * CHAMFER) * 0.8
            count = max(2, int(span / 3.2))
            for j in range(count):
                u = -span + (j + 0.5) * 2 * span / count
                fbox(f, u - 0.6, u + 0.6, n - 0.3, n + 0.3, z + 1.2, z + 2.2, "glow" if (i + j + f) % 3 else "dark")
        z += STEP_H
    # The top platform.
    k.solid(cham(TOP_HALF + 0.3, top), cham(TOP_HALF, top + 0.6), "mid")

    # The doors: one in the middle of each face of the bottom step: a dark bay, a shutter that drops (door_N), a
    # frame with hazard posts, a lamp over it, and a ramp out.
    for f in range(4):
        n = half - 0.4
        fbox(f, -DW, DW, n - 2.6, n + 0.3, 0.6, DH, "black")
        fbox(f, -DW + 0.6, DW - 0.6, n - 2.5, n - 2.1, DH - 1.2, DH - 0.8, "glow")      # a light inside along its top
        for s_ in (-1, 1):
            fbox(f, s_ * DW - 0.8, s_ * DW + 0.8, n - 0.3, n + 0.8, 0.6, DH + 0.6, "plate")
            for zz in (1.4, 3.0, 4.6, 6.2, 7.8):
                fbox(f, s_ * DW - 0.6, s_ * DW + 0.6, n + 0.8, n + 0.88, zz, zz + 0.7, "paint")
        fbox(f, -DW - 0.8, DW + 0.8, n - 0.3, n + 0.9, DH, DH + 1.0, "plate")             # lintel
        fbox(f, -1.4, 1.4, n + 0.9, n + 1.2, DH + 0.15, DH + 0.8, "glow")                # lamp
        pa, px = face_pt(f, 0.0, n + 0.2)
        with part("door_%d" % f, (pa, px, 0.6)):
            fbox(f, -DW + 0.1, DW - 0.1, n - 0.1, n + 0.25, 0.6, DH, "dark")
            zz = 1.6
            while zz < DH - 0.5:
                fbox(f, -DW + 0.4, DW - 0.4, n + 0.25, n + 0.33, zz, zz + 0.3, "black")
                zz += 1.3
            fbox(f, -DW + 0.4, DW - 0.4, n + 0.25, n + 0.35, 0.8, 1.2, "paint")
        ramp(f, -DW, DW, half + 0.8, half + 7.0, 0.6, "plate")
        for s_ in (-1, 1):
            fbox(f, s_ * (DW - 0.7) - 0.15, s_ * (DW - 0.7) + 0.15, half + 0.8, half + 6.0, 0.0, 0.62, "glow")

    if spire == 0:
        # Tier 1: a turning eye on the top platform, two aerials.
        eye_z = top + 0.6
        k.drum(0, 0, 4.2, eye_z, eye_z + 1.0, "mid", 16, r1=4.0)
        with part("spin_eye", (0, 0, eye_z + 1.0)):
            k.drum(0, 0, 3.9, eye_z + 1.0, eye_z + 2.2, "dark", 16, r1=3.6)
            for i in range(6):
                th = i * math.tau / 6
                a, x = math.cos(th) * 3.7, math.sin(th) * 3.7
                k.box(a - 0.45, a + 0.45, x - 0.45, x + 0.45, eye_z + 1.3, eye_z + 1.9, "glow")
        k.drum(0, 0, 2.6, eye_z + 2.2, eye_z + 3.0, "plate", 12, r1=2.0)
        k.drum(0, 0, 1.8, eye_z + 3.0, eye_z + 3.5, "glow", 16, r1=1.4)
        for s in (1, -1):
            k.drum(-4.5, s * 4.5, 0.18, top + 0.6, top + 5.0, "dark", 6)
            k.drum(-4.5, s * 4.5, 0.35, top + 5.0, top + 5.4, "glow", 6)
    else:
        spire_core(tier, top, spire)
    finish_toon("hq_t%d" % tier, max(2 * half + 12, (top + spire + 10) * 2.3))


for _t in SHAPE:
    globals()["hq_t%d" % _t] = (lambda t: lambda: pyramid(t))(_t)


# ---- add-ons: each faces +a (outward from the corner or face it's bolted to), stands on z = 0 -----------------------

def addon_armour():
    """Armour branch (concept 11): a low vehicle hangar bolted to the corner, its shutter and a lit pad in front, a
    roof gantry with a hook."""
    k.begin(0.0)
    plate(-6.0, 6.0, -5.5, 5.5, 0.0, 0.5, "mid", inset=0.2)
    plate(-5.5, 3.0, -5.0, 5.0, 0.5, 6.5, "steel", inset=0.3, front=0.0)
    plate(-5.8, 3.6, -5.3, 5.3, 6.5, 7.4, "plate", inset=0.4, front=0.0)
    k.box(2.6, 3.1, -3.6, 3.6, 0.5, 5.4, "black")
    k.box(3.0, 3.2, -3.4, 3.4, 0.6, 5.2, "dark")
    for z in (1.4, 2.6, 3.8):
        k.box(3.2, 3.28, -3.2, 3.2, z, z + 0.25, "black")
    k.box(3.1, 6.0, -3.6, 3.6, 0.5, 0.6, "dark")
    for s in (1, -1):
        k.box(3.3, 5.8, s * 3.3 - 0.12, s * 3.3 + 0.12, 0.6, 0.66, "glass_lit")
        for z in (1.0, 2.4, 3.8):
            k.box(3.1, 3.18, *sorted((s * 3.8, s * 4.9)), z, z + 0.5, "paint")
    for a in (-4.5, -1.5):
        k.box(a, a + 2.4, -4.2, -1.0, 7.4, 7.55, "paint")
    for s in (1, -1):                                                                     # roof gantry
        k.box(-3.6, -3.0, s * 4.4 - 0.3, s * 4.4 + 0.3, 7.4, 11.0, "dark")
    k.box(-3.8, -2.8, -4.8, 4.8, 11.0, 11.7, "plate")
    k.box(-3.5, -3.1, 0.8, 1.2, 8.6, 11.0, "black")
    k.box(-3.7, -2.9, 0.5, 1.5, 8.2, 8.6, "glow")
    finish_toon("addon_armour", 16.0)


def addon_walkers():
    """Walkers branch: a tall assembly crane at the corner, a lit lift cage and a hanging leg frame."""
    k.begin(0.0)
    plate(-4.5, 4.5, -4.5, 4.5, 0.0, 0.6, "mid", inset=0.2)
    for sa in (1, -1):
        for sx in (1, -1):
            k.box(sa * 3.2 - 0.4, sa * 3.2 + 0.4, sx * 3.2 - 0.4, sx * 3.2 + 0.4, 0.6, 15.0, "steel")
    for z in (4.0, 8.0, 12.0):
        k.box(-3.6, 3.6, -3.6, 3.6, z, z + 0.4, "plate")
        for sx in (1, -1):
            tube_between((-3.2, sx * 3.2, z - 3.6), (3.2, sx * 3.2, z), 0.18, "dark", 6)
    k.box(-3.8, 3.8, -3.8, 3.8, 15.0, 15.8, "plate")
    k.box(-2.0, 2.0, -2.0, 2.0, 6.0, 9.0, "glass_lit")                                    # the lift cage, lit
    k.box(-1.6, 1.6, -1.6, 1.6, 9.0, 9.2, "dark")
    k.box(-1.0, 13.0, -0.6, 0.6, 15.8, 16.8, "paint")                                     # the jib out front
    k.box(10.5, 11.3, -0.25, 0.25, 11.0, 15.8, "black")
    for s in (1, -1):                                                                     # a leg frame on the hook
        tube_between((10.9, s * 0.8, 11.0), (10.9, s * 1.6, 8.0), 0.3, "steel", 6)
        tube_between((10.9, s * 1.6, 8.0), (10.9, s * 1.2, 5.6), 0.25, "plate", 6)
    k.box(11.4, 11.8, -0.4, 0.4, 13.6, 14.2, "glow")
    k.box(-3.0, -2.2, -0.4, 0.4, 15.8, 17.5, "dark")
    k.box(-3.1, -2.1, -0.5, 0.5, 17.5, 18.0, "glow")
    finish_toon("addon_walkers", 28.0)


def addon_guns():
    """Guns branch (concept 14): a twin-barrelled battery on a turning mount, an ammunition drum each side."""
    k.begin(0.0)
    plate(-4.5, 4.5, -5.0, 5.0, 0.0, 1.2, "plate", inset=0.4)
    k.drum(0, 0, 4.0, 1.2, 2.2, "mid", 12, r1=3.6)
    plate(-3.8, 3.0, -3.4, 3.4, 2.2, 5.2, "steel", inset=0.5, front=1.4)
    k.box(2.0, 2.6, -2.6, 2.6, 3.0, 4.6, "dark")
    for s in (1, -1):
        hlathe(s * 1.4, 3.8, [(0.75, 1.0), (0.75, 9.0), (0.95, 9.2), (0.95, 10.6), (0.55, 10.8)], "dark", 14)
        k.box(9.0, 9.4, s * 1.4 - 1.0, s * 1.4 + 1.0, 3.0, 4.6, "paint")
        wlathe(-0.8, 3.0, [(1.5, s * 4.0), (1.5, s * 5.6)], "steel", 14)
        k.box(-1.2, -0.4, *sorted((s * 4.2, s * 5.4)), 4.4, 4.8, "glow")
    k.box(-3.0, -1.5, -0.6, 0.6, 5.2, 6.0, "glow")
    finish_toon("addon_guns", 22.0)


def addon_air():
    """Air branch (concepts 17/20): a round landing deck on struts, ringed with lights, two props parked on it and
    a little control cab."""
    k.begin(0.0)
    for i in range(6):
        th = i * math.tau / 6
        tube_between((math.cos(th) * 4.6, math.sin(th) * 4.6, 0.0), (math.cos(th) * 7.5, math.sin(th) * 7.5, 3.0), 0.35, "dark", 6)
    k.drum(0, 0, 9.6, 3.0, 3.4, "mid", 24, r1=9.4)
    k.drum(0, 0, 9.4, 3.4, 3.8, "steel", 24, r1=9.0)
    for i in range(16):
        th = i * math.tau / 16
        a, x = math.cos(th) * 9.0, math.sin(th) * 9.0
        k.box(a - 0.3, a + 0.3, x - 0.3, x + 0.3, 3.8, 4.1, "glow" if i % 2 else "paint")
    for s in (1, -1):                                                                     # two parked props
        a, x = s * 4.5, s * 2.0
        k.drum(a, x, 1.4, 3.8, 4.4, "dark", 10)
        k.box(a - 0.25, a + 0.25, x - 2.4, x + 2.4, 4.4, 4.6, "black")
        k.box(a - 2.4, a + 2.4, x - 0.25, x + 0.25, 4.4, 4.6, "black")
        k.box(a - 0.4, a + 0.4, x - 0.4, x + 0.4, 4.6, 4.9, "glow")
    plate(-2.0, 2.0, -7.5, -4.5, 3.8, 6.2, "plate", inset=0.3)
    k.box(1.8, 2.1, -7.0, -5.0, 4.6, 5.6, "glass_lit")
    finish_toon("addon_air", 22.0)


def addon_support():
    """Support branch (concepts 21/24): a green-lit dish on a short tower, and a drone pad beside it with two little
    repair drones."""
    k.begin(0.0)
    plate(-4.5, 4.5, -4.5, 4.5, 0.0, 0.6, "mid", inset=0.2)
    plate(-3.0, 3.0, -3.0, 3.0, 0.6, 6.5, "steel", inset=0.6)
    k.drum(0, 0, 1.0, 6.5, 8.5, "dark", 8)
    with part("spin_dish", (0, 0, 8.5)):
        k.lathe(0, 0, [(0.6, 8.5), (2.5, 9.2), (4.5, 10.4), (5.2, 11.4), (4.9, 11.6), (2.4, 10.4), (0.6, 9.8)], "plate", 24)
        k.drum(0, 0, 2.0, 10.0, 10.4, "green", 16, r1=1.5)
        k.drum(0, 0, 0.25, 10.4, 13.0, "dark", 6)
        k.drum(0, 0, 0.6, 13.0, 13.6, "green", 8)
    k.drum(5.5, 4.0, 3.2, 0.0, 1.4, "plate", 12, r1=3.0)                                 # the drone pad
    k.drum(5.5, 4.0, 2.4, 1.4, 1.5, "green", 16)
    for a, x, z in ((4.6, 3.2, 3.0), (6.6, 5.0, 4.2)):                                    # two drones over it
        k.drum(a, x, 0.6, z, z + 0.5, "dark", 8)
        k.box(a - 1.2, a + 1.2, x - 0.12, x + 0.12, z + 0.5, z + 0.6, "black")
        k.box(a - 0.12, a + 0.12, x - 1.2, x + 1.2, z + 0.5, z + 0.6, "black")
        k.box(a - 0.25, a + 0.25, x - 0.25, x + 0.25, z - 0.2, z, "green")
    finish_toon("addon_support", 18.0)


def addon_core():
    """Core branch: tooling works at the back corner, three smokestacks with hot bands, a press hall."""
    k.begin(0.0)
    plate(-5.0, 5.0, -5.0, 5.0, 0.0, 0.6, "mid", inset=0.2)
    plate(-4.5, 4.0, -4.5, 2.0, 0.6, 5.5, "plate", inset=0.4)
    for i in range(3):
        k.box(3.95, 4.05, -3.8 + i * 1.9, -2.6 + i * 1.9, 2.0, 4.0, "glow")
    for j, (a, x, h) in enumerate(((-2.5, 3.2, 14.0), (0.0, 3.6, 12.0), (2.5, 3.2, 10.0))):
        k.drum(a, x, 1.1, 0.6, h, "steel", 10, r1=0.9)
        k.drum(a, x, 1.15, h * 0.7, h * 0.7 + 0.6, "glow", 10)
        k.drum(a, x, 1.15, h - 1.2, h - 0.6, "paint", 10)
        k.drum(a, x, 1.0, h, h + 0.4, "black", 10)
    finish_toon("addon_core", 20.0)


globals()[sys.argv[sys.argv.index("--") + 1]]()
