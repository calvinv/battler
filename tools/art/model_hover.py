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
            k.solid([(a + c * r - 0.22 * d, xa, z + d * r + 0.22 * c), (a + c * r + 0.22 * d, xa, z + d * r - 0.22 * c),
                     (a + cc * r + 0.22 * dd, xb, z + dd * r - 0.22 * cc), (a + cc * r - 0.22 * dd, xb, z + dd * r + 0.22 * cc)],
                    [(a + c * (r + 0.28) - 0.18 * d, xa, z + d * (r + 0.28) + 0.18 * c), (a + c * (r + 0.28) + 0.18 * d, xa, z + d * (r + 0.28) - 0.18 * c),
                     (a + cc * (r + 0.28) + 0.18 * dd, xb, z + dd * (r + 0.28) - 0.18 * cc), (a + cc * (r + 0.28) - 0.18 * dd, xb, z + dd * (r + 0.28) + 0.18 * cc)], "black")
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
    # The big round cannon out of the front, low on the drum: a round mantlet, a fat barrel, a team band, a muzzle.
    gz = zc - 0.35
    with part("barrel", (1.6, 0, gz)):
        hlathe(0, gz, [(1.15, 1.2), (1.25, 1.5), (1.25, 2.2), (1.0, 2.45), (0.72, 2.5)], "plate", 32)
        hlathe(0, gz, [(0.62, 2.45), (0.62, 5.6)], "dark", 28)
        hlathe(0, gz, [(0.7, 4.1), (0.7, 4.45)], "paint", 28)
        hlathe(0, gz, [(0.82, 5.5), (0.82, 6.2), (0.72, 6.3)], "mid", 28)
        hlathe(0, gz, [(0.45, 6.28), (0.45, 6.32)], "black", 20)
    mark("muzzle", (6.4, 0, gz), "barrel")
    finish_toon("battlewheel", 20.0)


globals()[sys.argv[sys.argv.index("--") + 1]]()
