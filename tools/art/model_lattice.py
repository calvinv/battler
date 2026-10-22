"""The Lattice's models: model_lattice.py -- <crawler|spider|piercer|drone|l_hq|l_tower|l_outpost|l_extractor|
factory_crawler|factory_spider|factory_piercer>
(The user, 2026-10-21: the swarm civilisation, "Lattice"; its tier 1 has "no artillery but scout fliers and anti
armour units"; models low-poly, "the simpler the better".) Salvage machines grown into one mind of many bodies: the
same handful-of-blocks approach as the Coalition's wedge style, but built different so they read apart from far off:
legs not tracks, low scuttling bodies, angled scrap plates, clusters of glowing amber eyes; rust and bone, the team
colour on the backs.
Run: blender --background --python tools/art/model_lattice.py -- crawler"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unit_parts import *

# Bone-white plates, rust-brown scrap, dark frame, black joints; amber eyes ("ember": the game makes it glow).
k.recolour(steel=(206, 196, 176), plate=(150, 84, 48), mid=(112, 98, 84), dark=(54, 48, 44), black=(28, 26, 24),
           ember=(255, 150, 40))


def leg(hip, knee, foot, r=0.32):
    """A thin jointed leg: thigh up to the knee, shin down to the ground, a dark knuckle at the knee."""
    tube_between(hip, knee, r, "dark", 4)
    tube_between(knee, foot, r * 0.8, "mid", 4, r * 0.45)
    k.drum(knee[0], knee[1], r * 1.4, knee[2] - r, knee[2] + r, "black", 6)


def eyes(a, x, z, n=3, r=0.22, spread=0.6):
    """A cluster of glowing eyes on a face at `a`."""
    for i in range(n):
        dx = (i - (n - 1) / 2) * spread
        dz = (i % 2) * r * 1.5
        k.tube(a - 0.1, a + 0.12, x + dx, z + dz, r, "ember", 6)


def legs_round(n, body_r, reach, hip_z, knee_z, r=0.32, front_bias=0.0):
    """`n` legs spread round a body, knees high, feet out wide."""
    for i in range(n):
        ang = math.tau * (i + 0.5) / n
        ca, sa = math.cos(ang) + front_bias, math.sin(ang)
        leg((ca * body_r, sa * body_r, hip_z), (ca * (body_r + reach * 0.5), sa * (body_r + reach * 0.5), knee_z),
            (ca * (body_r + reach), sa * (body_r + reach), 0.0), r)


# ---- units --------------------------------------------------------------------------------------------------------

def crawler():
    """Crawler: the swarm's body. A low flat six-legged beetle of scrap, team plate on its back, three eyes, a little
    claw-gun under its chin."""
    hexagon(0, 0, 2.6, 1.6, 2.6, "plate", r1=2.2, sides=6)
    hexagon(-0.3, 0, 1.9, 2.6, 2.95, "paint", r1=1.6, sides=6)
    k.box(1.6, 3.2, -1.0, 1.0, 1.7, 2.5, "steel", nose=0.7)                             # head plate
    eyes(3.2, 0.0, 2.15, 3, 0.2, 0.5)
    k.box(2.6, 4.6, -0.2, 0.2, 1.6, 1.9, "dark")                                        # claw-gun
    legs_round(6, 2.0, 2.6, 2.0, 3.4, 0.26)


def spider():
    """Spider: four long legs under a squat armoured body, a turret with a twin laser (the Lattice's skirmisher)."""
    plate(-2.6, 2.6, -2.2, 2.2, 3.2, 4.8, "steel", inset=0.35, front=0.8, back=0.5)
    plate(-1.7, 1.5, -1.4, 1.4, 4.8, 5.0, "paint", inset=0.12)
    eyes(2.62, 0.0, 3.8, 3, 0.2, 0.55)
    legs_round(4, 2.2, 3.6, 3.6, 5.6, 0.36)
    hexagon(-0.2, 0, 1.3, 5.0, 6.0, "plate", r1=1.0)
    for s in (1, -1):
        k.box(0.6, 5.0, s * 0.36 - 0.15, s * 0.36 + 0.15, 5.4, 5.7, "dark")
        k.box(4.8, 5.2, s * 0.36 - 0.18, s * 0.36 + 0.18, 5.37, 5.73, "ember")


def piercer():
    """Piercer: anti-armour. A long low body slung between four bent legs, a single very long spike-gun laid along its
    back and out past its head, bracing plates either side of it."""
    plate(-3.8, 3.6, -1.9, 1.9, 2.0, 3.4, "plate", inset=0.3, front=1.2, back=0.6)
    plate(-2.8, 1.8, -1.2, 1.2, 3.4, 3.6, "paint", inset=0.1)
    eyes(3.62, 0.0, 2.6, 2, 0.24, 0.7)
    for fa in (1, -1):
        for s in (1, -1):
            leg((fa * 2.4, s * 1.8, 2.6), (fa * 3.6, s * 3.8, 4.2), (fa * 4.4, s * 4.4, 0.0), 0.34)
    k.box(-3.0, 9.0, -0.28, 0.28, 3.7, 4.3, "dark")                                     # the spike-gun
    k.box(8.4, 9.8, -0.12, 0.12, 3.85, 4.15, "ember")                                   # its glowing tip
    for s in (1, -1):
        k.upright([(-2.4, 3.4), (1.6, 3.4), (0.6, 4.6), (-2.0, 4.6)], s * 0.3, s * 0.3 + s * 0.12, "steel")


def drone():
    """Drone: the Lattice's flying scout. A small body hung between four rotor rings glowing amber underneath, a
    sensor eye below. Floats: the game lifts it."""
    hexagon(0, 0, 1.4, 0.4, 1.4, "plate", r1=1.1, sides=6)
    hexagon(0, 0, 1.0, 1.4, 1.6, "paint", sides=6)
    k.drum(0.8, 0, 0.45, 0.0, 0.4, "black", 8)
    k.drum(0.8, 0, 0.3, -0.1, 0.0, "ember", 8)
    for i in range(4):
        ang = math.tau * (i + 0.5) / 4
        ca, sa = math.cos(ang) * 2.6, math.sin(ang) * 2.6
        tube_between((ca * 0.4, sa * 0.4, 1.0), (ca, sa, 1.0), 0.16, "dark", 4)
        hexagon(ca, sa, 1.2, 0.85, 1.15, "mid", r1=1.2, sides=8)                         # rotor ring
        hexagon(ca, sa, 0.9, 0.8, 0.86, "ember", sides=8)                                # its glow underneath


# ---- buildings ---------------------------------------------------------------------------------------------------

def scrap_pile(a, x, r, h, n=5, seed=0):
    """A heap of angled scrap plates round a spot."""
    for i in range(n):
        ang = math.tau * (i + 0.37 * seed) / n
        ca, sa = math.cos(ang) * r, math.sin(ang) * r
        k.upright([(a + ca - 1.2, 0.0), (a + ca + 1.2, 0.0), (a + ca + 0.5, h), (a + ca - 0.6, h * 0.8)],
                  x + sa - 0.15, x + sa + 0.15, "plate" if i % 2 else "mid")


def l_hq():
    """The Lattice's heart: a low scrap mound round a tall glowing amber core held up by curved struts, team plates
    on the mound, eyes all round."""
    hexagon(0, 0, 15.0, 0.0, 4.0, "mid", r1=12.0, sides=7)
    hexagon(0, 0, 12.0, 4.0, 6.5, "plate", r1=9.0, sides=7)
    hexagon(0, 0, 9.2, 6.5, 6.9, "paint", r1=8.6, sides=7)
    hexagon(0, 0, 3.0, 6.9, 18.0, "ember", r1=2.0, sides=6)                            # the core
    for i in range(5):                                                                  # struts holding it
        ang = math.tau * i / 5
        ca, sa = math.cos(ang), math.sin(ang)
        tube_between((ca * 9.0, sa * 9.0, 6.5), (ca * 5.0, sa * 5.0, 16.0), 0.7, "dark", 4)
        tube_between((ca * 5.0, sa * 5.0, 16.0), (ca * 2.4, sa * 2.4, 19.5), 0.5, "dark", 4)
    hexagon(0, 0, 2.6, 18.0, 19.0, "black", r1=1.6, sides=6)
    for i in range(7):
        ang = math.tau * (i + 0.5) / 7
        eyes(math.cos(ang) * 13.5, math.sin(ang) * 13.5, 2.0, 1, 0.5)
    scrap_pile(-10, 7, 3.0, 3.5, 4, 1)
    scrap_pile(-9, -8, 2.5, 3.0, 4, 2)


def nest(kind_mark):
    """A Lattice factory: a ring of scrap walls round a fabricator pit, a tall bent crane over it, the team plate on
    the walls; `kind_mark` adds what it makes, as a sign on the crane."""
    for i in range(6):
        ang = math.tau * (i + 0.5) / 6
        if abs(math.atan2(math.sin(ang), math.cos(ang))) < 0.6:
            continue                                                                    # the front left open
        ca, sa = math.cos(ang) * 8.5, math.sin(ang) * 8.5
        plate(ca - 2.6, ca + 2.6, sa - 2.6, sa + 2.6, 0.0, 4.5, "plate", inset=0.8)
        plate(ca - 1.6, ca + 1.6, sa - 1.6, sa + 1.6, 4.5, 4.8, "paint", inset=0.3)
    hexagon(0, 0, 5.5, 0.0, 0.8, "dark", r1=5.0, sides=6)                               # the pit
    hexagon(0, 0, 3.0, 0.8, 0.9, "ember", sides=6)
    tube_between((-6.5, 0, 0.0), (-4.0, 0, 11.0), 0.8, "dark", 4)                     # the crane, bent over the pit
    tube_between((-4.0, 0, 11.0), (1.0, 0, 9.0), 0.6, "dark", 4)
    k.drum(1.0, 0, 0.8, 7.2, 9.0, "mid", 6)
    eyes(1.6, 0, 8.4, 2, 0.3)
    kind_mark()


def factory_crawler():
    nest(lambda: [legs_round(6, 0.8, 1.2, 3.0, 4.2, 0.18)])


def factory_spider():
    nest(lambda: [legs_round(4, 0.9, 1.6, 3.0, 4.6, 0.22)])


def factory_piercer():
    nest(lambda: k.box(-3.0, 6.0, -0.35, 0.35, 2.2, 2.9, "dark"))


def l_tower():
    """A Lattice watch tower: a tall tripod of struts, a squat gun pod on top with a cluster of eyes."""
    for i in range(3):
        ang = math.tau * i / 3
        ca, sa = math.cos(ang), math.sin(ang)
        tube_between((ca * 6.0, sa * 6.0, 0.0), (ca * 1.4, sa * 1.4, 18.0), 0.6, "dark", 4)
        k.box(ca * 6.0 - 1.0, ca * 6.0 + 1.0, sa * 6.0 - 1.0, sa * 6.0 + 1.0, 0.0, 0.6, "mid")
    hexagon(0, 0, 3.0, 18.0, 21.0, "plate", r1=2.4, sides=6)
    hexagon(0, 0, 2.2, 21.0, 21.4, "paint", sides=6)
    k.box(1.8, 8.0, -0.3, 0.3, 19.2, 19.8, "dark")
    eyes(2.6, 0.0, 20.2, 3, 0.3, 0.7)


def l_outpost():
    """A Lattice outpost: a scrap nest shaped to its pad, a squat hub with a gun and a tall eye-stalk."""
    hexagon(0, 0, 11.0, 0.0, 3.0, "mid", r1=9.0, sides=6)
    hexagon(0, 0, 8.5, 3.0, 4.5, "plate", r1=7.0, sides=6)
    hexagon(0, 0, 6.6, 4.5, 4.8, "paint", sides=6)
    tube_between((-3.0, -2.0, 4.8), (-3.6, -2.4, 13.0), 0.4, "dark", 4)
    k.drum(-3.6, -2.4, 0.9, 13.0, 14.0, "black", 6)
    eyes(-2.6, -2.4, 13.4, 1, 0.5)
    k.box(1.0, 9.0, -0.3, 0.3, 5.4, 6.0, "dark")


def l_extractor():
    """A Lattice extractor: four legs straddling the deposit, a drill body hung between them, glowing at its root."""
    for i in range(4):
        ang = math.tau * (i + 0.5) / 4
        ca, sa = math.cos(ang), math.sin(ang)
        leg((ca * 2.5, sa * 2.5, 8.0), (ca * 6.0, sa * 6.0, 10.0), (ca * 7.5, sa * 7.5, 0.0), 0.6)
    hexagon(0, 0, 3.2, 6.0, 10.0, "plate", r1=2.6, sides=6)
    hexagon(0, 0, 2.6, 10.0, 10.4, "paint", sides=6)
    k.drum(0, 0, 1.2, 0.0, 6.0, "dark", 6, 0.5)                                        # the drill
    hexagon(0, 0, 2.0, 0.0, 0.2, "ember", sides=6)



def l_node():
    """A Lattice network node: three short scrap struts gripping the ground round a knot of welded plate, an amber eye
    on a stub where the tendrils meet."""
    for i in range(3):
        ang = math.tau * (i + 0.25) / 3
        ca, sa = math.cos(ang), math.sin(ang)
        leg((ca * 1.6, sa * 1.6, 4.0), (ca * 4.6, sa * 4.6, 5.4), (ca * 6.0, sa * 6.0, 0.0), 0.5)
    hexagon(0, 0, 3.0, 2.0, 5.4, "plate", r1=2.4, sides=6)                              # the knot
    hexagon(0, 0, 2.3, 5.4, 5.7, "paint", sides=6)
    k.drum(0, 0, 0.7, 5.7, 8.6, "dark", 6)
    hexagon(0, 0, 1.2, 8.6, 9.6, "ember", r1=0.8, sides=6)                              # its eye



def l_mobilebase():
    """A Lattice mobile base: a long scrap carapace carried on six legs, the nest it will become folded on its back as
    a hump of plates, a cluster of eyes at the front."""
    plate(-9.0, 9.0, -4.6, 4.6, 4.0, 7.6, "plate", inset=1.0, front=1.4, back=1.0)       # the carapace
    plate(-6.6, 4.4, -3.6, 3.6, 7.6, 10.2, "steel", inset=1.0, front=0.6, back=0.6)        # the folded nest
    plate(-5.4, 3.2, -2.6, 2.6, 10.2, 10.5, "paint", inset=0.3)
    for i, a in enumerate((-6.0, 0.0, 6.0)):
        for s in (1, -1):
            leg((a, s * 4.2, 5.4), (a + 1.0, s * 8.0, 8.0), (a + 1.6, s * 10.0, 0.0), 0.6)
    eyes(9.1, 0.0, 5.4, 4, 0.34, 0.8)
    hexagon(0, 0, 2.6, 2.4, 4.0, "dark", r1=2.0, sides=6)
    hexagon(0, 0, 1.6, 2.2, 2.4, "ember", sides=6)                                      # glow underneath


def l_factory_mobilebase():
    """The Lattice's utility nest: the nest ring with a fat brood pod on the crane, where drones and mobile bases are
    grown."""
    nest(lambda: [hexagon(1.0, 0, 2.2, 4.0, 7.0, "steel", r1=1.6, sides=6), eyes(3.0, 0, 6.0, 3, 0.3)])



def l_turret():
    """A Lattice spike turret: a squat scrap mound, a stubby tripod and a long spike-gun with an eye."""
    hexagon(0, 0, 6.4, 0.0, 2.0, "plate", r1=5.0, sides=6)
    hexagon(0, 0, 4.2, 2.0, 2.3, "paint", sides=6)
    for i in range(3):
        ang = math.tau * i / 3
        tube_between((math.cos(ang) * 3.0, math.sin(ang) * 3.0, 2.0), (0, 0, 6.0), 0.5, "dark", 4)
    k.drum(0, 0, 1.6, 5.4, 7.2, "mid", 6)
    k.box(0.5, 9.0, -0.3, 0.3, 6.0, 6.6, "dark")
    k.box(8.4, 9.6, -0.18, 0.18, 6.1, 6.5, "ember")
    eyes(1.7, 0, 6.4, 2, 0.28)


def l_flak():
    """A Lattice flak spitter: a cluster of tubes pointing up out of a scrap mound."""
    hexagon(0, 0, 6.0, 0.0, 2.2, "plate", r1=4.6, sides=6)
    hexagon(0, 0, 3.8, 2.2, 2.5, "paint", sides=6)
    for i in range(5):
        ang = math.tau * i / 5
        ca, sa = math.cos(ang) * 1.5, math.sin(ang) * 1.5
        tube_between((ca, sa, 2.4), (ca * 1.8 + 1.0, sa * 1.8, 9.5), 0.35, "dark", 6)
        k.drum(ca * 1.8 + 1.0, sa * 1.8, 0.3, 9.3, 9.7, "ember", 6)


def l_bombard():
    """A Lattice bombard mound: a wide scrap heap with a fat tube raised out of it."""
    hexagon(0, 0, 8.0, 0.0, 2.6, "plate", r1=6.0, sides=6)
    hexagon(0, 0, 5.2, 2.6, 2.9, "paint", sides=6)
    tube_between((-1.0, 0, 2.6), (6.0, 0, 11.0), 1.1, "dark", 6)
    k.drum(0, 0, 2.6, 2.6, 4.4, "mid", 6)
    eyes(2.7, 0, 3.6, 3, 0.3)


def l_wall():
    """A Lattice scrap wall: a block of plates welded on a frame, a team plate on top."""
    plate(-5.2, 5.2, -5.2, 5.2, 0.0, 6.0, "plate", inset=1.4, front=1.4, back=1.4)
    k.box(-3.6, 3.6, -3.6, 3.6, 6.0, 6.25, "paint")
    for s in (1, -1):
        tube_between((-5.0, s * 5.0, 0.4), (5.0, s * 5.0, 5.0), 0.35, "dark", 4)



def l_ranger():
    """A Lattice ranger: a small skittering four-legged scrap body, a spine gun along its back, three eyes."""
    hexagon(0, 0, 1.6, 1.6, 3.0, "plate", r1=1.2, sides=6)
    hexagon(0, 0, 1.1, 3.0, 3.15, "paint", sides=6)
    legs_round(4, 1.2, 2.4, 2.2, 3.2, 0.22)
    k.box(-1.0, 3.6, -0.18, 0.18, 3.2, 3.5, "dark")
    eyes(1.7, 0, 2.3, 3, 0.18, 0.4)


def l_flier():
    """A Lattice flier: an insect-like scrap body slung between four buzzing wing plates, amber eyes. Flies."""
    k.slab([(4.0, 0.0), (1.0, 1.0), (-3.4, 0.6), (-3.4, -0.6), (1.0, -1.0)], 0.0, 1.2, "plate")
    k.slab([(2.4, 0.0), (0.6, 0.6), (-2.0, 0.4), (-2.0, -0.4), (0.6, -0.6)], 1.2, 1.35, "paint")
    for s in (1, -1):
        k.slab([(1.4, s * 0.8), (0.4, s * 5.0), (-0.8, s * 5.0), (-0.4, s * 0.8)] if s > 0 else [(1.4, -0.8), (-0.4, -0.8), (-0.8, -5.0), (0.4, -5.0)], 1.0, 1.1, "steel")
        k.slab([(-0.8, s * 0.7), (-2.0, s * 3.6), (-2.8, s * 3.6), (-2.0, s * 0.7)] if s > 0 else [(-0.8, -0.7), (-2.0, -0.7), (-2.8, -3.6), (-2.0, -3.6)], 0.9, 1.0, "steel")
    eyes(4.0, 0, 0.6, 2, 0.25, 0.5)


def l_bomber():
    """A Lattice bomber: a bloated welded sac of a hull on four rotor rings, a cluster of eyes, a bomb cradle below.
    Flies."""
    hexagon(0, 0, 3.4, 0.8, 3.6, "plate", r1=2.6, sides=8)
    hexagon(0, 0, 2.4, 3.6, 3.85, "paint", sides=8)
    k.drum(0, 0, 1.2, 0.0, 0.8, "dark", 6)
    for i in range(4):
        ang = math.tau * (i + 0.5) / 4
        ca, sa = math.cos(ang) * 5.0, math.sin(ang) * 5.0
        tube_between((ca * 0.5, sa * 0.5, 2.6), (ca, sa, 2.6), 0.3, "dark", 4)
        hexagon(ca, sa, 2.0, 2.3, 2.7, "mid", r1=2.0, sides=8)
        hexagon(ca, sa, 1.6, 2.2, 2.28, "ember", sides=8)
    eyes(3.4, 0, 2.0, 4, 0.3, 0.6)


which = sys.argv[sys.argv.index("--") + 1]
k.begin(0.0)
globals()[which]()
finish_realistic(which, 34.0 if which.startswith(("l_", "factory_")) else 22.0, style="lattice")
