"""The feral machines: model_feral.py -- <feral|feralbrute|derelict>
(The user, 2026-10-22: something "that slows down the early game"; feral waves and guarded deposits.) Nobody's
machines gone wild in the wastes: scrap buggies on mismatched wheels, a brute with a ram, old turrets rusted solid,
all with sickly green eyes so they read apart from both sides (the Lattice's eyes are amber, the Coalition's lights
cyan). The game paints their team plates a dull olive.
Run: blender --background --python tools/art/model_feral.py -- feral"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unit_parts import *

# Grey rusted steel, brown scrap, black wheels; "green" glows in the game (the eyes).
k.recolour(steel=(128, 124, 112), plate=(110, 82, 58), mid=(84, 80, 72), dark=(46, 44, 40), black=(24, 23, 22))


def wheel(a, x, z, r, w):
    tube_between((a, x - w / 2, z), (a, x + w / 2, z), r, "black", 10)
    tube_between((a, x - w / 2 - 0.05, z), (a, x + w / 2 + 0.05, z), r * 0.4, "mid", 6)


def feral():
    """Feral scrapper: a little scrap buggy on four mismatched wheels, a buzz-saw out front, one green eye."""
    k.begin(0.0)
    for a, r in ((2.4, 1.4), (-2.4, 1.6)):
        for s in (1, -1):
            wheel(a, s * 2.8, r, r, 1.0)
    plate(-3.4, 3.6, -2.2, 2.2, 1.4, 3.6, "plate", inset=0.5, front=1.2, back=0.4)
    plate(-2.2, 1.0, -1.4, 1.4, 3.6, 3.85, "paint", inset=0.15)
    tube_between((3.6, 0, 2.4), (4.6, 0, 2.4), 0.25, "dark", 6)
    tube_between((4.6, -0.12, 2.4), (4.6, 0.12, 2.4), 1.3, "mid", 12)               # the saw
    k.tube(3.5, 3.8, 0, 3.2, 0.32, "green", 8)                                       # its eye
    tube_between((-2.6, 1.2, 3.6), (-3.4, 1.4, 6.0), 0.12, "dark", 4)                 # a bent aerial
    finish_realistic("feral", 14.0, style="lattice")


def feralbrute():
    """Feral brute: a big scrap half-track, a ram blade on the front, two green eyes, a heap of plates on its back."""
    k.begin(0.0)
    for s in (1, -1):
        wheel(3.6, s * 4.0, 1.9, 1.9, 1.4)
        k.box(-5.2, 1.6, s * 4.0 - 0.8, s * 4.0 + 0.8, 0.0, 2.6, "black")              # the track
    plate(-5.6, 5.0, -3.4, 3.4, 1.6, 5.6, "plate", inset=0.8, front=1.6, back=0.6)
    plate(-4.2, 1.6, -2.4, 2.4, 5.6, 7.0, "steel", inset=0.6)
    plate(-3.4, 0.8, -1.6, 1.6, 7.0, 7.2, "paint", inset=0.15)
    plate(5.0, 6.4, -4.2, 4.2, 0.3, 4.2, "mid", inset=0.2, front=1.0)                 # the ram
    for x in (-1.0, 1.0):
        k.tube(4.9, 5.2, x, 4.6, 0.38, "green", 8)
    finish_realistic("feralbrute", 22.0, style="lattice")


def derelict():
    """Derelict: an old turret rusted where it stands on a heap of scrap, its barrel bent, one eye still lit."""
    k.begin(0.0)
    hexagon(0, 0, 7.4, 0.0, 1.8, "plate", r1=6.0, sides=7)
    for i in range(5):
        ang = i * 1.3
        k.box(math.cos(ang) * 5.0 - 1.2, math.cos(ang) * 5.0 + 1.2, math.sin(ang) * 5.0 - 0.8, math.sin(ang) * 5.0 + 0.8, 1.2, 2.6, "mid")
    k.drum(0, 0, 3.6, 1.8, 5.2, "steel", 8)
    plate(-2.8, 3.0, -2.6, 2.6, 5.2, 7.6, "steel", inset=0.6, front=1.0)
    plate(-2.0, 1.4, -1.6, 1.6, 7.6, 7.8, "paint", inset=0.15)
    tube_between((2.8, 0, 6.4), (6.4, 0.6, 6.0), 0.45, "dark", 8)
    tube_between((6.4, 0.6, 6.0), (8.6, 1.6, 4.6), 0.42, "dark", 8)                 # bent
    k.tube(2.9, 3.2, -1.0, 6.8, 0.4, "green", 8)
    finish_realistic("derelict", 22.0, style="lattice")


which = sys.argv[sys.argv.index("--") + 1]
{"feral": feral, "feralbrute": feralbrute, "derelict": derelict}[which]()
