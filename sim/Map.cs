using System;

namespace Battler.Sim;

/// <summary>
/// Which battlefield this run plays on, fixed when the program starts (the map's shape is built once, up front).
///   - "test": flat dirt, no water or trees, the bases 120 apart (the user, 2026-10-22: "a new basic map that's just
///     dirt and the bases are much closer so I can test"). The default for now.
///   - "main": the full map from the user's sketch: the two lakes, the hills, the bases about 490 apart.
/// Chosen by "--map main" (or "--map test") on the command line, or the BATTLER_MAP environment variable.
/// </summary>
public static class Map
{
    public static readonly string Name = Pick();
    public static bool Test => Name == "test";

    static string Pick()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--map");
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        return Environment.GetEnvironmentVariable("BATTLER_MAP") is { Length: > 0 } env ? env : "test";
    }
}
