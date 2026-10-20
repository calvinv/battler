using System;

namespace Battler.Sim;

/// <summary>
/// The battlefield's shape: a fixed heightmap, the same every match and on every machine (fixed-point, built from an
/// integer hash, no floats). Height matters (the user, 2026-10-19: "hills should matter"): see World's HighGround.
///
/// The layout is the user's sketch (2026-10-20, ../untitled.png), the same turned half round about the middle, so
/// neither side has the better map:
///  - water north and south: the north water fills the top left and thins to a strip toward the top right (its shore
///    curves down out of the top-left corner, then runs straight up to the top-right corner); the south water is the
///    same turned round. What's left is a band of land running corner to corner.
///  - each HQ on a hill in its corner (bottom left, top right), a flat top to build on;
///  - a long hill in the middle with a peak: the high ground both sides want;
///  - a valley across the band between each base and the middle hill: low ground to cross under the hill's guns.
/// Water can't be crossed (Passable; units find their way round: Nav).
/// The game draws the same heights (<see cref="HeightAtF"/>), out past the map's edge by <see cref="Margin"/>.
/// </summary>
public sealed class Ground
{
    public const int Margin = 20;

    /// <summary>No small ponds on this map (kept for the drawing code, which places any there are).</summary>
    public static readonly (FixVec2 At, Fix R)[] Ponds = { };

    // The middle hill: a rounded box HillHalfX by HillHalfZ (flat-topped to HillHigh), its sides HillSlope wide, a peak
    // on top. The valleys: troughs ValleyHalf wide either side of x = ValleyX, from ValleyZ0 to ValleyZ1 (player 0's;
    // the other is the same turned round), ValleyDeep down.
    static readonly Fix HillHalfX = 50, HillHalfZ = 21, HillSlope = 14, HillHigh = 6, PeakHigh = 3;
    static readonly Fix ValleyX = -77, ValleyHalf = 13, ValleyZ0 = -22, ValleyZ1 = 54, ValleyDeep = 3;
    static readonly Fix BaseHill = 4;   // how high each HQ's hill stands

    /// <summary>Where the north water's shore is at x: water lies north of it (smaller z). Out of the top-left corner it
    /// curves down to (-63, -24), then runs straight up to the top-right corner.</summary>
    public static Fix NorthShore(Fix x)
    {
        if (x <= -63)
        {
            var t = Fix.Max(Fix.Zero, Fix.Min(Fix.One, (x + 140) / 77));
            var ease = Fix.One - (Fix.One - t) * (Fix.One - t);
            return -85 + 61 * ease;
        }
        return -24 - 61 * (x + 63) / 203;
    }

    /// <summary>The south water's shore: the north one turned round (water south of it, larger z).</summary>
    public static Fix SouthShore(Fix x) => -NorthShore(-x);

    /// <summary>How far out into the water a place is, square to the shore (negative on land: how far from it).</summary>
    public static Fix WaterDepth(FixVec2 p)
    {
        Fix Across(Func<Fix, Fix> shore)   // the shore's slope, to measure square to it rather than straight north
        {
            var m = (shore(p.X + 1) - shore(p.X - 1)) / 2;
            return Fix.Sqrt(Fix.One + m * m);
        }
        var north = (NorthShore(p.X) - p.Z) / Across(NorthShore);
        var south = (p.Z - SouthShore(p.X)) / Across(SouthShore);
        return Fix.Max(north, south);
    }

    /// <summary>Whether ground units can be here: not in the water (nor its very edge).</summary>
    public static bool Passable(FixVec2 p) => WaterDepth(p) < Fix.Ratio(-1, 2);

    readonly int _x0, _z0, _w, _h;   // the grid: one height per world unit, from (_x0, _z0)
    readonly Fix[] _heights;

    public Ground(int halfX, int halfZ, FixVec2[] bases)
    {
        _x0 = -halfX - Margin;
        _z0 = -halfZ - Margin;
        _w = (halfX + Margin) * 2 + 1;
        _h = (halfZ + Margin) * 2 + 1;
        _heights = new Fix[_w * _h];
        for (int j = 0; j < _h; j++)
            for (int i = 0; i < _w; i++)
                _heights[j * _w + i] = Shape(_x0 + i, _z0 + j, bases);
    }

    static Fix Shape(int x, int z, FixVec2[] bases)
    {
        // Gentle rolling ground: three octaves of value noise, about +-3. The same turned round about the middle:
        // every place on player 1's half takes its noise from the matching place on player 0's.
        bool turn = x > 0 || (x == 0 && z > 0);
        int nx = turn ? -x : x, nz = turn ? -z : z;
        var n = Value(nx, nz, 24, 0) + Value(nx, nz, 12, 1) * Fix.Ratio(1, 2) + Value(nx, nz, 6, 2) * Fix.Ratio(1, 4);
        var h = n * Fix.Ratio(12, 7);
        var p = new FixVec2(x, z);
        var q = turn ? new FixVec2(-x, -z) : p;   // the place on player 0's half that matches this one

        // Each HQ's hill: flat on top where the base builds, sloping off.
        foreach (var b in bases)
        {
            var d = (p - b).Length;
            h *= SmoothStep(24, 36, d);
            h += BaseHill * SmoothStep(38, 24, d);
        }
        // The middle hill: a rounded box, a peak along its middle.
        var bx = Fix.Max(Fix.Zero, Fix.Abs(p.X) - HillHalfX);
        var bz = Fix.Max(Fix.Zero, Fix.Abs(p.Z) - HillHalfZ);
        var off = Fix.Sqrt(bx * bx + bz * bz);
        h += HillHigh * SmoothStep(HillSlope, Fix.Zero, off);
        h += PeakHigh * SmoothStep(36, 0, Fix.Abs(p.X)) * SmoothStep(16, 0, Fix.Abs(p.Z));
        // The valley on this half (player 0's, turned round on the other): a trough across the band.
        var across = SmoothStep(ValleyHalf, ValleyHalf - 7, Fix.Abs(q.X - ValleyX));
        var along = SmoothStep(ValleyZ0 - 8, ValleyZ0, q.Z) * SmoothStep(ValleyZ1 + 8, ValleyZ1, q.Z);
        h -= ValleyDeep * across * along;
        // The water: the ground falls away under it.
        var depth = WaterDepth(p);
        if (depth > -4) h = Lerp(h, Fix.FromInt(-4), SmoothStep(-4, 8, depth));
        return h;
    }

    /// <summary>Smoothly interpolated noise on a lattice `spacing` apart, -1 to 1.</summary>
    static Fix Value(int x, int z, int spacing, int octave)
    {
        int gx = FloorDiv(x, spacing), gz = FloorDiv(z, spacing);
        var fx = Smooth(Fix.Ratio(x - gx * spacing, spacing));
        var fz = Smooth(Fix.Ratio(z - gz * spacing, spacing));
        var top = Lerp(Lattice(gx, gz, octave), Lattice(gx + 1, gz, octave), fx);
        var bottom = Lerp(Lattice(gx, gz + 1, octave), Lattice(gx + 1, gz + 1, octave), fx);
        return Lerp(top, bottom, fz);
    }

    static Fix Lattice(int x, int z, int octave)
    {
        uint h = unchecked((uint)(x * 374761393 + z * 668265263 + octave * 1442695041 + 7));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        h ^= h >> 16;
        return Fix.FromRaw((long)(h & 0xFFFF) * 2 - Fix.OneRaw);
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    static Fix Smooth(Fix t) => t * t * (3 - 2 * t);
    static Fix Lerp(Fix a, Fix b, Fix t) => a + (b - a) * t;
    static Fix SmoothStep(Fix e0, Fix e1, Fix x)
    {
        var t = Fix.Max(Fix.Zero, Fix.Min(Fix.One, (x - e0) / (e1 - e0)));
        return Smooth(t);
    }

    Fix At(int i, int j) => _heights[Math.Clamp(j, 0, _h - 1) * _w + Math.Clamp(i, 0, _w - 1)];

    /// <summary>The ground's height at a place (between grid points, blended).</summary>
    public Fix HeightAt(FixVec2 p)
    {
        var gx = p.X - _x0;
        var gz = p.Z - _z0;
        int i = gx.ToInt(), j = gz.ToInt();   // (rounds down: ToInt shifts)
        var fx = gx - i;
        var fz = gz - j;
        var top = Lerp(At(i, j), At(i + 1, j), fx);
        var bottom = Lerp(At(i, j + 1), At(i + 1, j + 1), fx);
        return Lerp(top, bottom, fz);
    }

    /// <summary>The same in floats, for drawing only (never inside the sim).</summary>
    public float HeightAtF(float x, float z)
    {
        float gx = x - _x0, gz = z - _z0;
        int i = (int)MathF.Floor(gx), j = (int)MathF.Floor(gz);
        float fx = gx - i, fz = gz - j;
        float F(int a, int b) => At(a, b).ToFloat();
        float top = F(i, j) + (F(i + 1, j) - F(i, j)) * fx;
        float bottom = F(i, j + 1) + (F(i + 1, j + 1) - F(i, j + 1)) * fx;
        return top + (bottom - top) * fz;
    }

    /// <summary>WaterDepth in floats, for drawing only.</summary>
    public static float WaterDepthF(float x, float z) => WaterDepth(new FixVec2(Fix.FromFloat(x), Fix.FromFloat(z))).ToFloat();
}
