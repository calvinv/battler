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



# (The user, 2026-10-20, of the textured units: "I like it but they need to be more bulky". So: taller, wider hulls
# sitting on thick track pods, big slab turrets, heavy guns; the same footprints so they fit the game, the same roles.)


def tank():
    """A main battle tank: two thick armoured track pods, a tall slab-sided hull with a short raked glacis, a big
    blocky turret with a heavy gun."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 2.7, 5.4, -8.0, 7.8, 4.2)
    k.box(-7.0, 6.6, -3.0, 3.0, 0.8, 4.2, "dark")                                      # belly between the pods
    plate(-7.9, 7.9, -5.0, 5.0, 3.9, 6.6, "steel", inset=0.35, front=2.4, back=0.7)    # upper hull, tall and wide
    k.upright([(5.2, 3.6), (8.8, 3.0), (8.8, 3.9), (5.2, 4.3)], -4.2, 4.2, "plate")    # nose armour, a heavy lip
    for s in (1, -1):
        strip(-7.0, 3.2, s * 3.7 - 0.4, s * 3.7 + 0.4, 6.55, "paint", 0.1)              # team stripes down the deck
        lens(8.82, s * 3.0, 3.45, 0.32)                                                  # headlamp sensors
        plate(-6.8, 4.0, *sorted((s * 4.9, s * 5.5)), 4.6, 6.2, "plate", inset=0.15)      # applique armour on the flanks
    vents(-7.5, -4.6, -2.8, 2.8, 6.55, 5)                                               # engine grille
    # the turret: a big angular block, raked front, a team panel on the roof, lit strips down its cheeks
    plate(-4.4, 3.2, -3.7, 3.7, 6.6, 9.6, "steel", inset=0.45, front=1.5, back=0.4)
    plate(-3.4, 1.2, -2.5, 2.5, 9.6, 9.95, "paint", inset=0.3)
    for s in (1, -1):
        strip(-3.4, 1.4, s * 3.3 - 0.05, s * 3.3 + 0.05, 8.3, "glow", 0.2)
    plate(1.8, 4.6, -1.3, 1.3, 7.0, 9.0, "plate", inset=0.2, front=0.4)                  # mantlet, thick
    k.tube(4.4, 13.0, 0, 8.0, 0.52, "mid", 10)                                          # the gun, heavy
    k.tube(7.4, 9.6, 0, 8.0, 0.68, "dark", 10)                                          # its sleeve
    k.tube(12.0, 13.4, 0, 8.0, 0.72, "dark", 10)                                        # muzzle
    lens(3.2, 2.4, 9.2, 0.34)                                                           # the turret's sight
    k.box(-4.0, -2.5, -2.2, -0.8, 9.6, 10.5, "dark")                                    # sensor block
    k.drum(-3.6, 2.2, 0.07, 9.6, 13.2, "black", 4)                                      # whip aerial
    k.drum(-3.6, 2.2, 0.16, 13.2, 13.5, "glow", 4)                                      # its tip light
    finish_textured("tank", 32.0, serial="12")


def artillery():
    """Mobile artillery: a long heavy chassis on thick track pods, a big armoured turret at the back carrying a
    twin-rail cannon raised 35 degrees, stabiliser legs braced out behind."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 2.8, 5.2, -8.4, 7.0, 3.8)
    k.box(-8.0, 6.4, -3.0, 3.0, 0.7, 3.8, "dark")
    plate(-8.5, 7.5, -4.9, 4.9, 3.5, 5.4, "steel", inset=0.3, front=2.0, back=0.4)      # long deck, deeper
    for s in (1, -1):
        lens(7.55, s * 2.8, 3.3, 0.3)
        strip(0.0, 6.0, s * 3.8 - 0.35, s * 3.8 + 0.35, 5.35, "paint", 0.1)
        # stabiliser legs: thick struts down and back to pads on the ground
        tube_between((-7.6, s * 3.4, 4.0), (-11.8, s * 4.8, 0.6), 0.45, "mid", 8)
        k.box(-13.0, -10.8, s * 4.8 - 1.1, s * 4.8 + 1.1, 0.0, 0.8, "plate", tail=0.8)
    vents(2.0, 6.2, -2.4, 2.4, 5.35, 4)                                                 # engine grille at the front
    # the turret: a heavy box at the back, its roof in team colour
    plate(-7.4, -0.2, -3.8, 3.8, 5.4, 8.4, "steel", inset=0.45, front=1.1)
    plate(-6.4, -2.2, -2.5, 2.5, 8.4, 8.75, "paint", inset=0.3)
    for s in (1, -1):
        k.box(-4.6, -1.6, s * 1.2 - 0.6 * s, s * 2.9, 7.6, 10.4, "plate")              # trunnion cheeks, big
    # the cannon: twin heavy rails raised 35 degrees, lit coils along them, a big breech
    pivot, tilt, length = Vector((-3.1, 0.0, 9.4)), math.radians(35), 18.0
    d = Vector((math.cos(tilt), 0.0, math.sin(tilt)))
    tube_between(pivot - d * 2.8, pivot + d * 2.2, 1.45, "dark", 10)                    # breech
    for s in (1, -1):
        off = Vector((0.0, s * 0.55, 0.0))
        tube_between(pivot + off + d * 1.0, pivot + off + d * length, 0.42, "mid", 8)   # a rail
    for i in range(4):                                                                  # the coils that throw the shell
        c = pivot + d * (4.2 + i * 3.2)
        tube_between(c - d * 0.45, c + d * 0.45, 0.98, "glow" if i % 2 == 0 else "dark", 10)
    tube_between(pivot + d * (length - 0.7), pivot + d * (length + 0.7), 1.05, "dark", 10)   # muzzle collar
    lens(-0.1, -2.8, 8.6, 0.36)
    finish_textured("artillery", 40.0, serial="31")


def scout():
    """A scout hovercraft: a stout arrowhead hull on two big lift nacelles glowing underneath, a sensor dish on a
    short mast. Unarmed."""
    k.begin(0.0)
    lift = 1.1                                                                          # it floats this high
    k.slab([(6.5, 0.0), (1.5, 2.8), (-4.9, 2.5), (-4.9, -2.5), (1.5, -2.8)], lift + 0.5, lift + 2.6, "steel")   # the dart
    k.slab([(5.0, 0.0), (1.2, 1.7), (-3.7, 1.5), (-3.7, -1.5), (1.2, -1.7)], lift + 2.6, lift + 3.0, "paint")   # its back
    for s in (1, -1):
        # lift nacelles either side, lit underneath
        plate(-4.8, 2.8, s * 2.2 - 1.1, s * 2.2 + 1.1 + s * 0.7, lift, lift + 2.0, "plate", inset=0.3, front=0.9)
        k.box(-4.0, 2.0, s * 2.6 - 0.6, s * 2.6 + 0.6, lift - 0.12, lift, "glow")
        lens(6.0 - 2.2, s * 1.4, lift + 1.9, 0.26)
    k.slab([(-0.8, 1.0), (-4.3, 1.0), (-4.3, -1.0), (-0.8, -1.0)], lift + 3.0, lift + 4.0, "plate")   # sensor housing
    k.drum(-2.6, 0, 0.24, lift + 4.0, lift + 6.4, "dark", 6)                           # the mast
    k.drum(-2.2, 0, 1.6, lift + 6.4, lift + 6.75, "white", 12, 1.15)                   # the dish
    k.drum(-2.2, 0, 0.34, lift + 6.75, lift + 7.2, "glow", 6)                           # its feed
    k.box(-4.8, -4.0, -1.8, 1.8, lift + 1.3, lift + 2.2, "glow")                        # thruster glow at the tail
    finish_textured("scout", 26.0, serial="05")


def lighttank():
    """A light tank: small but stocky on four big wheels in thick armoured fairings, a tall blunt hull, a chunky
    eight-sided turret in the middle with one long gun reaching out past the nose. Reads as a small tank (a gun, not
    the scout's dish; a short boxy hull, not its dart)."""
    k.begin(0.0)
    for s in (1, -1):
        for a in (-3.6, 3.2):                                                           # four wheel fairings, bigger
            lo, hi = sorted((s * 2.5, s * 4.5))
            plate(a - 1.9, a + 1.9, lo, hi, 0.0, 2.8, "plate", inset=0.25, front=0.55, back=0.55)
            k.box(a - 1.3, a + 1.3, s * 4.5 - 0.05, s * 4.5 + 0.05, 1.2, 1.4, "glow")
    plate(-5.7, 5.7, -3.8, 3.8, 1.0, 3.9, "steel", inset=0.3, front=1.5, back=0.5)      # the hull, tall and blunt
    for s in (1, -1):
        lens(5.72 - 1.0, s * 2.3, 2.6, 0.24)
        strip(-4.7, 2.7, s * 2.8 - 0.28, s * 2.8 + 0.28, 3.88, "paint", 0.1)
    hexagon(-0.4, 0, 2.75, 3.9, 5.7, "steel", r1=2.25, sides=8, turn=math.pi / 8)      # the turret, chunky
    hexagon(-0.4, 0, 1.85, 5.7, 5.95, "paint", sides=8, turn=math.pi / 8)
    plate(1.6, 3.0, -0.9, 0.9, 4.2, 5.4, "plate", inset=0.15, front=0.3)                 # mantlet
    k.tube(2.8, 9.4, 0, 4.8, 0.32, "mid", 10)                                           # one long gun, thicker
    k.tube(8.6, 9.6, 0, 4.8, 0.45, "dark", 10)
    vents(-5.3, -3.3, -2.1, 2.1, 3.85, 3)
    finish_textured("lighttank", 26.0, serial="27")


def mortar():
    """A mortar carrier: a short, tall tracked box on thick pods, its back an open well holding a fat mortar tube
    pointing almost straight up; a team band round the tube. Short and squat where the artillery is long."""
    k.begin(0.0)
    for s in (1, -1):
        pod(s, 2.3, 4.3, -5.5, 5.1, 3.2)
    k.box(-5.0, 4.6, -2.4, 2.4, 0.6, 3.2, "dark")
    plate(-5.6, 5.7, -4.0, 4.0, 3.0, 5.6, "steel", inset=0.3, front=1.4, back=0.35)     # the box body, tall
    k.box(-4.5, 0.7, -2.8, 2.8, 5.2, 5.65, "black")                                     # the open well
    for s in (1, -1):
        lens(5.72, s * 2.1, 4.0, 0.28)
        strip(1.2, 4.6, s * 3.0 - 0.28, s * 3.0 + 0.28, 5.55, "paint", 0.1)
    # the mortar: a fat short tube tilted 70 degrees forward, on a base plate in the well
    base, tilt = Vector((-1.9, 0.0, 5.3)), math.radians(70)
    d = Vector((math.cos(tilt), 0.0, math.sin(tilt)))
    k.drum(-1.9, 0, 1.8, 5.2, 5.9, "mid", 12)
    tube_between(base, base + d * 6.8, 1.1, "mid", 12, 0.98)
    tube_between(base + d * 2.2, base + d * 3.5, 1.28, "paint", 12)
    tube_between(base + d * 6.2, base + d * 7.2, 1.22, "dark", 12)
    tube_between(base + d * 4.1, base + d * 4.5, 1.22, "glow", 12)
    for s in (1, -1):                                                                   # ready rounds
        k.drum(-4.0, s * 1.9, 0.42, 5.2, 6.8, "hazard", 6, 0.3)
    finish_textured("mortar", 26.0, serial="44")


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"tank": tank, "artillery": artillery, "scout": scout, "lighttank": lighttank, "mortar": mortar}[which]()
