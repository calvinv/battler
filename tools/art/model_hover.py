"""Simple futuristic units in the cel shaded look: model_hover.py -- <name>
(The user, 2026-10-23: "the shell shaded look we used in void wake ... simple but futuristic builds"; the light tank from
FLUX concept art/_gen/concepts/hover2_slate_0_s301.png, "the barrel needs a bit of work".)
Flat colours, few big chamfered shapes, no weathering: the game draws them with toon light bands and ink outlines
(game/toon_unit.gdshader). Moving parts are tagged for the rig (<name>_rig.glb, game/UnitRig.cs).
Names: hovertank mech
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


globals()[sys.argv[sys.argv.index("--") + 1]]()
