"""Low-poly Coalition mock-ups: coalition_mockups.py -- <style>_<unit>   (e.g. a_tank)
(The user, 2026-10-21: "I want a complete rework on models. Low poly models, the simpler the better. Show me some mock
ups of coalition".) Three simple styles, each a handful of pieces and a few hundred triangles, flat colour (the bake's
shading only), no painted detail:
  A BLOCK  - plain boxes: a hull, two track blocks, a box turret, a square barrel.
  B WEDGE  - the same with sloped faces: raked fronts, tapered turret, chamfered tracks.
  C ROUND  - softened: drum turrets, rounded tracks, a capsule-ish hull.
Units: light tank, tank, artillery, scout. Out to art/_gen/mockups (not the game's models).
Run: blender --background --python tools/art/coalition_mockups.py -- b_tank"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unit_parts import *

OUT = k.PROJECT + "/art/_gen/mockups"
os.makedirs(OUT, exist_ok=True)
k.recolour(steel=(196, 200, 206), plate=(150, 156, 164), dark=(58, 62, 70), black=(30, 32, 36))


# ---- A BLOCK ------------------------------------------------------------------------------------------------------

def a_light():
    for s in (1, -1):
        k.box(-4.0, 4.0, *sorted((s * 2.6, s * 4.0)), 0.0, 1.6, "dark")
    k.box(-4.4, 4.4, -2.8, 2.8, 1.0, 2.8, "steel")
    k.box(-1.8, 1.4, -1.8, 1.8, 2.8, 4.0, "steel")
    k.box(-1.4, 1.0, -1.4, 1.4, 4.0, 4.15, "paint")
    k.box(1.4, 7.0, -0.25, 0.25, 3.2, 3.7, "dark")


def a_tank():
    for s in (1, -1):
        k.box(-6.0, 6.0, *sorted((s * 3.2, s * 5.0)), 0.0, 2.4, "dark")
    k.box(-6.4, 6.4, -3.6, 3.6, 1.2, 4.0, "steel")
    k.box(-3.0, 2.4, -2.8, 2.8, 4.0, 6.0, "steel")
    k.box(-2.4, 1.6, -2.0, 2.0, 6.0, 6.2, "paint")
    k.box(2.4, 10.5, -0.4, 0.4, 4.6, 5.4, "dark")


def a_artillery():
    for s in (1, -1):
        k.box(-6.5, 6.0, *sorted((s * 3.0, s * 4.8)), 0.0, 2.2, "dark")
    k.box(-6.8, 6.4, -3.4, 3.4, 1.0, 3.4, "steel")
    k.box(-6.0, -0.6, -2.8, 2.8, 3.4, 5.6, "steel")
    k.box(-5.4, -1.2, -2.0, 2.0, 5.6, 5.8, "paint")
    tube_between((-3.0, 0, 5.0), (8.0, 0, 13.0), 0.5, "dark", 4)


def a_scout():
    for s in (1, -1):
        k.box(-3.5, 3.0, *sorted((s * 1.8, s * 3.0)), 0.6, 1.6, "plate")
        k.box(-3.0, 2.5, *sorted((s * 2.0, s * 2.8)), 0.45, 0.6, "glow")
    k.box(-3.6, 3.8, -1.8, 1.8, 1.2, 2.6, "steel")
    k.box(-3.0, 1.0, -1.2, 1.2, 2.6, 2.8, "paint")
    k.box(-2.4, -2.0, -0.2, 0.2, 2.8, 5.0, "dark")
    k.box(-3.2, -1.2, -1.0, 1.0, 5.0, 5.3, "steel")


# ---- B WEDGE ------------------------------------------------------------------------------------------------------

def b_light():
    for s in (1, -1):
        plate(-4.0, 4.0, *sorted((s * 2.6, s * 4.0)), 0.0, 1.7, "dark", inset=0.2, front=0.7, back=0.7)
    plate(-4.4, 4.6, -2.9, 2.9, 1.0, 2.9, "steel", inset=0.35, front=1.8, back=0.5)
    plate(-1.9, 1.5, -1.9, 1.9, 2.9, 4.1, "steel", inset=0.4, front=0.9)
    plate(-1.3, 0.7, -1.2, 1.2, 4.1, 4.25, "paint", inset=0.1)
    k.box(1.3, 7.0, -0.22, 0.22, 3.3, 3.75, "dark")


def b_tank():
    for s in (1, -1):
        plate(-6.0, 6.0, *sorted((s * 3.2, s * 5.0)), 0.0, 2.5, "dark", inset=0.25, front=1.0, back=1.0)
    plate(-6.5, 6.8, -3.7, 3.7, 1.2, 4.2, "steel", inset=0.4, front=2.6, back=0.7)
    plate(-3.2, 2.6, -3.0, 3.0, 4.2, 6.3, "steel", inset=0.6, front=1.3, back=0.3)
    plate(-2.4, 0.8, -1.9, 1.9, 6.3, 6.5, "paint", inset=0.15)
    k.box(2.0, 10.5, -0.38, 0.38, 4.9, 5.6, "dark")


def b_artillery():
    for s in (1, -1):
        plate(-6.5, 6.0, *sorted((s * 3.0, s * 4.8)), 0.0, 2.3, "dark", inset=0.2, front=0.9, back=0.9)
    plate(-6.8, 6.6, -3.5, 3.5, 1.0, 3.5, "steel", inset=0.35, front=2.0, back=0.4)
    plate(-6.2, -0.4, -2.9, 2.9, 3.5, 5.9, "steel", inset=0.6, front=1.4)
    plate(-5.2, -2.0, -1.9, 1.9, 5.9, 6.1, "paint", inset=0.15)
    tube_between((-3.0, 0, 5.2), (8.5, 0, 13.4), 0.5, "dark", 4)
    tube_between((7.6, 0, 12.8), (8.9, 0, 13.7), 0.75, "dark", 4)


def b_scout():
    k.slab([(5.0, 0.0), (1.0, 2.0), (-3.6, 1.8), (-3.6, -1.8), (1.0, -2.0)], 1.2, 2.6, "steel")
    k.slab([(3.4, 0.0), (0.6, 1.1), (-2.6, 1.0), (-2.6, -1.0), (0.6, -1.1)], 2.6, 2.8, "paint")
    for s in (1, -1):
        plate(-3.6, 2.2, *sorted((s * 1.8, s * 3.0)), 0.6, 1.7, "plate", inset=0.2, front=0.8)
        k.box(-3.0, 1.6, *sorted((s * 2.0, s * 2.8)), 0.45, 0.6, "glow")
    k.box(-2.0, -1.6, -0.2, 0.2, 2.8, 4.8, "dark")
    hexagon(-1.6, 0, 1.2, 4.8, 5.0, "steel", r1=0.9)


# ---- C ROUND ------------------------------------------------------------------------------------------------------

def rounded_track(s, inner, outer, a0, a1, h):
    """A track as a long rounded block: two half-drums at the ends joined by a box."""
    x0, x1 = sorted((s * inner, s * outer))
    r = h / 2
    tube_between((a0 + r, x0, r), (a0 + r, x1, r), r, "dark", 8)
    tube_between((a1 - r, x0, r), (a1 - r, x1, r), r, "dark", 8)
    k.box(a0 + r, a1 - r, x0, x1, 0.0, h, "dark")


def c_light():
    for s in (1, -1):
        rounded_track(s, 2.6, 4.0, -4.0, 4.0, 1.7)
    plate(-4.4, 4.4, -2.8, 2.8, 1.0, 2.8, "steel", inset=0.5, front=1.2, back=0.9)
    k.drum(-0.2, 0, 1.9, 2.8, 4.0, "steel", 10, 1.6)
    k.drum(-0.2, 0, 1.3, 4.0, 4.2, "paint", 10)
    k.tube(1.4, 7.0, 0, 3.45, 0.24, "dark", 6)


def c_tank():
    for s in (1, -1):
        rounded_track(s, 3.2, 5.0, -6.0, 6.0, 2.5)
    plate(-6.4, 6.6, -3.6, 3.6, 1.2, 4.1, "steel", inset=0.7, front=2.0, back=1.2)
    k.drum(-0.4, 0, 3.0, 4.1, 6.1, "steel", 12, 2.5)
    k.drum(-0.4, 0, 2.0, 6.1, 6.35, "paint", 12)
    k.tube(2.4, 10.5, 0, 5.2, 0.4, "dark", 8)


def c_artillery():
    for s in (1, -1):
        rounded_track(s, 3.0, 4.8, -6.5, 6.0, 2.3)
    plate(-6.8, 6.4, -3.4, 3.4, 1.0, 3.4, "steel", inset=0.6, front=1.6, back=0.8)
    k.drum(-3.3, 0, 2.9, 3.4, 5.6, "steel", 12, 2.5)
    k.drum(-3.3, 0, 1.9, 5.6, 5.8, "paint", 12)
    tube_between((-3.0, 0, 5.0), (8.5, 0, 13.2), 0.5, "dark", 8)


def c_scout():
    k.drum(0.0, 0, 2.4, 1.0, 2.4, "steel", 12, 2.0)
    k.dome(0.0, 0, 2.0, 2.4, 0.6, "paint", 2, 12)
    for s in (1, -1):
        k.drum(0.0, s * 2.6, 1.0, 0.5, 1.4, "plate", 10, 0.9)
        k.drum(0.0, s * 2.6, 0.8, 0.38, 0.5, "glow", 10)
    k.drum(-0.6, 0, 0.18, 2.6, 4.8, "dark", 6)
    k.drum(-0.6, 0, 1.0, 4.8, 5.0, "steel", 10, 0.7)


which = sys.argv[sys.argv.index("--") + 1]
k.begin(0.0)
globals()[which]()
k.finish("mock_" + which, 26.0, OUT)
