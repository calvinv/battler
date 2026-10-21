"""Battler's units, the Coalition's tier 1: model_units.py -- <tank|artillery|scout|lighttank|mortar|spider>
Low-poly "wedge" style (the user, 2026-10-21: "I want a complete rework on models. Low poly models, the simpler the
better"; of the mock-ups, "wedge works"): a few blocks each with sloped faces, raked fronts and tapered turrets, flat
colour with the bake's shading, a team-colour panel on top where the camera looks, a thin cyan light down each side.
A few hundred triangles each. No crew features (the user: "no humans").
Built with ai-tools' kit (C:/dev/ai-tools/blender/ship_kit.py): `a` along it (front +), `x` across, `z` up from the
ground. "paint" is the team colour (alpha 0 in the texture: the game recolours it).
Run: blender --background --python tools/art/model_units.py -- tank
-> art/units/<name>.glb and <name>.png; art/_gen/ships_iso/work/<name>_model_views.png to judge."""
import sys
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
from unit_parts import *      # the palette, Blender's kit as k, and the shared pieces

# Gritty (the user, 2026-10-22: "rounder cannon. gritty look. tracks at least"): worn grey-green steel, darker.
k.recolour(steel=(150, 152, 142), plate=(118, 121, 112), mid=(96, 98, 94), dark=(52, 54, 52), black=(26, 26, 25))


def tracks(s, inner, outer, a0, a1, h):
    """A real track on side `s`: the rubber belt seen from the side with rounded ends, tread bars across its top run,
    road wheels and a toothed drive sprocket on its outer face, an armour skirt over the top half."""
    x0, x1 = sorted((s * inner, s * outer))
    xo = s * outer
    r = h * 0.5
    outline = [(a0 + r * (1 - math.cos(t)), r - r * math.sin(t)) for t in [i * math.pi / 6 for i in range(-3, 4)]]
    outline = [(a, z) for a, z in outline]   # back end, bottom to top
    front = [(a1 - r * (1 - math.cos(t)), r + r * math.sin(t)) for t in [i * math.pi / 6 for i in range(-3, 4)]]
    k.upright(list(reversed(outline)) + front[::-1], x0, x1, "black")                  # the belt
    for i in range(int((a1 - a0 - h) / 0.75)):                                          # tread bars on the top run
        a = a0 + r + 0.2 + i * 0.75
        k.box(a, a + 0.3, x0 - 0.04, x1 + 0.04, h - 0.02, h + 0.14, "dark")
    n = max(3, int((a1 - a0 - h) / (h * 0.75)))
    for i in range(n):                                                                  # road wheels
        a = a0 + r + i * (a1 - a0 - h) / max(n - 1, 1)
        tube_between((a, xo - s * 0.05, r * 0.85), (a, xo + s * 0.12, r * 0.85), r * 0.62, "mid", 10)
        tube_between((a, xo + s * 0.12, r * 0.85), (a, xo + s * 0.2, r * 0.85), r * 0.22, "dark", 8)
    tube_between((a1 - r, xo - s * 0.05, r * 1.05), (a1 - r, xo + s * 0.16, r * 1.05), r * 0.72, "dark", 8)   # sprocket
    plate(a0 + 0.4, a1 - 0.3, x0 - 0.05, x1 + 0.05, h * 0.62, h + 0.22, "plate", inset=0.1, front=0.6, back=0.4)  # skirt


def gun(a0, a1, z, r, muzzle=True):
    """A round gun barrel along the hull's middle at height z, a thicker sleeve near its root and a muzzle brake."""
    k.tube(a0, a1, 0, z, r, "dark", 12)
    k.tube(a0 + (a1 - a0) * 0.25, a0 + (a1 - a0) * 0.45, 0, z, r * 1.35, "mid", 12)
    if muzzle:
        k.tube(a1 - 0.9, a1 + 0.2, 0, z, r * 1.6, "dark", 12)


def tank():
    """Main battle tank: wide tracks, a raked hull, a tapered turret with a long square gun."""
    k.begin(0.0)
    for s in (1, -1):
        tracks(s, 3.6, 5.6, -7.0, 7.0, 2.9)
    plate(-7.5, 7.8, -4.2, 4.2, 1.3, 4.8, "steel", inset=0.45, front=3.0, back=0.8)
    plate(-3.8, 3.0, -3.4, 3.4, 4.8, 7.2, "steel", inset=0.7, front=1.5, back=0.35)
    plate(-2.8, 1.0, -2.2, 2.2, 7.2, 7.45, "paint", inset=0.15)
    k.tube(2.2, 3.6, 0, 6.05, 1.0, "plate", 10)                                          # mantlet
    gun(3.4, 12.6, 6.05, 0.42)
    finish_realistic("tank", 30.0, serial="12")


def lighttank():
    """Light tank: small and quick, a low raked hull, a little turret, one thin gun."""
    k.begin(0.0)
    for s in (1, -1):
        tracks(s, 2.9, 4.5, -4.7, 4.7, 2.0)
    plate(-5.1, 5.4, -3.3, 3.3, 1.1, 3.3, "steel", inset=0.4, front=2.1, back=0.6)
    plate(-2.1, 1.8, -2.1, 2.1, 3.3, 4.7, "steel", inset=0.45, front=1.0)
    plate(-1.4, 0.9, -1.35, 1.35, 4.7, 4.88, "paint", inset=0.1)
    k.tube(1.4, 2.4, 0, 4.0, 0.62, "plate", 10)                                          # mantlet
    gun(2.2, 8.2, 4.0, 0.26)
    finish_realistic("lighttank", 24.0)


def artillery():
    """Artillery: a long low hull, the gun house at the back, a very long barrel raised steeply."""
    k.begin(0.0)
    for s in (1, -1):
        tracks(s, 3.4, 5.4, -7.5, 7.0, 2.6)
    plate(-7.9, 7.6, -4.0, 4.0, 1.1, 4.0, "steel", inset=0.4, front=2.4, back=0.5)
    plate(-7.2, -0.4, -3.4, 3.4, 4.0, 6.8, "steel", inset=0.7, front=1.6)
    plate(-6.0, -2.2, -2.2, 2.2, 6.8, 7.05, "paint", inset=0.15)
    tube_between((-3.6, 0, 6.0), (9.4, 0, 15.2), 0.55, "dark", 12)                    # the barrel, round
    tube_between((-2.0, 0, 7.1), (1.6, 0, 9.7), 0.85, "mid", 12)                       # recoil sleeve
    tube_between((8.4, 0, 14.5), (9.9, 0, 15.6), 0.9, "dark", 12)                     # muzzle brake
    for s in (1, -1):                                                                   # spades folded at the back
        plate(-9.4, -7.6, *sorted((s * 2.2, s * 3.6)), 0.0, 1.0, "plate", inset=0.15, back=0.6)
    finish_realistic("artillery", 36.0)


def mortar():
    """Mortar carrier: a short tall box, a fat tube standing nearly upright in its back."""
    k.begin(0.0)
    for s in (1, -1):
        tracks(s, 2.8, 4.5, -5.0, 4.8, 2.4)
    plate(-5.4, 5.4, -3.5, 3.5, 1.1, 4.6, "steel", inset=0.4, front=2.0, back=0.4)
    plate(1.2, 4.2, -2.2, 2.2, 4.6, 4.8, "paint", inset=0.15)
    hexagon(-1.9, 0, 1.9, 4.6, 5.3, "plate", r1=1.6)                                   # the base ring
    tube_between((-1.9, 0, 5.0), (0.2, 0, 11.0), 1.1, "dark", 14, 0.95)                 # the tube, fat and short
    tube_between((-1.2, 0, 7.0), (-1.0, 0, 7.6), 1.25, "mid", 14)                      # a band round it
    finish_realistic("mortar", 26.0)


def scout():
    """Scout: a small dart on two hover pods, lit underneath, a sensor dish on a short mast. Unarmed."""
    k.begin(0.0)
    k.slab([(5.4, 0.0), (1.2, 2.2), (-3.8, 1.9), (-3.8, -1.9), (1.2, -2.2)], 1.2, 2.8, "steel")
    k.slab([(3.6, 0.0), (0.7, 1.2), (-2.8, 1.1), (-2.8, -1.1), (0.7, -1.2)], 2.8, 3.0, "paint")
    for s in (1, -1):
        plate(-3.8, 2.4, *sorted((s * 1.9, s * 3.2)), 0.6, 1.8, "plate", inset=0.2, front=0.9)
        k.box(-3.2, 1.8, *sorted((s * 2.1, s * 3.0)), 0.45, 0.6, "glow")
    k.box(-2.1, -1.7, -0.22, 0.22, 3.0, 5.2, "dark")
    hexagon(-1.7, 0, 1.4, 5.2, 5.45, "steel", r1=1.05)
    finish_realistic("scout", 22.0)


def spider():
    """Spider bot: a squat wedge body on four plain legs, a small turret with a twin laser. (A walker for now; it may
    move to the Lattice.)"""
    k.begin(0.0)
    plate(-3.4, 3.4, -2.8, 2.8, 2.4, 4.6, "steel", inset=0.4, front=1.0, back=0.6)
    plate(-2.2, 2.0, -1.8, 1.8, 4.6, 4.8, "paint", inset=0.15)
    for fa in (1, -1):
        for s in (1, -1):
            tube_between((fa * 2.4, s * 2.2, 3.4), (fa * 4.4, s * 4.4, 5.0), 0.6, "plate", 4)   # thigh
            tube_between((fa * 4.4, s * 4.4, 5.0), (fa * 5.2, s * 5.4, 0.0), 0.5, "dark", 4)    # shin
    plate(-1.5, 1.6, -1.4, 1.4, 4.8, 6.1, "steel", inset=0.3, front=0.6)
    for s in (1, -1):
        k.box(1.2, 5.4, s * 0.42 - 0.17, s * 0.42 + 0.17, 5.3, 5.64, "dark")
        k.box(5.2, 5.6, s * 0.42 - 0.2, s * 0.42 + 0.2, 5.27, 5.67, "glow")              # lit emitter tips
    finish_realistic("spider", 24.0)


which = sys.argv[sys.argv.index("--") + 1]      # one per run: the kit starts Blender empty when imported
{"tank": tank, "artillery": artillery, "scout": scout, "lighttank": lighttank, "mortar": mortar, "spider": spider}[which]()
