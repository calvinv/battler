"""Simple futuristic units in the cel shaded look: model_hover.py -- <name>
(The user, 2026-10-23: "the shell shaded look we used in void wake ... simple but futuristic builds"; the light tank from
FLUX concept art/_gen/concepts/hover2_slate_0_s301.png, "the barrel needs a bit of work".)
Flat colours, few big chamfered shapes, no weathering: the game draws them with toon light bands and ink outlines
(game/toon_unit.gdshader). Moving parts are tagged for the rig (<name>_rig.glb, game/UnitRig.cs).
Names: hovertank mech battlewheel
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


def mech():
    """Small rifle mech (FLUX concept art/_gen/concepts/mech2_rifle_s501.png, the user's pick 2026-10-23: "9"): a boxy
    head-like body with two big round glowing eyes, team stripes on its top and flanks, carried high on two long
    backward-bending bird legs set at its sides (so they show from the game's camera: thigh, shin, three splayed toes
    and a spur, team-painted toe caps), a long rifle carried in front in two arms. Each leg is three parts (thigh_L/R, shin_L/R,
    foot_L/R) the game walks; the rifle kicks."""
    k.begin(0.0)
    z0, z1 = 4.5, 7.0                                                                       # the body's foot and top
    # The body: a slightly tapered box, a lid panel, team stripes, a vent on one flank, a pack on its back.
    plate(-1.35, 1.35, -1.45, 1.45, z0, z1, "steel", inset=0.12)
    k.box(-0.9, 0.8, -1.0, 1.0, z1, z1 + 0.1, "plate")
    for a0 in (-1.15, 0.25):
        k.box(a0, a0 + 0.8, -1.25, -0.75, z1, z1 + 0.08, "paint")
    for s_ in (1, -1):
        for zz in (z0 + 1.3, z0 + 1.8):
            k.box(-1.0, 0.8, *xs(s_, 1.43, 1.5), zz, zz + 0.28, "paint")
    k.box(-1.0, 0.2, 1.43, 1.5, z0 + 0.5, z0 + 0.9, "black")                                # a vent on the left flank
    k.box(-1.7, -1.35, -0.9, 0.9, z0 + 0.6, z0 + 2.0, "plate")
    k.box(-1.73, -1.68, -0.55, 0.55, z0 + 0.9, z0 + 1.7, "dark")
    # The eyes: dark rims, glowing lenses, a bright glint each.
    ez = z0 + 1.5
    for x in (-0.68, 0.68):
        k.tube(1.28, 1.48, x, ez, 0.56, "dark", 24)
        k.tube(1.36, 1.54, x, ez, 0.43, "glow", 24)
        k.tube(1.54, 1.56, x - 0.13, ez + 0.15, 0.12, "white", 10)
    k.box(1.3, 1.4, -1.3, 1.3, z0 + 0.45, z0 + 0.58, "dark")                                # a seam under them
    # Hip block under the body, the hip joints standing out at its sides.
    hz = z0 - 0.35
    k.box(-0.8, 0.6, -1.1, 1.1, hz - 0.35, z0, "dark")
    for s in (1, -1):
        wlathe(0, hz, [(0.5, s * 1.05), (0.5, s * 1.75)], "plate", 20)
    # The legs, at the body's sides: hip -> a joint bending backward -> ankle -> three toes and a spur.
    for side, s in (("L", 1), ("R", -1)):
        x = s * 1.65
        hip, knee, ankle = (0.0, x, hz), (-1.0, x, 2.4), (0.2, x, 0.45)
        with part("thigh_" + side, hip):
            tube_between(hip, knee, 0.32, "dark", 12)
            k.box(-0.75, -0.05, x - 0.22, x + 0.22, 2.95, 3.75, "plate")                    # a thigh plate
            wlathe(knee[0], knee[2], [(0.38, x - 0.27), (0.38, x + 0.27)], "plate", 18)
            with part("shin_" + side, knee, parent="thigh_" + side):
                tube_between(knee, ankle, 0.23, "dark", 12)
                tube_between((-0.8, x, 2.1), (-0.05, x, 1.0), 0.1, "mid", 8)                 # a piston
                wlathe(ankle[0], ankle[2], [(0.25, x - 0.2), (0.25, x + 0.2)], "dark", 14)
                with part("foot_" + side, ankle, parent="shin_" + side):
                    for turn in (-0.6, 0.0, 0.6):
                        tip = (ankle[0] + math.cos(turn) * 1.05, x + math.sin(turn) * 1.05, 0.14)
                        tube_between(ankle, tip, 0.17, "dark", 8)
                        tube_between(tip, (tip[0] + math.cos(turn) * 0.34, tip[1] + math.sin(turn) * 0.34, 0.12), 0.21, "paint", 8, 0.12)
                    tube_between(ankle, (ankle[0] - 0.6, x, 0.16), 0.13, "dark", 8)
    # Two arms holding the rifle, modelled at the aim (pointing ahead under the eyes): shoulder joints on the lower
    # front corners, upper arms (arm_L/R, turning at the shoulder) down to the elbows, forearms in to the hands, one
    # on the grip and one on the fore-end. The rifle, forearms and hands are one part (barrel) that the game swings
    # between carried across the body and aimed; the upper arms follow its elbows (marks elbow_L/R).
    rz, rx = z0 - 0.35, -0.35
    sh = {1: (0.7, 1.55, z0 + 0.55), -1: (0.7, -1.55, z0 + 0.55)}
    el = {1: (1.25, 1.35, z0 - 0.55), -1: (0.65, -1.35, z0 - 0.75)}
    hand = {1: (2.35, rx + 0.3, rz), -1: (0.75, rx - 0.3, rz - 0.15)}
    for s_ in (1, -1):
        wlathe(sh[s_][0], sh[s_][2], [(0.45, s_ * 1.4), (0.45, s_ * 1.8)], "plate", 16)       # shoulder joint
        with part("arm_" + ("L" if s_ > 0 else "R"), sh[s_]):
            tube_between(sh[s_], el[s_], 0.27, "steel", 12)                                  # upper arm
        k.box(sh[s_][0] - 0.25, sh[s_][0] + 0.25, *xs(s_, 1.62, 1.8), z0 - 0.1, z0 + 0.45, "plate")   # arm plate
    with part("barrel", (1.5, rx, rz)):
        k.box(-1.6, -0.4, rx - 0.2, rx + 0.2, rz - 0.55, rz + 0.12, "dark")                 # stock
        k.box(-0.4, 1.6, rx - 0.28, rx + 0.28, rz - 0.32, rz + 0.3, "mid")                  # receiver
        k.box(0.95, 1.45, rx - 0.18, rx + 0.18, rz - 0.95, rz - 0.32, "dark")              # magazine
        k.tube(0.0, 1.2, rx, rz + 0.5, 0.15, "dark", 12)                                    # scope
        k.tube(1.15, 1.25, rx, rz + 0.5, 0.11, "glow", 10)
        k.box(1.6, 2.9, rx - 0.22, rx + 0.22, rz - 0.22, rz + 0.18, "dark")                 # fore-end
        k.tube(2.9, 5.0, rx, rz + 0.02, 0.17, "dark", 12)                                   # barrel
        k.tube(3.6, 3.9, rx, rz + 0.02, 0.21, "paint", 12)
        k.tube(4.9, 5.4, rx, rz + 0.02, 0.25, "mid", 12)                                    # muzzle brake
        for s_ in (1, -1):
            wlathe(el[s_][0], el[s_][2], [(0.3, el[s_][1] - 0.22), (0.3, el[s_][1] + 0.22)], "plate", 12)     # elbow
            tube_between(el[s_], hand[s_], 0.23, "steel", 12)                                # forearm
            h = hand[s_]
            k.box(h[0] - 0.3, h[0] + 0.3, h[1] - 0.32, h[1] + 0.32, h[2] - 0.38, h[2] + 0.34, "plate")    # hand round the gun
    mark("muzzle", (5.5, rx, rz + 0.02), "barrel")
    mark("elbow_L", el[1], "barrel")
    mark("elbow_R", el[-1], "barrel")
    finish_toon("mech", 16.0)


def tyre(a, s, z, r, inner, outer, lugs=22):
    """A big toon tyre on side s: a black ring with a rounded tread and chunky lugs across it, an open rim, a slate
    hub disc with a bolt circle and team marks on the outer face."""
    x0, x1 = xs(s, inner, outer)
    g = max(0.5, min(1.0, r / 3.0))                                 # lugs in proportion on a small tyre
    out = 1 if s > 0 else -1
    face = x1 if s > 0 else x0
    w = x1 - x0
    wlathe(a, z, [(r * 0.66, x0), (r * 0.9, x0), (r, x0 + w * 0.15), (r, x1 - w * 0.15), (r * 0.9, x1),
                  (r * 0.66, x1), (r * 0.66, x0)], "black", 40, caps=False)
    for i in range(lugs):
        t = i * math.tau / lugs
        c, d = math.cos(t), math.sin(t)
        for xa, xb in ((x0 + 0.08, x0 + w * 0.47), (x0 + w * 0.53, x1 - 0.08)):
            off = 0.12 if xa < x0 + 0.1 else -0.12                  # (staggered: a chevron tread)
            cc, dd = math.cos(t + off), math.sin(t + off)
            k.solid([(a + c * r - 0.22 * g * d, xa, z + d * r + 0.22 * g * c), (a + c * r + 0.22 * g * d, xa, z + d * r - 0.22 * g * c),
                     (a + cc * r + 0.22 * g * dd, xb, z + dd * r - 0.22 * g * cc), (a + cc * r - 0.22 * g * dd, xb, z + dd * r + 0.22 * g * cc)],
                    [(a + c * (r + 0.28 * g) - 0.18 * g * d, xa, z + d * (r + 0.28 * g) + 0.18 * g * c), (a + c * (r + 0.28 * g) + 0.18 * g * d, xa, z + d * (r + 0.28 * g) - 0.18 * g * c),
                     (a + cc * (r + 0.28 * g) + 0.18 * g * dd, xb, z + dd * (r + 0.28 * g) - 0.18 * g * cc), (a + cc * (r + 0.28 * g) - 0.18 * g * dd, xb, z + dd * (r + 0.28 * g) + 0.18 * g * cc)], "black")
    wlathe(a, z, [(r * 0.66, face - out * 0.6), (r * 0.66, face - out * 0.1), (r * 0.6, face), (r * 0.36, face),
                  (r * 0.3, face - out * 0.2)], "plate", 40)
    wlathe(a, z, [(r * 0.26, face - out * 0.2), (r * 0.26, face + out * 0.15), (r * 0.17, face + out * 0.28)], "mid", 24)
    for i in range(8):
        t = i * math.tau / 8
        p = (a + math.cos(t) * r * 0.21, face + out * 0.13, z + math.sin(t) * r * 0.21)
        tube_between(p, (p[0], p[1] + out * 0.1, p[2]), 0.06, "dark", 6)
    for t in (0.4, 0.4 + math.pi):
        p = (a + math.cos(t) * r * 0.5, face + out * 0.02, z + math.sin(t) * r * 0.5)
        tube_between(p, (p[0], p[1] + out * 0.05, p[2]), 0.18, "paint", 8)


def battlewheel():
    """Battle wheel (FLUX concept art/_gen/concepts/wheel2_words2_s701.png, "3"; the user, 2026-10-23: "2 large tyres
    and big round cannon in the middle"): two big lugged tyres on one axle, a round drum body slung between them,
    a big round cannon out of its front with the amber eye above it, an armoured cap with team stripes on top. The
    tyres roll (wheel_0/1); the body rocks on the axle; the cannon kicks."""
    k.begin(0.0)
    R, zc = 3.0, 3.05                                                                       # tyre radius, axle height
    for i, s in enumerate((1, -1)):
        with part("wheel_%d" % i, (0, s * 2.65, zc), parent="root"):
            tyre(0, s, zc, R, 1.95, 3.35)
    wlathe(0, zc, [(0.42, -1.95), (0.42, 1.95)], "dark", 16)                                 # the axle
    # The drum body (hull) turns on the axle: the game rocks it back so the cannon points up while driving and
    # tips it level to aim.
    with part("hull", (0, 0, zc)):
        # The drum body between the tyres, its ends capped, a seam ring, a vent.
        wlathe(0, zc + 0.25, [(1.6, -1.85), (2.05, -1.7), (2.15, -1.3), (2.15, 1.3), (2.05, 1.7), (1.6, 1.85)], "steel", 40)
        for x in (-1.0, 1.0):
            wlathe(0, zc + 0.25, [(2.2, x - 0.07), (2.2, x + 0.07)], "dark", 40)
        k.box(-2.25, -2.05, -0.8, 0.8, zc - 0.3, zc + 0.7, "dark")                               # a rear vent
        for i in range(4):
            k.box(-2.3, -2.2, -0.7, 0.7, zc - 0.2 + i * 0.25, zc - 0.1 + i * 0.25, "black")
        # The armoured cap on top with team stripes and a small sensor box.
        top = zc + 0.25 + 2.1
        plate(-1.3, 1.1, -1.25, 1.25, top - 0.3, top + 0.55, "plate", inset=0.2, front=0.35)
        for x in (-0.75, -0.3, 0.3, 0.75):
            k.box(-0.9, 0.6, x - 0.13, x + 0.13, top + 0.55, top + 0.6, "paint")
        k.box(-1.0, -0.5, 0.35, 0.85, top + 0.55, top + 0.85, "mid")
        k.box(-0.52, -0.49, 0.45, 0.75, top + 0.62, top + 0.78, "glow")
        # The amber eye on the drum's front, above the cannon, in an octagonal frame.
        ez = zc + 1.35
        k.tube(1.6, 2.15, 0, ez, 0.62, "dark", 8)
        k.tube(1.9, 2.25, 0, ez, 0.45, "glow", 24)
        k.tube(2.25, 2.27, -0.12, ez + 0.14, 0.12, "white", 10)
        k.box(1.95, 2.2, -0.9, -0.7, ez - 0.3, ez + 0.3, "paint")
        k.box(1.95, 2.2, 0.7, 0.9, ez - 0.3, ez + 0.3, "paint")
        # The big round cannon out of the front, low on the drum: a round mantlet, a short wide barrel, a team band, a muzzle.
        gz = zc - 0.35
        with part("barrel", (1.6, 0, gz), parent="hull"):
            hlathe(0, gz, [(1.35, 1.2), (1.5, 1.5), (1.5, 2.3), (1.25, 2.55), (1.0, 2.6)], "plate", 32)
            hlathe(0, gz, [(0.95, 2.55), (0.95, 4.3)], "dark", 32)
            hlathe(0, gz, [(1.03, 3.2), (1.03, 3.55)], "paint", 32)
            hlathe(0, gz, [(1.15, 4.2), (1.15, 4.85), (1.02, 4.95)], "mid", 32)
            hlathe(0, gz, [(0.7, 4.93), (0.7, 4.97)], "black", 24)
        mark("muzzle", (5.05, 0, gz), "barrel")
    finish_toon("battlewheel", 20.0)


def longleg():
    """Long Leg artillery tripod (FLUX concept art/_gen/concepts/unit_longleg_2_s901.png, "3"): a chamfered box head
    with a team octagon on its lid, a big lens on one cheek and a long cannon out of a slot in its face, on a hub
    held high by three long legs (two either side of the gun, one behind), each a thick plated thigh out and down
    to the knee with an amber panel, a slimmer shin down to a round pad. Legs are tleg_0..2 (thigh, turning at the
    hip) and tshin_0..2 (at the knee); the head turns (turret) on the body, which dips as it steps; the cannon kicks."""
    k.begin(0.0)
    hz = 7.4                                                                                # hip height
    # The hub the legs hang from (the body: it dips as it steps), a round foot under it.
    hexagon(0, 0, 1.7, hz - 0.9, hz + 0.3, "dark", r1=1.9, sides=6)
    hexagon(0, 0, 0.6, hz - 1.6, hz - 0.9, "dark", r1=0.75, sides=8)
    tz = hz + 0.3
    with part("turret", (0, 0, tz)):
        # The head: a chamfered box, a lid panel with a team octagon, two lid ribs, a rear pack.
        plate(-2.4, 2.4, -2.2, 2.2, tz, tz + 3.4, "steel", inset=0.35, front=0.45, back=0.35)
        k.box(-1.6, 1.4, -1.5, 1.5, tz + 3.4, tz + 3.5, "plate")
        hexagon(-0.2, 0, 1.0, tz + 3.5, tz + 3.56, "paint", sides=8, turn=math.pi / 8)
        hexagon(-0.2, 0, 0.82, tz + 3.5, tz + 3.6, "plate", sides=8, turn=math.pi / 8)
        for x in (-1.75, 1.75):
            k.box(-1.0, 0.6, x - 0.22, x + 0.22, tz + 3.3, tz + 3.62, "mid")
        plate(-2.9, -2.35, -1.5, 1.5, tz + 0.6, tz + 2.8, "plate", inset=0.15)
        # A team band round the head and slanted stripes by the lens.
        for s in (1, -1):
            k.box(-2.2, 2.2, *xs(s, 2.17, 2.24), tz + 1.25, tz + 1.55, "paint")
        for i in range(3):
            a0 = -1.9 + i * 0.42
            k.box(a0, a0 + 0.22, 2.17, 2.24, tz + 2.2, tz + 3.0, "paint")
        # The lens on its left cheek: a dark rim, a glowing eye, a glint.
        wlathe(0.9, tz + 2.2, [(0.85, 2.15), (0.85, 2.45), (0.7, 2.55)], "dark", 24)
        wlathe(0.9, tz + 2.2, [(0.55, 2.4), (0.55, 2.6)], "glow", 24)
        wlathe(0.7, tz + 2.4, [(0.15, 2.6), (0.15, 2.63)], "white", 10)
        # The gun slot in the face, a mantlet in it, the long barrel.
        k.box(2.35, 2.5, -1.0, 1.0, tz + 1.0, tz + 2.8, "black")
        gz = tz + 1.9
        with part("barrel", (2.3, 0, gz), parent="turret"):
            hlathe(0, gz, [(0.75, 1.8), (0.75, 3.0), (0.6, 3.2)], "mid", 24)
            hlathe(0, gz, [(0.42, 3.1), (0.42, 4.0), (0.3, 4.2), (0.3, 9.6)], "dark", 20)
            hlathe(0, gz, [(0.36, 6.4), (0.36, 6.8)], "paint", 20)
            hlathe(0, gz, [(0.42, 9.5), (0.42, 10.3), (0.36, 10.4)], "dark", 20)
            hlathe(0, gz, [(0.22, 10.38), (0.22, 10.42)], "black", 16)
        mark("muzzle", (10.5, 0, gz), "barrel")
    # The legs: two either side of the gun, one behind. Thigh out and down to the knee, shin down to a pad.
    for i, turn in enumerate((math.radians(55), math.radians(-55), math.pi)):
        c, s = math.cos(turn), math.sin(turn)
        at = lambda r, z: (c * r, s * r, z)
        hip, knee, foot = at(1.5, hz - 0.3), at(4.6, 5.0), at(6.0, 0.55)
        with part("tleg_%d" % i, hip, parent="root"):
            tube_between(hip, knee, 0.75, "plate", 6)
            # An amber light panel on the thigh's outer face.
            mid = tuple(hip[j] + (knee[j] - hip[j]) * 0.6 for j in range(3))
            tube_between((mid[0] + c * 0.55, mid[1] + s * 0.55, mid[2] + 0.45), (mid[0] + c * 0.75, mid[1] + s * 0.75, mid[2] + 0.55), 0.28, "glow", 4)
            tube_between(at(1.2, hz - 0.3), at(1.9, hz - 0.3), 0.85, "dark", 8)               # hip joint
            with part("tshin_%d" % i, knee, parent="tleg_%d" % i):
                tube_between(knee, foot, 0.5, "steel", 6)
                tube_between(at(4.6, 5.6), at(4.6, 4.4), 0.7, "dark", 8)                      # knee joint
                low = tuple(knee[j] + (foot[j] - knee[j]) * 0.45 for j in range(3))
                tube_between((low[0] + c * 0.4, low[1] + s * 0.4, low[2]), (low[0] + c * 0.55, low[1] + s * 0.55, low[2]), 0.2, "paint", 4)
                tube_between((foot[0], foot[1], 0.0), (foot[0], foot[1], 0.55), 0.75, "dark", 10)   # pad
    finish_toon("longleg", 24.0)


def lobber():
    """Lobber mortar carrier (FLUX concept art/_gen/concepts/unit_lobber_2_s901.png, "6"): a squat faceted shell body on
    four fat wheels on stub arms, each under a mudguard with a team tab; a stubby mortar tube angled steeply up out
    of a hatch on its back half, an ammunition drum on its right flank, team chevrons over the top, amber lamps.
    Wheels roll (wheel_0..3, on the ground); the body rides its springs; the mortar mount turns (turret) and the
    tube (barrel) kicks."""
    k.begin(0.0)
    R, zw = 1.25, 1.25
    for i, (a, s) in enumerate(((2.0, 1), (2.0, -1), (-2.0, 1), (-2.0, -1))):
        with part("wheel_%d" % i, (a, s * 2.55, zw), parent="root"):
            tyre(a, s, zw, R, 2.0, 3.1, lugs=14)
    for a in (2.0, -2.0):
        wlathe(a, zw, [(0.3, -2.0), (0.3, 2.0)], "dark", 12)                                  # axles
    zb = 1.5
    # The shell body: a wide chamfered block, a faceted upper shell, a front face with two lamps.
    plate(-2.6, 2.6, -1.85, 1.85, zb, zb + 1.8, "steel", inset=0.3, front=0.55, back=0.45)
    plate(-2.1, 2.0, -1.55, 1.55, zb + 1.8, zb + 2.7, "plate", inset=0.45, front=0.6, back=0.4)
    for x in (-1.15, 1.15):
        k.tube(2.25, 2.55, x, zb + 0.75, 0.3, "dark", 16)
        k.tube(2.45, 2.62, x, zb + 0.75, 0.2, "glow", 16)
    k.box(2.3, 2.5, -0.6, 0.6, zb + 0.25, zb + 0.5, "black")                                # a grille
    # Mudguards over the wheels with team tabs.
    for a in (2.0, -2.0):
        for s in (1, -1):
            plate(a - 1.35, a + 1.35, *xs(s, 1.95, 3.2), zw + R + 0.1, zw + R + 0.45, "plate", inset=0.12, front=0.35, back=0.35)
            k.box(a + 0.2, a + 0.9, *xs(s, 2.3, 2.85), zw + R + 0.45, zw + R + 0.5, "paint")
            k.box(a - 0.3, a + 0.3, *xs(s, 1.55, 2.0), zw - 0.25, zw + 0.25, "dark")            # stub arm
    # Team chevrons over the back of the shell; a vent on the left flank.
    for a0 in (-1.4, -0.85):
        k.box(a0, a0 + 0.32, -1.5, 1.5, zb + 2.7, zb + 2.76, "paint")
    k.box(0.0, 0.7, 1.84, 1.9, zb + 0.8, zb + 1.4, "dark")
    # The ammunition drum on the right flank, lying along the body.
    hlathe(-1.95, zb + 1.35, [(0.75, -2.7), (0.85, -2.5), (0.85, -0.2), (0.75, 0.0)], "mid", 24)
    for a0 in (-2.1, -0.8):
        hlathe(-1.95, zb + 1.35, [(0.9, a0), (0.9, a0 + 0.25)], "paint", 24)
    # The mortar: a round hatch mount on the back half of the shell, a short fat tube angled steeply forward.
    mz = zb + 2.7
    with part("turret", (-0.5, 0, mz)):
        hexagon(-0.5, 0, 1.05, mz, mz + 0.35, "dark", r1=0.95, sides=10)
        k.box(-1.25, -0.95, -0.25, 0.25, mz + 0.35, mz + 0.55, "glow")
        pitch = math.radians(55)
        d = (math.cos(pitch), 0, math.sin(pitch))
        base = (-0.5, 0, mz + 0.5)
        at = lambda t: tuple(base[j] + d[j] * t for j in range(3))
        with part("barrel", base, parent="turret"):
            tube_between(at(-0.6), at(0.9), 0.7, "mid", 16)                                   # breech collar
            tube_between(at(0.9), at(4.0), 0.48, "dark", 16)                                  # tube
            tube_between(at(2.2), at(2.55), 0.53, "paint", 16)
            tube_between(at(3.8), at(4.5), 0.6, "dark", 16)                                   # muzzle ring
            tube_between(at(4.48), at(4.52), 0.38, "black", 12)
        mark("muzzle", at(4.6), "barrel")
    finish_toon("lobber", 16.0)


def skitter():
    """Skitter scout (FLUX concept art/_gen/concepts/unit_skitter_0_s901.png, "7"): a round ball body between two fat
    tyres, one big glassy cyan eye in a white rim, team stripes over its crown and a band round its waist with
    amber studs, a gun pod with an orange lens on its right cheek, a whip antenna with a ball tip. The ball turns to
    look at what it shoots (turret, the gun pod its barrel); the tyres roll (wheel_0/1)."""
    k.begin(0.0)
    R, zw = 1.35, 1.35
    for i, s in enumerate((1, -1)):
        with part("wheel_%d" % i, (0, s * 2.35, zw), parent="root"):
            tyre(0, s, zw, R, 1.85, 2.85, lugs=16)
    wlathe(0, zw, [(0.35, -1.9), (0.35, 1.9)], "dark", 12)
    zc, rb = 2.45, 1.8                                                                        # ball centre, radius
    ring = lambda n: [(rb * math.sin(t), rb * -math.cos(t)) for t in (math.pi * j / n for j in range(n + 1))]
    with part("turret", (0, 0, zc)):
        hlathe(0, zc, [(max(r, 0.01), a) for r, a in ring(14)], "steel", 28)
        # Team stripes over the crown (following the ball front to back), a waist band with amber studs.
        for x0, x1 in ((0.28, 0.62), (-0.62, -0.28)):
            wlathe(0, zc, [(math.sqrt(rb * rb - x * x) + 0.03, x) for x in (x0 + (x1 - x0) * j / 4 for j in range(5))], "paint", 40)
        rw = math.sqrt(rb * rb - 0.6 * 0.6) + 0.05
        hexagon(0, 0, rw, zc - 0.85, zc - 0.5, "paint", sides=32)
        for t in range(-50, 51, 25):
            ta = math.radians(t)
            p = (math.cos(ta) * rw, math.sin(ta) * rw, zc - 0.67)
            tube_between(p, (p[0] * 1.05, p[1] * 1.05, p[2]), 0.11, "glow", 8)
        # The eye: a white rim, a cyan lens, two glints.
        ez = zc + 0.35
        k.tube(1.35, 1.95, 0, ez, 0.82, "white", 28)
        k.tube(1.6, 2.02, 0, ez, 0.66, "glass_lit", 28)
        k.tube(2.02, 2.05, -0.22, ez + 0.28, 0.13, "white", 10)
        k.tube(2.02, 2.05, 0.1, ez + 0.36, 0.07, "white", 8)
        # The antenna, leaning back, a ball on its tip.
        top = (-0.6, 0.5, zc + rb - 0.15)
        tip = (-1.1, 0.75, zc + rb + 1.7)
        tube_between(top, (top[0], top[1], top[2] + 0.3), 0.22, "dark", 8)
        tube_between(top, tip, 0.06, "black", 6)
        hexagon(tip[0], tip[1], 0.17, tip[2], tip[2] + 0.3, "dark", sides=8)
        # The gun pod on its right cheek.
        gz, gx = zc + 0.1, -1.55
        with part("barrel", (0.2, gx, gz), parent="turret"):
            hlathe(gx, gz, [(0.3, -0.6), (0.55, -0.4), (0.6, 0.6), (0.5, 1.1)], "plate", 20)
            hlathe(gx, gz, [(0.38, 1.05), (0.38, 1.25)], "paint", 20)
            hlathe(gx, gz, [(0.24, 1.2), (0.24, 1.3)], "glow", 16)
        mark("muzzle", (1.4, gx, gz), "barrel")
    finish_toon("skitter", 12.0)


def blade(a, x, z, length, width, mat="mid"):
    """A two-bladed prop lying flat at (a, x, z) across x: tapered blades, a little twist (a raised leading edge)."""
    for s in (1, -1):
        r0, r1 = 0.35, length
        bot = [(a - width * 0.5, x + s * r0, z), (a + width * 0.5, x + s * r0, z), (a + width * 0.3, x + s * r1, z), (a - width * 0.3, x + s * r1, z)]
        top = [(p[0], p[1], p[2] + (0.14 if (p[0] - a) * s > 0 else 0.06)) for p in bot]
        k.solid(bot if s > 0 else bot[::-1], top if s > 0 else top[::-1], mat)


def dart():
    """Dart interceptor (FLUX concept art/_gen/concepts/unit_dart_0_s901.png, "10"): an armoured pod with a sharp
    wedge nose, a canopy sensor over it and team flashes on its shoulders; stub arms out the back to two open props
    (rotor_0/1, spun by the game), stubby wing pods out the front with amber lamps, twin guns under the nose."""
    k.begin(0.0)
    zb = 1.2
    # The pod: a chamfered body, a raised spine, the wedge nose.
    plate(-2.4, 1.6, -1.55, 1.55, zb, zb + 1.9, "steel", inset=0.4, front=0.3, back=0.55)
    plate(-2.0, 1.2, -0.75, 0.75, zb + 1.9, zb + 2.3, "plate", inset=0.2)
    k.solid([(1.6, -1.25, zb + 0.15), (1.6, 1.25, zb + 0.15), (3.7, 0.0, zb + 0.5)],
            [(1.6, -1.15, zb + 1.75), (1.6, 1.15, zb + 1.75), (3.7, 0.0, zb + 0.75)], "plate")
    tube_between((1.6, 0, zb + 1.8), (3.6, 0, zb + 0.72), 0.06, "white", 4)                  # a lit ridge down the nose
    # The canopy sensor over the nose: a dark frame, a glassy face.
    plate(0.4, 1.7, -0.65, 0.65, zb + 1.75, zb + 2.45, "dark", inset=0.12)
    k.box(1.55, 1.72, -0.45, 0.45, zb + 1.9, zb + 2.3, "glass_lit")
    # Team flashes on the shoulders.
    for s in (1, -1):
        k.solid([(0.9, s * 1.2, zb + 1.85), (0.2, s * 1.2, zb + 1.85), (-0.6, s * 1.45, zb + 0.9), (0.1, s * 1.45, zb + 0.9)],
                [(0.9, s * 1.27, zb + 1.85), (0.2, s * 1.27, zb + 1.85), (-0.6, s * 1.52, zb + 0.9), (0.1, s * 1.52, zb + 0.9)], "paint")
    # Twin guns under the nose: housings with amber lamps, short barrels.
    for s in (1, -1):
        plate(0.9, 2.0, *xs(s, 0.55, 1.35), zb - 0.35, zb + 0.35, "dark", inset=0.1)
        k.box(2.0, 2.06, *xs(s, 0.8, 1.1), zb - 0.15, zb + 0.15, "glow")
        k.tube(2.0, 2.7, s * 0.95, zb - 0.2, 0.09, "black", 8)
    mark("muzzle", (2.8, 0, zb - 0.2))
    # Stubby wing pods out the front, an amber lamp and a team tab on each.
    for s in (1, -1):
        plate(-0.4, 0.9, *xs(s, 1.5, 2.6), zb + 0.4, zb + 1.3, "plate", inset=0.15)
        plate(-0.3, 0.9, *xs(s, 2.6, 3.5), zb + 0.2, zb + 1.4, "steel", inset=0.15, front=0.25)
        k.box(0.9, 0.96, *xs(s, 2.8, 3.25), zb + 0.55, zb + 0.95, "glow")
        k.box(0.0, 0.5, *xs(s, 2.75, 3.35), zb + 1.4, zb + 1.45, "paint")
    # Arms out the back to the props: a nacelle each, the prop spinning on its hub.
    for i, s in enumerate((1, -1)):
        pa, px = -1.7, s * 3.4
        plate(-2.2, -1.2, *xs(s, 1.4, 2.9), zb + 0.7, zb + 1.4, "plate", inset=0.12)
        hexagon(pa, px, 0.75, zb + 0.4, zb + 1.7, "steel", r1=0.6, sides=8)
        with part("rotor_%d" % i, (pa, px, zb + 2.05)):
            hexagon(pa, px, 0.32, zb + 1.7, zb + 2.25, "dark", sides=8)
            blade(pa, px, zb + 1.95, 2.3, 0.55)
    finish_toon("dart", 14.0)


def hpatch(x, z, profile, t0, t1, mat, lift=0.05, depth=0.06, steps=8):
    """A raised patch on an hlathe surface (same (radius, a) profile) between angles t0 and t1 (radians about the
    length, 0 out to +x, up positive): a painted belt that follows the hull's curve, as one smooth closed piece."""
    import bmesh, bpy
    bm = bmesh.new()
    angs = [t0 + (t1 - t0) * j / steps for j in range(steps + 1)]
    def grid(off):
        return [[bm.verts.new(k.at(a, x + math.cos(t) * (r + off), z + math.sin(t) * (r + off))) for t in angs] for r, a in profile]
    out, inn = grid(lift), grid(lift - depth)
    n, m = len(profile), len(angs)
    for i in range(n - 1):
        for j in range(m - 1):
            bm.faces.new([out[i][j], out[i + 1][j], out[i + 1][j + 1], out[i][j + 1]]).smooth = True
            bm.faces.new([inn[i][j + 1], inn[i + 1][j + 1], inn[i + 1][j], inn[i][j]]).smooth = True
    for i in range(n - 1):
        for j in (0, m - 1):
            bm.faces.new([out[i][j], inn[i][j], inn[i + 1][j], out[i + 1][j]])
    for i in (0, n - 1):
        for j in range(m - 1):
            bm.faces.new([out[i][j], out[i][j + 1], inn[i][j + 1], inn[i][j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("piece")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("piece", me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(k.mats[mat])
    k.pieces.append(ob)
    return ob


def blimp():
    """Blimp bomber (FLUX concept art/_gen/concepts/unit_blimp_2_s901.png, "15"): a fat armoured egg of a hull lying
    along its flight, gore seams round it, a team belt down each flank with a row of green running lights over it,
    a jet duct at the tail and a short spike at the nose, a sensor hatch on top, a grey bomb-bay panel on each flank
    and the bay doors underneath, two stubby thrusters under the belly with team nozzles. (No moving parts: the game
    flies it level, bobbing.)"""
    k.begin(0.0)
    zc, L, Rm = 5.2, 7.0, 3.6                                                                # hull centre, half length, girth
    prof = []
    for j in range(25):
        a = -L + 2 * L * j / 24
        u = a / L
        r = Rm * math.sqrt(max(0.0, 1 - abs(u) ** 2.4)) * (1.0 - 0.06 * u)                  # a touch fuller at the back
        prof.append((r, a))
    prof[0] = (1.6, -L + 0.05)                                                               # open into the tail duct
    prof[-1] = (0.5, L - 0.02)
    hlathe(0, zc, prof, "steel", 40)
    # Gore seams round the hull.
    for a in (-4.2, -1.4, 1.4, 4.2):
        u = a / L
        r = Rm * math.sqrt(1 - abs(u) ** 2.4) * (1.0 - 0.06 * u)
        hlathe(0, zc, [(r + 0.04, a - 0.06), (r + 0.04, a + 0.06)], "dark", 40)
    # The team belt down each flank, running lights over it.
    belt = [(r, a) for r, a in prof[2:-1]]
    for s in (1, -1):
        lo, hi = math.radians(-18), math.radians(2)
        if s > 0:
            hpatch(0, zc, belt, lo, hi, "paint", lift=0.03)
        else:
            hpatch(0, zc, belt, math.pi - hi, math.pi - lo, "paint", lift=0.03)
        for a in (-4.8, -3.0, -1.2, 0.6, 2.4, 4.2):
            u = a / L
            r = Rm * math.sqrt(1 - abs(u) ** 2.4) * (1.0 - 0.06 * u)
            t = math.radians(12)
            p = (a, s * math.cos(t) * r, zc + math.sin(t) * r)
            tube_between(p, (a, s * math.cos(t) * (r + 0.1), p[2] + math.sin(t) * 0.1), 0.13, "glass_lit", 8)
        # The bomb-bay panel on the flank under the belt.
        t = math.radians(-34)
        r = Rm * 0.97
        cx, cz = s * math.cos(t) * r, zc + math.sin(t) * r
        k.box(-1.0, 1.8, *xs(s, abs(cx) - 0.3, abs(cx) + 0.12), cz - 0.55, cz + 0.55, "mid")
        k.box(-0.85, 0.35, *xs(s, abs(cx) + 0.1, abs(cx) + 0.16), cz - 0.4, cz + 0.4, "dark")
    # The tail duct and the nose spike.
    hlathe(0, zc, [(1.25, -L - 0.9), (1.75, -L - 0.7), (1.85, -L + 0.4), (1.5, -L + 0.6)], "plate", 32)
    hlathe(0, zc, [(1.25, -L - 0.85), (1.25, -L + 0.3)], "black", 24)
    hlathe(0, zc, [(0.75, -L - 0.2), (0.75, -L + 0.1)], "glow", 20)
    hlathe(0, zc, [(0.6, L - 0.3), (0.5, L + 0.4), (0.12, L + 1.3)], "dark", 16)
    # The sensor hatch on top.
    top = zc + Rm
    hexagon(0.6, 0, 1.25, top - 0.4, top + 0.45, "plate", r1=1.1, sides=12)
    k.box(1.6, 1.75, -0.4, 0.4, top - 0.05, top + 0.3, "glass_lit")
    for x in (-0.75, 0.75):
        k.tube(1.6, 1.82, x, top + 0.1, 0.14, "black", 10)
    # The bay doors underneath and two stubby thrusters with team nozzles.
    k.box(-1.5, 1.8, -1.0, 1.0, zc - Rm + 0.02, zc - Rm + 0.18, "dark")
    k.box(-1.5, 1.8, -0.04, 0.04, zc - Rm - 0.02, zc - Rm + 0.2, "black")
    for a in (-3.2, 3.0):
        hexagon(a, 0, 0.55, zc - Rm - 0.9, zc - Rm + 0.6, "dark", r1=0.45, sides=10)
        hexagon(a, 0, 0.55, zc - Rm - 1.5, zc - Rm - 0.9, "paint", r1=0.8, sides=10)
    finish_toon("blimp", 20.0)


globals()[sys.argv[sys.argv.index("--") + 1]]()
