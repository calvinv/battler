namespace Battler.Sim;

/// <summary>
/// The HQ's tech tree (the user, 2026-10-24: "lets have the production things be like a tech tree in a submenu").
/// Each branch unlocks units and side-wide upgrades, and adds its own add-on to the pyramid, so the base shows what
/// it makes. Research runs one at a time and is paid when it starts.
/// </summary>
public enum Branch { Armour, Walkers, Guns, Air, Support, Core }

/// <summary>Every research. Append only: the wire sends the numbers.</summary>
public enum Tech
{
    Wheel, Plating1, Plating2,
    Servos1, Servos2, Magnet,
    Lobber, Gunnery1, LongLeg, Gunnery2, Napalm,
    Dart, Blimp, Hive,
    Mender, Bulwark, ShieldCoil,
    Door2, Door3, Door4, Tooling1, Tooling2, Twin,
}

/// <param name="Lane">which of its branch's columns it sits in (0 or 1) on the tree screen</param>
/// <param name="Row">how far down its branch</param>
/// <param name="Needs">what has to be researched first</param>
/// <param name="Unlocks">a unit it lets the HQ build</param>
/// <param name="Gives">a side-wide upgrade (one more of that Mod) for units made from then on</param>
public sealed record TechInfo(Tech Id, Branch Branch, int Lane, int Row, string Name, int Cost, int Seconds, Tech? Needs, Kind? Unlocks, Mod? Gives, string Does);

public static class Techs
{
    public static readonly TechInfo[] All =
    {
        new(Tech.Wheel, Branch.Armour, 0, 0, "Battle wheel", 250, 30, null, Kind.Tank, null, "Unlocks the battle wheel: tough, a heavy cannon."),
        new(Tech.Plating1, Branch.Armour, 0, 1, "Plating I", 250, 30, Tech.Wheel, null, Mod.Armour, "+25% health for every unit made from now on."),
        new(Tech.Plating2, Branch.Armour, 0, 2, "Plating II", 450, 45, Tech.Plating1, null, Mod.Armour, "+25% more health."),

        new(Tech.Servos1, Branch.Walkers, 0, 0, "Servos I", 200, 25, null, null, Mod.Speed, "+20% speed for every unit made from now on."),
        new(Tech.Servos2, Branch.Walkers, 0, 1, "Servos II", 350, 35, Tech.Servos1, null, Mod.Speed, "+20% more speed."),
        new(Tech.Magnet, Branch.Walkers, 0, 2, "Salvage magnet", 300, 35, Tech.Servos2, null, Mod.Magnet, "Kills pay 50% more salvage."),

        new(Tech.Lobber, Branch.Guns, 0, 0, "Lobber", 250, 30, null, Kind.Mortar, null, "Unlocks the lobber: a mortar, lobs shells with a small blast."),
        new(Tech.Gunnery1, Branch.Guns, 0, 1, "Gunnery I", 300, 30, Tech.Lobber, null, Mod.Attack, "+25% damage for every unit made from now on."),
        new(Tech.LongLeg, Branch.Guns, 1, 1, "Long leg", 400, 40, Tech.Lobber, Kind.Artillery, null, "Unlocks the long leg: artillery, outranges everything, helpless up close."),
        new(Tech.Gunnery2, Branch.Guns, 0, 2, "Gunnery II", 500, 45, Tech.Gunnery1, null, Mod.Attack, "+25% more damage."),
        new(Tech.Napalm, Branch.Guns, 1, 2, "Napalm", 300, 35, Tech.LongLeg, null, Mod.Napalm, "Lobbers and long legs fire napalm: hits do a quarter less but leave the ground burning."),

        new(Tech.Dart, Branch.Air, 0, 0, "Dart", 250, 30, null, Kind.Flier, null, "Unlocks the dart: a fast interceptor that hunts fliers and guards the base."),
        new(Tech.Blimp, Branch.Air, 0, 1, "Blimp", 400, 40, Tech.Dart, Kind.Bomber, null, "Unlocks the blimp: a bomber, heavy bombs on looping runs."),
        new(Tech.Hive, Branch.Air, 0, 2, "Hive", 400, 40, Tech.Blimp, Kind.Hive, null, "Unlocks the hive: a carrier whose drones sting from range, fliers too."),

        new(Tech.Mender, Branch.Support, 0, 0, "Mender", 250, 30, null, Kind.Mender, null, "Unlocks the mender: repair drones fly out and mend your units."),
        new(Tech.Bulwark, Branch.Support, 0, 1, "Bulwark", 400, 40, Tech.Mender, Kind.Bulwark, null, "Unlocks the bulwark: a walker whose dome cuts damage to all under it."),
        new(Tech.ShieldCoil, Branch.Support, 0, 2, "Shield coil", 400, 40, Tech.Bulwark, null, Mod.Shield, "Every unit made from now on carries a shield of 40% its health."),

        new(Tech.Door2, Branch.Core, 0, 0, "Second door", 300, 30, null, null, null, "The HQ builds two units at once."),
        new(Tech.Door3, Branch.Core, 0, 1, "Third door", 500, 45, Tech.Door2, null, null, "Three at once."),
        new(Tech.Door4, Branch.Core, 0, 2, "Fourth door", 800, 60, Tech.Door3, null, null, "Four at once."),
        new(Tech.Tooling1, Branch.Core, 1, 0, "Tooling I", 250, 30, null, null, Mod.Build, "Every unit builds 15% quicker."),
        new(Tech.Tooling2, Branch.Core, 1, 1, "Tooling II", 450, 45, Tech.Tooling1, null, Mod.Build, "15% quicker again."),
        new(Tech.Twin, Branch.Core, 1, 2, "Twin feed", 500, 45, Tech.Tooling2, null, Mod.Twin, "Every third unit comes out with a free twin."),
    };

    public static int Count => All.Length;
    public static TechInfo Of(Tech t) => All[(int)t];

    /// <summary>What the HQ builds before any research: the hover tank, the rifle mech and the scout.</summary>
    public static readonly Kind[] Starting = { Kind.LightTank, Kind.Ranger, Kind.Scout };

    /// <summary>Everything the HQ can ever build, in card order (scout last).</summary>
    public static readonly Kind[] Buildable =
        { Kind.LightTank, Kind.Ranger, Kind.Tank, Kind.Mortar, Kind.Artillery, Kind.Flier, Kind.Bomber, Kind.Hive, Kind.Mender, Kind.Bulwark, Kind.Scout };

    /// <summary>The research that unlocks a unit, or null when it's there from the start.</summary>
    public static Tech? UnlockFor(Kind k)
    {
        foreach (var t in All) if (t.Unlocks == k) return t.Id;
        return null;
    }
}
