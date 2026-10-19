using System;

namespace Battler.Sim;

/// <summary>
/// Fixed-point number (16.16 in a long). The simulation uses this instead of float so every
/// machine in a lockstep match computes bit-identical results.
/// </summary>
public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
{
    public const int Shift = 16;
    public const long OneRaw = 1L << Shift;

    public readonly long Raw;

    Fix(long raw) { Raw = raw; }

    public static readonly Fix Zero = new(0);
    public static readonly Fix One = new(OneRaw);

    public static Fix FromRaw(long raw) => new(raw);
    public static Fix FromInt(int v) => new((long)v << Shift);
    /// <summary>num/den, e.g. Ratio(3, 10) = 0.3.</summary>
    public static Fix Ratio(int num, int den) => new(((long)num << Shift) / den);
    /// <summary>Only for turning player input into a command; never call inside the simulation.</summary>
    public static Fix FromFloat(float f) => new((long)MathF.Round(f * OneRaw));

    public static implicit operator Fix(int v) => FromInt(v);

    public static Fix operator +(Fix a, Fix b) => new(a.Raw + b.Raw);
    public static Fix operator -(Fix a, Fix b) => new(a.Raw - b.Raw);
    public static Fix operator -(Fix a) => new(-a.Raw);
    public static Fix operator *(Fix a, Fix b) => new((a.Raw * b.Raw) >> Shift);
    public static Fix operator /(Fix a, Fix b) => new((a.Raw << Shift) / b.Raw);
    public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
    public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
    public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;
    public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
    public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;

    public static Fix Abs(Fix a) => a.Raw < 0 ? -a : a;
    public static Fix Min(Fix a, Fix b) => a.Raw < b.Raw ? a : b;
    public static Fix Max(Fix a, Fix b) => a.Raw > b.Raw ? a : b;

    public static Fix Sqrt(Fix v)
    {
        if (v.Raw <= 0) return Zero;
        return new((long)ISqrt((ulong)v.Raw << Shift));
    }

    static ulong ISqrt(ulong n)
    {
        ulong res = 0, bit = 1UL << 62;
        while (bit > n) bit >>= 2;
        while (bit != 0)
        {
            if (n >= res + bit) { n -= res + bit; res = (res >> 1) + bit; }
            else res >>= 1;
            bit >>= 2;
        }
        return res;
    }

    /// <summary>For rendering only.</summary>
    public float ToFloat() => Raw / (float)OneRaw;
    public int ToInt() => (int)(Raw >> Shift);

    public bool Equals(Fix o) => Raw == o.Raw;
    public override bool Equals(object obj) => obj is Fix f && f.Raw == Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public int CompareTo(Fix o) => Raw.CompareTo(o.Raw);
    public override string ToString() => ToFloat().ToString("0.###");
}

/// <summary>A point or direction on the ground plane (x across, z into the screen).</summary>
public readonly struct FixVec2
{
    public readonly Fix X, Z;

    public FixVec2(Fix x, Fix z) { X = x; Z = z; }

    public static readonly FixVec2 Zero = new(Fix.Zero, Fix.Zero);

    public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new(a.X + b.X, a.Z + b.Z);
    public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new(a.X - b.X, a.Z - b.Z);
    public static FixVec2 operator *(FixVec2 a, Fix s) => new(a.X * s, a.Z * s);

    public Fix LengthSq => X * X + Z * Z;
    public Fix Length => Fix.Sqrt(LengthSq);

    /// <summary>This vector scaled to length <paramref name="len"/> (zero stays zero).</summary>
    public FixVec2 WithLength(Fix len)
    {
        var l = Length;
        if (l.Raw == 0) return Zero;
        return new(X * len / l, Z * len / l);
    }

    public override string ToString() => $"({X}, {Z})";
}
