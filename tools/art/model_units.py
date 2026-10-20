"""Battler's units: model_units.py -- <tank|artillery|scout|lighttank|mortar>
Basic boxy shapes with the detail in the texture (the user, 2026-10-20: "the units should be basic shapes but have
detailed textures, boxy is fine"; Mechabellum and Supreme Commander, "modern futuristic but grounded"; no crew
features: "no humans"). finish_textured (unit_parts.py) softens the edges and paints panels, rivets, grime,
chevrons and serials into the texture.
The look is clean modern war machines like Supreme Commander's (the user, 2026-10-20), set in unit_parts.py.
Each unit's role still reads in its outline from far off (the user, 2026-10-19: "units need to have unique
sillhouttees"): the tank a low wedge hull with an angular turret and a long gun; the artillery a long chassis with a
twin-rail cannon raised steeply over its back, stabiliser legs out behind; the scout a small arrowhead hovercraft with
glowing lift pads and a sensor dish, and no gun (it finds things; others fight).
Built with ai-tools' kit (C:/dev/ai-tools/blender/ship_kit.py): `a` along it (front +), `x` across, `z` up from the
ground. "paint" is the team colour (alpha 0 in the texture: the game recolours it).
Run: blender --background --python tools/art/model_units.py -- tank
-> art/units/<name>.glb and <name>.png; art/_gen/ships_iso/work/<name>_model_views.png to judge."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *      # the palette, Blender's kit as k, and the shared pieces


def tank():
    """A main battle tank: two armoured track pods, a low wedge hull, a flat angular turret with a long gun."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 2.9, 5.0, -8.0, 7.8, 3.4)
    k.box(-7.0, 6.6, -3.0, 3.0, 0.8, 3.4, "dark")                                      # belly between the pods
    plate(-7.8, 7.8, -4.4, 4.4, 3.2, 5.0, "steel", inset=0.5, front=3.0, back=0.9)     # upper hull, raked glacis
    k.upright([(4.8, 3.2), (8.6, 2.4), (8.6, 3.0), (4.8, 3.6)], -3.6, 3.6, "plate")    # nose armour, a lip
    for s in (1, -1):
        strip(-6.8, 3.6, s * 3.2 - 0.35, s * 3.2 + 0.35, 4.95, "paint", 0.1)            # team stripes down the deck
        lens(8.62, s * 2.6, 2.8, 0.28)                                                   # headlamp sensors
    vents(-7.4, -4.2, -2.4, 2.4, 4.95, 5)                                               # engine grille
    # the turret: an angular cast block, raked front, a team panel on the roof, lit strips down its cheeks
    plate(-3.4, 2.8, -3.0, 3.0, 5.0, 7.2, "steel", inset=0.6, front=1.6, back=0.5)
    plate(-2.6, 1.0, -2.0, 2.0, 7.2, 7.5, "paint", inset=0.3)
    for s in (1, -1):
        strip(-2.6, 1.0, s * 2.55 - 0.05, s * 2.55 + 0.05, 6.4, "glow", 0.18)
    k.box(1.6, 4.0, -0.9, 0.9, 5.6, 6.8, "plate", nose=0.8)                              # mantlet
    k.tube(3.6, 12.4, 0, 6.2, 0.36, "mid", 8)                                           # the gun
    k.tube(7.0, 9.0, 0, 6.2, 0.48, "dark", 8)                                           # its sleeve
    k.tube(11.6, 12.6, 0, 6.2, 0.5, "dark", 8)                                          # muzzle
    lens(2.8, 1.8, 7.0, 0.3)                                                            # the turret's sight
    k.box(-3.0, -1.8, -1.6, -0.6, 7.2, 7.9, "dark")                                     # sensor block
    k.drum(-2.6, 1.6, 0.06, 7.2, 11.0, "black", 4)                                      # whip aerial
    k.drum(-2.6, 1.6, 0.14, 11.0, 11.3, "glow", 4)                                      # its tip light
    finish_textured("tank", 30.0, serial="12")


def artillery():
    """Mobile artillery: a long chassis on track pods, a heavy turret at the back carrying a twin-rail cannon raised
    35 degrees, stabiliser legs braced out behind."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 3.0, 4.8, -8.4, 7.0, 3.0)
    k.box(-8.0, 6.4, -3.0, 3.0, 0.7, 3.0, "dark")
    plate(-8.4, 7.4, -4.4, 4.4, 2.8, 4.2, "steel", inset=0.4, front=2.2, back=0.5)      # long low deck
    for s in (1, -1):
        lens(7.45, s * 2.4, 2.6, 0.26)
        strip(0.0, 6.0, s * 3.4 - 0.3, s * 3.4 + 0.3, 4.15, "paint", 0.1)
        # stabiliser legs: struts down and back to pads on the ground
        tube_between((-7.6, s * 3.0, 3.2), (-11.6, s * 4.4, 0.5), 0.32, "mid", 6)
        k.box(-12.6, -10.8, s * 4.4 - 0.9, s * 4.4 + 0.9, 0.0, 0.6, "plate", tail=0.8)
    vents(2.0, 6.0, -2.0, 2.0, 4.15, 4)                                                 # engine grille at the front
    # the turret: a heavy angular block at the back, its roof in team colour
    plate(-7.0, -0.6, -3.2, 3.2, 4.2, 6.6, "steel", inset=0.6, front=1.2)
    plate(-6.2, -2.4, -2.0, 2.0, 6.6, 6.9, "paint", inset=0.3)
    for s in (1, -1):
        k.box(-4.4, -1.8, s * 1.0 - 0.55 * s, s * 2.4, 6.0, 8.6, "plate")              # trunnion cheeks
    # the cannon: twin rails raised 35 degrees, lit coils along them, a heavy breech
    pivot, tilt, length = Vector((-3.1, 0.0, 7.6)), math.radians(35), 18.0
    d = Vector((math.cos(tilt), 0.0, math.sin(tilt)))
    tube_between(pivot - d * 2.6, pivot + d * 2.0, 1.1, "dark", 8)                      # breech
    for s in (1, -1):
        off = Vector((0.0, s * 0.42, 0.0))
        tube_between(pivot + off + d * 1.0, pivot + off + d * length, 0.3, "mid", 6)    # a rail
    for i in range(4):                                                                  # the coils that throw the shell
        c = pivot + d * (4.0 + i * 3.2)
        tube_between(c - d * 0.4, c + d * 0.4, 0.75, "glow" if i % 2 == 0 else "dark", 8)
    tube_between(pivot + d * (length - 0.6), pivot + d * (length + 0.6), 0.8, "dark", 8)   # muzzle collar
    lens(-0.5, -2.4, 7.0, 0.32)
    finish_textured("artillery", 38.0, serial="31")


def scout():
    """A scout hovercraft: a small arrowhead hull on two lift nacelles glowing underneath, a sensor dish on a short
    mast. Unarmed."""
    k.begin(0.0)
    lift = 1.1                                                                          # it floats this high
    k.slab([(6.5, 0.0), (1.5, 2.4), (-4.8, 2.0), (-4.8, -2.0), (1.5, -2.4)], lift + 0.6, lift + 2.0, "steel")   # the dart
    k.slab([(5.0, 0.0), (1.2, 1.4), (-3.6, 1.2), (-3.6, -1.2), (1.2, -1.4)], lift + 2.0, lift + 2.4, "paint")   # its back
    for s in (1, -1):
        # lift nacelles either side, lit underneath
        plate(-4.6, 2.6, s * 2.0 - 0.9, s * 2.0 + 0.9 + s * 0.6, lift, lift + 1.5, "plate", inset=0.3, front=0.9)
        k.box(-3.8, 1.8, s * 2.3 - 0.5, s * 2.3 + 0.5, lift - 0.12, lift, "glow")
        lens(6.0 - 2.2, s * 1.2, lift + 1.5, 0.22)
    k.slab([(-1.0, 0.8), (-4.2, 0.8), (-4.2, -0.8), (-1.0, -0.8)], lift + 2.4, lift + 3.2, "plate")   # sensor housing
    k.drum(-2.6, 0, 0.18, lift + 3.2, lift + 5.6, "dark", 6)                           # the mast
    k.drum(-2.2, 0, 1.4, lift + 5.6, lift + 5.9, "white", 12, 1.0)                     # the dish
    k.drum(-2.2, 0, 0.3, lift + 5.9, lift + 6.3, "glow", 6)                             # its feed
    k.box(-4.6, -3.8, -1.6, 1.6, lift + 1.2, lift + 1.8, "glow")                        # thruster glow at the tail
    finish_textured("scout", 24.0, serial="05")


def lighttank():
    """A light tank (the user, 2026-10-20: "a combat tank", cheaper than the tank): small, wide and flat on four wheels
    in armoured fairings, a blunt chamfered nose, a low round-cornered turret in the middle with one long thin gun
    reaching out past the nose. Reads as a small tank (a gun, not the scout's dish; a short boxy hull, not its dart)."""
    k.begin(0.0)
    for s in (1, -1):
        for a in (-3.6, 3.2):                                                           # four wheel fairings
            lo, hi = sorted((s * 2.6, s * 4.2))
            plate(a - 1.8, a + 1.8, lo, hi, 0.0, 2.3, "plate", inset=0.3, front=0.6, back=0.6)
            k.box(a - 1.2, a + 1.2, s * 4.2 - 0.05, s * 4.2 + 0.05, 1.0, 1.2, "glow")
    plate(-5.6, 5.6, -3.4, 3.4, 1.0, 3.0, "steel", inset=0.4, front=1.8, back=0.6)      # the hull, blunt nose
    for s in (1, -1):
        lens(5.62 - 1.0, s * 2.0, 2.0, 0.2)
        strip(-4.6, 2.6, s * 2.4 - 0.25, s * 2.4 + 0.25, 2.98, "paint", 0.1)
    hexagon(-0.4, 0, 2.3, 3.0, 4.4, "steel", r1=1.8, sides=8, turn=math.pi / 8)        # the turret
    hexagon(-0.4, 0, 1.5, 4.4, 4.6, "paint", sides=8, turn=math.pi / 8)
    k.tube(1.4, 9.0, 0, 3.7, 0.24, "mid", 8)                                            # one long thin gun
    k.tube(8.4, 9.2, 0, 3.7, 0.34, "dark", 8)
    vents(-5.2, -3.4, -1.8, 1.8, 2.95, 3)
    finish_textured("lighttank", 24.0, serial="27")


def mortar():
    """A mortar carrier (the user, 2026-10-20: "a smaller artillery tank"): a short tracked box, its back an open
    well holding a stubby fat mortar tube pointing almost straight up; a team band round the tube. Short and squat
    where the artillery is long with a long barrel."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 2.4, 3.9, -5.4, 5.0, 2.6)
    k.box(-5.0, 4.6, -2.4, 2.4, 0.6, 2.6, "dark")
    plate(-5.4, 5.6, -3.6, 3.6, 2.4, 4.4, "steel", inset=0.4, front=1.6, back=0.4)      # the box body
    k.box(-4.4, 0.6, -2.4, 2.4, 4.0, 4.45, "black")                                     # the open well
    for s in (1, -1):
        lens(5.62, s * 1.8, 3.2, 0.24)
        strip(1.2, 4.4, s * 2.6 - 0.25, s * 2.6 + 0.25, 4.35, "paint", 0.1)
    # the mortar: a fat short tube tilted 70 degrees forward, on a base plate in the well
    base, tilt = Vector((-1.9, 0.0, 4.1)), math.radians(70)
    d = Vector((math.cos(tilt), 0.0, math.sin(tilt)))
    k.drum(-1.9, 0, 1.5, 4.0, 4.6, "mid", 10)
    tube_between(base, base + d * 6.5, 0.85, "mid", 10, 0.75)
    tube_between(base + d * 2.2, base + d * 3.4, 1.0, "paint", 10)
    tube_between(base + d * 6.0, base + d * 6.9, 0.95, "dark", 10)
    tube_between(base + d * 4.0, base + d * 4.4, 0.95, "glow", 10)
    for s in (1, -1):                                                                   # ready rounds
        k.drum(-4.0, s * 1.6, 0.35, 4.0, 5.4, "hazard", 6, 0.25)
    finish_textured("mortar", 24.0, serial="44")


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"tank": tank, "artillery": artillery, "scout": scout, "lighttank": lighttank, "mortar": mortar}[which]()
