using System;

namespace Battler.Sim;

/// <summary>
/// The battlefield's shape: a fixed heightmap, the same every match and on every machine (fixed-point, built from an
/// integer hash, no floats). Height matters (the user, 2026-10-19: "hills should matter"): see World's HighGround.
/// Rolling hills, flat where the bases build, gentle down the middle lane where the armies meet, dips for ponds.
/// The game draws the same heights (<see cref="HeightAtF"/>), out past the map's edge by <see cref="Margin"/>.
/// </summary>
public sealed class Ground
{
    public const int Margin = 20;
    public static readonly (FixVec2 At, Fix R)[] Ponds =
    {
        // Mirrored left to right like the rest of the ground, so neither side has the better map.
        (new FixVec2(28, 62), 6), (new FixVec2(-28, 62), 6), (new FixVec2(60, -66), 5), (new FixVec2(-60, -66), 5),
        (new FixVec2(95, -42), 5), (new FixVec2(-95, -42), 5), (new FixVec2(105, 62), Fix.Ratio(9, 2)), (new FixVec2(-105, 62), Fix.Ratio(9, 2)),
        (new FixVec2(48, 24), 4), (new FixVec2(-48, 24), 4), (new FixVec2(20, -60), 5), (new FixVec2(-20, -60), 5),
    };

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
        // Three octaves of value noise, about +-5 high.
        // Mirrored about the middle (x = 0): each side's half of the map is the other's reflection, for fairness.
        int mx = Math.Abs(x);
        var n = Value(mx, z, 24, 0) + Value(mx, z, 12, 1) * Fix.Ratio(1, 2) + Value(mx, z, 6, 2) * Fix.Ratio(1, 4);
        var h = n * Fix.Ratio(20, 7);
        var p = new FixVec2(x, z);
        foreach (var b in bases) h *= SmoothStep(27, 38, (p - b).Length);                  // flat where bases build
        h *= Lerp(Fix.Ratio(3, 10), Fix.One, SmoothStep(6, 20, Fix.Abs(p.Z)));             // gentle in the lane
        foreach (var (at, r) in Ponds)
        {
            var d = (p - at).Length;
            if (d < r + 5) h = Lerp(Fix.Ratio(-17, 10), h, SmoothStep(r - Fix.Ratio(3, 2), r + 5, d));
        }
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
}
