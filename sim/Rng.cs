namespace Battler.Sim;

/// <summary>Seeded random numbers (splitmix64). The same seed gives the same sequence everywhere.</summary>
public sealed class Rng
{
    ulong _state;

    public Rng(ulong seed) { _state = seed; }

    public ulong State => _state;

    public ulong NextULong()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>0 &lt;= result &lt; max.</summary>
    public int Next(int max) => (int)(NextULong() % (ulong)max);

    /// <summary>min &lt;= result &lt;= max.</summary>
    public int Range(int min, int max) => min + Next(max - min + 1);
}
