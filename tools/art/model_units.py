"""Battler's first unit models: model_units.py -- <tank|artillery|scout>
(The user, 2026-10-19: "remember these units need to have unique sillhouttees since they will be seen from far, a tank
and artillery should look different, lets take this stlye forward and develop a basic tank like a sherman, a basic
artillery, a basic scout".) The style: gritty dieselpunk on tracks (art/_gen/concepts/track_sheet_s7.png,
gmech_sheet_s7.png): worn olive riveted steel, exhaust stacks, no crews, so glowing sensor eyes instead of hatches.
Each unit's role reads in its outline: the tank a tall hull and a round turret with a medium gun; the artillery a low
open chassis under a very long howitzer raised steeply, spades at the back; the scout small and narrow with a big
searchlight eye and a tall sensor mast.
Built with ai-tools' kit (C:/dev/ai-tools/blender/ship_kit.py): `a` along it (front +), `x` across, `z` up from the
ground. "paint" is the team colour (alpha 0 in the texture: the game recolours it).
Run: blender --background --python tools/art/model_units.py -- tank
-> art/units/<name>.glb and <name>.png; art/_gen/ships_iso/work/<name>_model_views.png to judge."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *      # the palette, Blender's kit as k, and the shared pieces

def tank():
    """A medium tank like a Sherman: tall hull, sloped glacis, round cast turret in the middle, a medium gun."""
    k.begin(0.0)
    for s in (1, -1):
        track(s, 3.0, 4.5, -8.0, 7.6, 3.2, 5)
    k.box(-7.4, 6.0, -3.0, 3.0, 0.9, 3.6, "plate")                                    # lower hull between the tracks
    k.box(-7.6, 5.0, -4.9, 4.9, 3.4, 5.8, "steel")                                    # upper hull over the tracks
    k.upright([(5.0, 1.2), (8.4, 1.2), (8.4, 2.8), (5.0, 5.8)], -4.6, 4.6, "steel")  # sloped glacis
    k.box(5.6, 7.2, -3.6, 3.6, 3.2, 5.1, "plate")                                     # glacis armour plate, proud
    for s in (1, -1):
        rivets(-7.0, 4.6, s * 4.9, 5.4, 9)
        k.box(-6.5, 3.5, min(s * 4.9, s * 5.1), max(s * 4.9, s * 5.1), 3.8, 5.0, "paint")   # team panel on each flank
        eye(8.45, s * 2.8, 2.2, 0.32)                                                  # driving eyes low on the nose
    for i in range(3):                                                                # spare track links on the glacis
        k.box(6.0 + i * 0.75, 6.5 + i * 0.75, -2.2, 2.2, 3.9 - i * 0.67, 4.4 - i * 0.67, "black")
    # engine deck: louvres and two exhaust stacks rising at the back (the diesel)
    k.box(-7.6, -3.4, -3.8, 3.8, 5.8, 6.1, "plate")
    for i in range(6):
        k.box(-7.2 + i * 0.65, -6.9 + i * 0.65, -3.2, 3.2, 6.1, 6.3, "black")
    for s in (1, -1):
        stack(-7.0, s * 3.6, 5.8, 9.2)
    # turret: round cast drum, a band of team colour, a mantlet and the gun
    k.drum(0.4, 0, 3.4, 5.8, 6.4, "dark", 12)                                         # turret ring
    k.drum(0.4, 0, 3.2, 6.4, 8.6, "steel", 12, 2.8)
    k.drum(0.4, 0, 3.22, 7.1, 7.6, "paint", 12, 3.0)
    k.dome(0.4, 0, 2.8, 8.6, 0.9, "paint", 2, 12)                                    # team colour on the roof: what the camera sees
    k.studs(0.4, 0, 3.05, 6.6, 6.8, "dark", 14, 0.13)
    k.box(2.8, 4.2, -1.5, 1.5, 6.4, 8.4, "plate", nose=0.8)                           # mantlet
    k.tube(4.0, 11.5, 0, 7.4, 0.42, "mid", 8)                                         # the gun
    k.tube(10.4, 11.9, 0, 7.4, 0.62, "dark", 8)                                       # muzzle brake
    eye(1.6, 1.9, 9.1, 0.45)                                                          # the turret's sensor eye
    k.drum(-0.8, -1.4, 0.5, 9.0, 9.6, "dark", 8)                                      # sensor cupola
    k.drum(-1.6, 1.6, 0.08, 9.0, 13.0, "black", 4)                                    # aerial
    k.finish("tank", 30.0, UNITS)


def artillery():
    """Self-propelled artillery: a low open chassis carrying a very long howitzer raised steeply, spades at the back."""
    k.begin(0.0)
    for s in (1, -1):
        track(s, 3.0, 4.5, -8.0, 6.8, 2.8, 5)
    k.box(-8.0, 6.4, -3.0, 3.0, 0.8, 3.2, "plate")
    k.box(-7.0, 6.6, -4.8, 4.8, 3.0, 4.1, "steel")                                    # low flat deck
    k.upright([(6.6, 1.2), (8.0, 1.2), (8.0, 2.4), (6.6, 4.1)], -4.4, 4.4, "steel")   # short nose
    for s in (1, -1):
        rivets(-6.6, 6.0, s * 4.8, 3.6, 10)
        eye(8.05, s * 2.6, 1.9, 0.3)
        k.box(-1.0, 5.5, min(s * 4.8, s * 5.0), max(s * 4.8, s * 5.0), 3.1, 3.9, "paint")
        # recoil spades: hinged arms down to blades dug in behind the tracks
        tube_between((-7.4, s * 2.6, 3.0), (-11.0, s * 2.9, 0.6), 0.32, "dark", 6)
        k.box(-11.8, -10.6, min(s * 2.0, s * 3.8), max(s * 2.0, s * 3.8), 0.0, 1.4, "plate", tail=1.0, nose_z=0.6)
    # the gun: a turntable at the back half, trunnions, a cradle and a long barrel raised 38 degrees forward
    k.drum(-2.0, 0, 3.0, 4.1, 4.6, "paint", 12)                                      # turntable in team colour, seen from above
    for s in (1, -1):
        k.box(-3.6, -0.6, min(s * 1.4, s * 2.2), max(s * 1.4, s * 2.2), 4.6, 7.4, "steel")   # trunnion cheeks
    k.upright([(-1.0, 4.6), (2.6, 4.6), (2.6, 6.4), (0.4, 9.0), (-1.0, 9.0)], -3.4, -3.0, "plate")   # gun shield, port
    k.upright([(-1.0, 4.6), (2.6, 4.6), (2.6, 6.4), (0.4, 9.0), (-1.0, 9.0)], 3.0, 3.4, "plate")     # and starboard
    pivot, tilt, length = Vector((-2.2, 0.0, 6.6)), math.radians(38), 19.0
    d = Vector((math.cos(tilt), 0.0, math.sin(tilt)))
    tube_between(pivot - d * 3.4, pivot + d * 1.5, 1.05, "dark", 8)                   # breech
    tube_between(pivot + d * 1.5, pivot + d * length, 0.55, "mid", 8, 0.48)           # barrel
    tube_between(pivot + d * (length - 1.6), pivot + d * (length + 0.6), 0.85, "dark", 8)   # muzzle brake
    under = Vector((0.0, 0.0, -0.95))
    tube_between(pivot + under - d * 1.0, pivot + under + d * 8.0, 0.36, "steel", 6)  # recuperator under the barrel
    tube_between(pivot + d * 3.0, pivot + d * 5.0, 0.72, "paint", 8)                  # team band on the barrel
    # ammunition: racks of shells on the deck either side
    for s in (1, -1):
        for i in range(4):
            k.drum(-6.6 + i * 0.9, s * 3.8, 0.34, 4.1, 5.6, "rust", 6, 0.22)
    eye(0.6, -2.6, 8.4, 0.4)                                                          # rangefinder eye on the shield
    k.box(-0.6, 0.6, -3.0, -2.2, 7.6, 8.6, "dark")
    stack(-6.6, 0.0, 4.1, 6.6, 0.36)
    k.finish("artillery", 38.0, UNITS)


def scout():
    """A light scout: small, narrow and low, a big searchlight eye on the nose and a tall sensor mast."""
    k.begin(0.0)
    for s in (1, -1):
        track(s, 1.9, 2.9, -5.2, 5.0, 2.2, 3, skirt=False)
    k.box(-5.0, 3.6, -1.9, 1.9, 0.7, 2.6, "plate")
    k.box(-5.0, 2.8, -2.2, 2.2, 2.4, 3.8, "steel", nose=1.0)                          # small upper body
    k.upright([(2.8, 1.0), (6.0, 1.0), (6.0, 2.0), (2.8, 3.8)], -2.1, 2.1, "steel")   # wedge nose
    for s in (1, -1):
        rivets(-4.6, 2.4, s * 2.2, 3.4, 6)
        k.box(-4.6, 1.8, min(s * 2.2, s * 2.36), max(s * 2.2, s * 2.36), 2.6, 3.3, "paint")
    # the big searchlight eye on top of the nose
    k.box(1.4, 3.4, -1.2, 1.2, 3.8, 5.2, "dark")
    k.tube(3.4, 4.0, 0, 4.5, 1.0, "black", 10)
    k.tube(4.0, 4.15, 0, 4.5, 0.78, "ember", 10)
    # a radio set beside it, with a whip aerial: it has no gun (it finds things; others fight)
    k.box(-0.2, 1.4, 1.2, 2.0, 3.8, 4.8, "dark")
    k.drum(0.6, 1.6, 0.08, 4.8, 8.0, "black", 4)
    # the tall sensor mast at the back: a pole, a crossbar, a dish and a lamp at the top
    k.box(-4.6, 0.8, -1.8, 1.8, 3.8, 4.1, "paint")                                    # team colour on the roof
    k.box(-4.6, -2.6, -1.6, 1.6, 3.8, 4.4, "plate")
    k.drum(-3.6, 0, 0.32, 4.4, 12.5, "dark", 6, 0.2)
    k.box(-3.75, -3.45, -1.6, 1.6, 10.6, 10.9, "dark")
    k.drum(-3.1, 0, 1.3, 11.2, 11.5, "mid", 10, 1.0)                                  # dish, tilted forward by its offset
    k.drum(-3.6, 0, 0.25, 12.5, 12.9, "ember", 6)
    stack(-4.8, -1.4, 3.8, 6.0, 0.3)
    k.finish("scout", 24.0, UNITS)


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"tank": tank, "artillery": artillery, "scout": scout}[which]()
