using System.Collections.Generic;

namespace Battler.Sim;

public enum Kind { Hq, Factory, Scout, Tank, Artillery, Tower, Outpost, Extractor, LightTank, Mortar, Spider, Crawler, Piercer, Drone, Node, MobileBase, Turret, Flak, Bombard, Wall, Feral, FeralBrute, Derelict, Ranger, Flier, Bomber, PowerPlant, Module, Mender, Bulwark, Hive }   // (new kinds go at the end: StatsFor and the wire use the numbers)

/// <summary>A factory's upgrade tracks. They improve the units it makes from then on (not ones already out).</summary>
public enum Upgrade { Production, Damage, Armour }

/// <summary>What a deposit gives (the user, 2026-10-22: resources "impact the way your units can fit and offer upgrades,
/// like you can find a sulfur patch that could increase explosive damage for all units or give you access to napalm").
/// Metal is income; the others are mined for what having them does, not stockpiled.</summary>
public enum Resource { Metal, Sulfur, Crystal, Oil }

/// <summary>What a factory fits its units with. Napalm needs sulfur: hits do a quarter less, but leave the ground
/// burning (World.Fires).</summary>
public enum Fitting { Standard, Napalm }

/// <summary>What a module does for the wings after it on its line (the user, 2026-10-23: "I want this to be like a
/// rube goldberg machine ... armoury upgrade, then whatever comes after has more firepower. the modules should be speed,
/// armour, attack, build speed, energy cost, wildcards"). Stat modules stack; the wildcards change what comes out.</summary>
public enum Mod { Speed, Armour, Attack, Build, Energy, Twin, Napalm, Shield, Magnet, Blank }   // (a blank does nothing: it carries the line on)

public sealed class Stats
{
    public int MaxHp;
    public Fix Radius;
    public Fix Speed;      // ground units per tick; zero for buildings
    public Fix Range;      // weapon reach, measured edge to edge
    public Fix MinRange;   // can't fire at anything nearer than this (artillery)
    public Fix Aggro;      // how far it looks for targets (it can only target what its side sees)
    public Fix Vision;     // how far it sees, edge to edge; its whole side shares what it sees
    public Fix Splash;     // shots also hit enemies this close to the target
    public int Damage;
    public int Reload;     // ticks between shots; zero = unarmed
    public int BuildTicks;    // a unit: how often its factory makes one
    public bool Flying;       // flies: crosses water and hills, and only HitsAir guns can reach it
    public bool HitsAir;      // its gun can hit fliers
    public bool AirOnly;      // and only fliers (the interceptor)
    public bool Armoured;     // heavy plate: anti-armour weapons do extra to it
    public int VsArmour = 100;   // percent of its damage it does to Armoured targets
    public int VsAir = 100;      // percent of its damage it does to fliers
    public bool Explosive;    // shells, cannon and spikes: sulfur makes these hit harder
    public bool Heals;        // its "shots" mend friendly units instead (Damage is health given back): the Mender
    public bool Shields;      // throws a dome over its side's things near it: they take less damage (the Bulwark)

    public bool IsBuilding => Speed.Raw == 0;
    public bool IsUnit => !IsBuilding;
}

public sealed class Entity
{
    public int Id;
    public Kind Kind;
    public int Owner;
    public FixVec2 Pos;
    public int Hp;
    public int Cooldown;   // ticks until it can fire again
    public int Timer;      // factory: ticks until the next unit
    public Kind Makes;     // factory: the unit it makes
    public bool Dead;
    public int SeenBy;     // a bit per player whose side can see it (World.UpdateVision)
    public Fix Height;     // of the ground under it (World.Settle)
    // A factory's upgrade levels by Upgrade; a unit carries its factory's at the time it was made.
    public readonly int[] Levels = new int[3];
    public int MaxHp;      // with armour upgrades
    public int Damage;     // with damage upgrades and veterancy
    public int Kills;      // what it has finished off
    public int Rank;       // veterancy, 0 to World.MaxRank: every World.KillsPerRank kills
    public int FactoryId;  // a unit: the factory that made it
    public bool HasWaypoint;   // a unit: its factory's rally point when it was made, to reach before attacking
    public FixVec2 Waypoint;
    public bool HasRally;  // a factory, or the HQ for the whole base: whether new units gather at Rally before attacking
    public FixVec2 Rally;
    public int BaseId;     // a wing (factory): the HQ or set-up mobile base it's built onto
    public int Deposit = -1;   // an extractor: which of World.Deposits it sits on
    public int Slot;           // a wing (factory): which of its base's wing spots it fills
    public readonly List<int> Orders = new();
    public int PowerAcc;       // a wing short of power: what it has banked toward its next tick of work (World.Work)
    public int Vent = -1;      // a power plant: which of World.Vents it stands on   // a utility wing: what's paid for and waiting to be built (by Kind), in order
    public readonly List<int> UpgradeQueue = new();   // a factory or outpost: upgrades (by Upgrade) to buy, in order, once affordable
    public List<FixVec2> NavPath;      // a unit: corners to drive through to NavGoal (Nav), when there is no straight way
    public FixVec2 NavGoal;
    public FixVec2 Heading;            // a ground unit: which way it's driving (World.Steer turns it a little at a time)
    public int SwaySide;               // a ground unit going round friends: the side it chose (World.FanOut), kept while blocked
    public int FlankTicks;             // a ground unit swinging round to a flank (World.ChaseRound): ticks it keeps at it
    public int NavTick;                // when that path was found
    public int NavVersion;             // the buildings as they stood when it was found (Nav.Version)
    public int Building;               // a building: ticks until it's finished (World.ConstructionTicks); it does nothing till then
    public Fitting Fitting;    // a factory: what it fits its units with; a unit: what it was made with
    public bool Enhanced;      // a unit made by a wing on a sulfur extractor: hits harder and sets fires
    public Mod Mod;            // a module: what it does
    public int Link;           // a wing or module: how far along its line it sits (0 against the host)
    public readonly int[] Mods = new int[World.ModCount];   // a wing: the modules before it on its line, by Mod; a unit: its wing's when it was made
    public int Shield;         // a unit from behind a shield coil: damage it soaks before its health
    public int Made;           // a wing: units it has made (the twin feed doubles every few)

    public int Level(Upgrade u) => Levels[(int)u];

    public Stats Stats => World.StatsFor[(int)Kind];
}

/// <summary>The three civilisations (the user, 2026-10-21): the Coalition (versatile), the Lattice (swarm), the
/// Ascendancy (tech, not built yet).</summary>
public enum Civ { Coalition, Lattice, Ascendancy }

public sealed class Player
{
    public int Id;
    public Civ Civ;
    public int Metal;
    public int PausesLeft;
    public int Mined, Salvaged;   // metal earned so far: from the trickle and extractors, and from salvage
    public FixVec2 HqPos;
    /// <summary>Enemies this side has seen, by id, at where they were last seen (World.UpdateVision keeps it).</summary>
    public readonly SortedDictionary<int, FixVec2> Known = new();
    /// <summary>Which of those fly: only units that can shoot up go after them (the user, 2026-10-22: a flying scout
    /// was "distracting my tanks, they should ignore things they can't shoot").</summary>
    public readonly HashSet<int> KnownFliers = new();
    /// <summary>The last radar sweep: where hidden enemy units were (World.Radar), and when.</summary>
    public readonly List<FixVec2> Blips = new();
    public int BlipTick = -1;
}

/// <summary>A shot fired this tick, kept for drawing tracers.</summary>
/// <remarks>Flight: ticks a shell is in the air (0 for direct fire, which hits at once).</remarks>
public readonly record struct Shot(FixVec2 From, FixVec2 To, int Owner, Kind By, int ShooterId, int TargetId, int Flight);

/// <summary>
/// The whole battle. Deterministic: given the same seed and the same commands on the same ticks,
/// every machine ends in exactly the same state (compare <see cref="Checksum"/>). It has no Godot
/// dependency; the game reads it and draws it.
/// </summary>
public sealed class World
{
    public const int TicksPerSecond = 10;
    public const int PauseTicks = 30 * TicksPerSecond;
    public const int PausesPerPlayer = 3;
    public const int StartMetal = 600;
    // Resources (the user, 2026-10-20): a trickle of metal comes in regardless, and extractors mine deposits. Slow, since
    // 2026-10-22 (the user: "make the primary way to collect resources a battle, and resource gen slower"): most metal
    // comes from salvage, what's destroyed paying whoever destroyed it (World.Salvage). Paid once a second.
    public const int TrickleIncome = 3;     // per second
    public const int ExtractorIncome = 3;   // per extractor, per second
    public const int ExtractorCost = 250;
    public static readonly Fix DepositRadius = 3;   // nothing else can be built this near a deposit's centre
    // What a wing costs, by what it makes (the user, 2026-10-22: "we should balance the cost of things, light tanks
    // should be much cheaper"). The units themselves are free; the wing is the price of a stream of them.
    public static int WingCost(Kind makes) => makes switch
    {
        Kind.LightTank or Kind.Crawler => 200,
        Kind.Mortar or Kind.Spider => 350,
        Kind.Tank or Kind.Piercer => 450,
        Kind.Artillery => 600,
        Kind.Ranger => 150,
        Kind.Flier => 300,
        Kind.Bomber => 400,
        Kind.Mender => 300,
        Kind.Bulwark => 450,
        Kind.Hive => 400,
        Kind.MobileBase => 300,   // the utility wing
        _ => 500,
    };
    // Bases and wings (the user, 2026-10-22: "the main hq can build wings on it which produce vehicles, one of the
    // wings is a utility vehicle, that lets you build scouts and mobile bases, you can move a mobile base like a scout
    // and set it up somewhere else"). The HQ takes five wings, a set-up mobile base (Kind.Outpost) three; a wing makes
    // one kind of unit, or (the utility wing) scouts and mobile bases to order. Extractors go near a base.
    // (The branching network this replaced is on the network-prototype branch.)
    // (2026-10-22, the user: "we expand out to resources and then build wings off those buildings": the HQ keeps three
    // wing spots; every other wing goes on an extractor or a power plant, two each; mobile bases claim ground, no wings.)
    public const int HqWings = 3, ResourceWings = 2, MobileBaseCost = 500, MaxBases = 4;
    public const int SulfurHitPercent = 125;   // units from a wing on a sulfur extractor: hits this much harder, and they set fires
    public static readonly Fix BaseReach = 30, BaseSpacing = 40;
    // Radar (the user, 2026-10-22: "a radar that pings every 30s to show where units are in the fog so you can't get
    // cheesed"): each base sweeps its range every 30 s; hidden enemy units in it show as blips, and become known.
    public const int RadarTicks = 30 * TicksPerSecond;

    // Power (the user, 2026-10-22: "build powerplants to produce power to build units, this encourages expansion"):
    // the HQ supplies a little; each power plant, on one of the vents beside every metal deposit, supplies more. Every
    // finished wing draws power; drawing more than is supplied, all the side's wings work that much slower.
    public const int HqPower = 4, PlantPower = 6, PlantCost = 300;
    // The other natural resources (the user, 2026-10-23: "I want to add natural resources"): an oil well powers
    // wings and pays a little; a crystal field shields everything you make.
    public const int OilPower = 4, OilIncome = 1, CrystalShieldPercent = 15;
    // A bulwark's dome: how far it reaches (from the bulwark's centre to the edge of what it covers) and how much less
    // what's under it takes. Domes don't add up.
    public static readonly Fix DomeReach = 7;
    public const int DomePercent = 35;

    /// <summary>How many deposits of `res` a player mines (built).</summary>
    public int MinedOf(int player, Resource res)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Extractor && e.Owner == player && e.Building == 0 && DepositResource[e.Deposit] == res) n++;
        return n;
    }
    public static int PowerDraw(Kind makes) => makes switch
    {
        Kind.LightTank or Kind.Crawler or Kind.Ranger or Kind.MobileBase => 1,
        Kind.Artillery or Kind.Bomber => 3,
        _ => 2,
    };

    // Defences: placed within BaseReach of a base; turrets, flak and bombards take upgrades like wings (their
    // "Production" track is range, +2 a level).
    public const int TurretCost = 250, FlakCost = 200, BombardCost = 400, WallCost = 50;
    public static readonly Kind[] DefenceKinds = { Kind.Turret, Kind.Flak, Kind.Bombard, Kind.Wall };
    public static bool IsDefence(Kind k) => k is Kind.Turret or Kind.Flak or Kind.Bombard or Kind.Wall;
    public static int CostOf(Kind defence) => defence switch { Kind.Turret => TurretCost, Kind.Flak => FlakCost, Kind.Bombard => BombardCost, _ => WallCost };

    // Feral machines (the user, 2026-10-22: something "that slows down the early game and makes it more interesting"):
    // a third side that's nobody's. Waves come in from the west and east edges every WaveTicks (the first a minute in)
    // and go for the nearest building; every deposit away from home starts guarded by a derelict turret and a few
    // ferals. Killing them pays salvage.
    public const int Feral = 2;   // their owner number
    // Switched off (the user, 2026-10-22: "get rid of the npcs"): no waves, no guards. True brings them back.
    public const bool FeralsOn = false;
    public const int WaveTicks = 90 * TicksPerSecond, FirstWaveTicks = 60 * TicksPerSecond, MaxFerals = 50;
    /// <summary>What destroying something pays whoever did it (ferals collect nothing): the scrap in it. Units are free
    /// to build, so this is what a fight is worth; buildings pay about half their cost. Tuned so that, over a match,
    /// salvage brings in more than mining.</summary>
    public static int Salvage(Kind k) => k switch
    {
        Kind.Feral => 40, Kind.FeralBrute => 120, Kind.Derelict => 300,
        Kind.Crawler or Kind.Drone or Kind.Scout => 40, Kind.LightTank or Kind.Spider => 70, Kind.Mortar or Kind.Piercer => 90,
        Kind.Tank => 120, Kind.Artillery => 140, Kind.MobileBase => 300,
        Kind.Ranger => 30, Kind.Flier => 70, Kind.Bomber => 100,
        Kind.Factory => 250, Kind.Extractor => 150, Kind.Outpost => 300, Kind.Tower => 200, Kind.Hq => 0,
        Kind.Turret or Kind.Flak => 120, Kind.Bombard => 200, Kind.Wall => 25, Kind.PowerPlant => 150,
        _ => 0,
    };

    /// <summary>Salvage paid this tick, for the game to show (where, to whom, how much).</summary>
    public readonly List<(FixVec2 At, int Owner, int Amount)> Salvaged = new();
    static FixVec2[] WaveGates => new FixVec2[] { new(6 - HalfX, -20), new(HalfX - 6, 20) };   // west and east edges, the map turned round
    public static readonly Fix GuardReach = 25;   // no base or extractor while a derelict stands this near
    public static readonly Fix HqRadar = 120, OutpostRadar = 70;
    // Sulfur: each patch mined (and powered) makes explosive weapons hit harder; one unlocks the napalm fitting.
    public const int SulfurBonusPercent = 20, NapalmHitPercent = 75, FireTicks = 8 * TicksPerSecond;
    // Building limits (the user, 2026-10-20: "building new factories is so much more powerful than upgrading"):
    // each base has so many factory slots; more factories means expanding, and upgrades are how a base grows.
    // Scouts (the user, 2026-10-20): built at the HQ, cheap, unarmed, and the one unit a player steers. They find
    // deposits and set up new bases: outposts go down beside one, and extractors beside one (or in a build ring).
    public const int ScoutCost = 100, MaxScouts = 6;
    public const int MaxArmy = 30;   // tanks and artillery a side can have out at once (in practice 15-25 in the field)

    // Everything has 4.5 times the health in the table (the user, 2026-10-21: "time to kill for a basic unit battle at
    // 1 minute": equal groups took about 14 s to settle a fight; now about a minute). Kept as a ratio (/2) in integers.
    public const int HealthScale = 9;
    public const int MaxUpgradeLevel = 3;
    // Veterancy (the user, 2026-10-19: "they get 10% stat boosts after x kills"): a rank every 3 kills, up to 3;
    // each rank +10% damage and health.
    public const int KillsPerRank = 3, MaxRank = 3;
    static readonly Fix WaypointReach = 4;   // how near a rally point counts as having reached it
    static readonly int[] UpgradeCosts = { 200, 400, 700 };   // for levels 1, 2, 3
    public static int UpgradeCost(int toLevel) => UpgradeCosts[toLevel - 1];
    // Per level: production 15% quicker, damage +25%, armour (health) +25%.
    public static int WithDamage(int baseDamage, int level) => baseDamage * (100 + 25 * level) / 100;
    public static int WithArmour(int baseHp, int level) => baseHp * (100 + 25 * level) / 100;
    public static int WithProduction(int baseTicks, int level) => baseTicks * (100 - 15 * level) / 100;
    public static readonly Fix BuildRadius = 25;
    // (The user, 2026-10-19: "the maps should also be further apart": the HQs 144 apart, was 90.)
    // (The user, 2026-10-20: "make more expansive maps with less resources on them": 280 by 170, the HQs 230 apart,
    // seven deposits where there were twelve.)
    // (2026-10-20: "4x bigger", four times the area; the test map much smaller, Map.cs.)
    static readonly int HalfX = Map.Test ? 110 : 140 * Ground.Scale, HalfZ = Map.Test ? 70 : 85 * Ground.Scale;
    public static readonly Fix MapHalfX = HalfX, MapHalfZ = HalfZ;
    public static readonly Ground Ground = new(HalfX, HalfZ, new[] { HqStart(0), HqStart(1) });
    static readonly Fix MaxHighGround = 5, MaxDownhillRange = 4;

    /// <summary>What factories can make (scouts come from the HQ).</summary>
    // In key order: light tank, tank, mortar, artillery (the user, 2026-10-20: "new cheaper units, a smaller artillery
    // tank and a combat tank": units stay free, the light ones just come quicker).
    public static readonly Kind[] FactoryKinds = { Kind.LightTank, Kind.Tank, Kind.Mortar, Kind.Artillery, Kind.Ranger, Kind.Flier, Kind.Bomber, Kind.Mender, Kind.Bulwark, Kind.Hive };   // the Coalition's

    /// <summary>What a civilisation's factories can make, in build-menu order. The Lattice (the user, 2026-10-21: "they
    /// don't get artillery at tier 1 but they get scout fliers and anti armour units"): crawlers, spiders, piercers.</summary>
    public static Kind[] FactoryKindsOf(Civ civ) => civ == Civ.Lattice ? LatticeKinds : FactoryKinds;
    static readonly Kind[] LatticeKinds = { Kind.Crawler, Kind.Spider, Kind.Piercer, Kind.Ranger, Kind.Flier, Kind.Bomber };

    /// <summary>The scout a civilisation's HQ builds: the Coalition's hovercraft, the Lattice's flying drone.</summary>
    public static Kind ScoutKindOf(Civ civ) => civ == Civ.Lattice ? Kind.Drone : Kind.Scout;
    public static bool IsScout(Kind k) => k is Kind.Scout or Kind.Drone;
    /// <summary>A unit its player steers (scouts and mobile bases); the rest fight on their own.</summary>
    public static bool IsSteered(Kind k) => IsScout(k) || k == Kind.MobileBase;
    public static bool IsFeral(Kind k) => k is Kind.Feral or Kind.FeralBrute or Kind.Derelict;

    /// <summary>How many fighting units a side can have out: the swarm more.</summary>
    public static int MaxArmyOf(Civ civ) => civ == Civ.Lattice ? 45 : MaxArmy;

    /// <summary>
    /// The metal deposits, the same every match and turned round for the other side: few for the size of the map. On
    /// each side: one on the HQ's hill, one by the water behind the valley, one in the valley, one on the middle hill.
    /// Every deposit outside the home rings has an outpost pad beside it (<see cref="Pads"/>): mine the deposit, then
    /// an outpost can go on its pad (the user, 2026-10-20: "it needs to be more clear that you build next to resource
    /// extractors for outposts").
    /// </summary>
    public static readonly FixVec2[] Deposits;
    /// <summary>Per deposit, where its outpost goes; HasPad false for the home one on each side.</summary>
    public static readonly FixVec2[] Pads;
    public static readonly bool[] HasPad;
    /// <summary>What each deposit gives.</summary>
    public static readonly Resource[] DepositResource;
    /// <summary>Power vents, where power plants go: one beside each metal deposit, toward the middle of the map.</summary>
    public static readonly FixVec2[] Vents;

    static World()
    {
        // (deposit, its pad) on player 0's half; player 1's are the same turned round about the middle.
        var west = Map.Test
            ? new (FixVec2 At, FixVec2? Pad)[]
            {
                (new(-74, 24), null),                                // home, beside the HQ
                (new(-38, -30), null),                               // out front, either flank
                (new(-28, 42), null),
            }
            : new (FixVec2 At, FixVec2? Pad)[]
            {
                (new(-237, 88), null),                                   // home, on the HQ's hill
                (new(-200, -30), new FixVec2(-200, -20)),                // out by the north water, behind the valley
                (new(-154, 40), new FixVec2(-154, 30)),                  // down in the valley
                (new(-48, -12), new FixVec2(-38, -12)),                  // up on the middle hill: contested
            };
        var all = new List<(FixVec2 At, FixVec2? Pad)>();
        foreach (var d in west)
        {
            all.Add(d);
            all.Add((-d.At, d.Pad is { } p ? -p : null));
        }
        int metal = all.Count;
        // Sulfur: two patches in the open middle between the lakes, one a little nearer each side, so both are fought over.
        int sulfurZ = Map.Test ? 30 : 45;
        all.Add((new FixVec2(0, sulfurZ), null));
        all.Add((new FixVec2(0, -sulfurZ), null));
        int sulfurEnd = all.Count;
        // Crystal and oil (one of each a side, turned round for player 1; on the big map only where the ground is open).
        var extra = Map.Test
            ? new[] { (new FixVec2(-36, 12), Resource.Crystal), (new FixVec2(-82, -24), Resource.Oil) }
            : new[] { (new FixVec2(-172, 64), Resource.Crystal), (new FixVec2(-250, 36), Resource.Oil) };
        var extraRes = new List<Resource>();
        foreach (var (at, res) in extra)
            foreach (var p in new[] { at, -at })
                if (Nav.OpenAt(p)) { all.Add((p, null)); extraRes.Add(res); }
        Deposits = all.ConvertAll(d => d.At).ToArray();
        Pads = all.ConvertAll(d => d.Pad ?? d.At).ToArray();
        HasPad = new bool[all.Count];   // (outposts are gone: the network replaced them)
        DepositResource = new Resource[all.Count];
        for (int i = metal; i < sulfurEnd; i++) DepositResource[i] = Resource.Sulfur;
        for (int i = sulfurEnd; i < all.Count; i++) DepositResource[i] = extraRes[i - sulfurEnd];
        var vents = new List<FixVec2>();
        for (int i = 0; i < metal; i++)
        {
            var d = Deposits[i];
            var toMiddle = d.LengthSq.Raw == 0 ? new FixVec2(1, 0) : -d;
            foreach (var side in new[] { Fix.Zero, Fix.FromInt(2), Fix.FromInt(-2) })   // (straight in, or a little to one side if that's water)
            {
                var off = toMiddle.WithLength(Fix.One) + new FixVec2(-toMiddle.Z, toMiddle.X).WithLength(Fix.One) * (side / 3);
                var at = d + off.WithLength(Fix.FromInt(10));
                if (Nav.OpenAt(at) && Ground.Passable(at + new FixVec2(3, 0)) && Ground.Passable(at - new FixVec2(3, 0))) { vents.Add(at); break; }
            }
        }
        Vents = vents.ToArray();
    }

    // Roles: the scout is fast and fragile; the tank is tough, the all-rounder; artillery outranges everything and
    // hits an area, but can't fire at anything close, so it's helpless once reached.
    public static readonly Stats[] StatsFor =
    {
        // Hq
        new() { MaxHp = 3000, Radius = 3, Range = 10, Aggro = 12, Vision = 14, Damage = 10, Reload = 10, HitsAir = true, Armoured = true, Explosive = true },
        // Factory
        new() { MaxHp = 800, Radius = 2, Vision = 8, Armoured = true },
        // Scout: unarmed (Reload 0), quick, sees far; built at the HQ and moved by its player.
        new() { MaxHp = 60, Radius = Fix.Ratio(8, 10), Speed = Fix.Ratio(55, 100), Vision = 24, BuildTicks = 10 * TicksPerSecond },
        // Tank (the user, 2026-10-20: tanks and artillery "need to build slower and be weaker, we want like 20 units in
        // game eventually": twice as slow to build, about two thirds the health and damage, and an army cap).
        new() { Armoured = true, MaxHp = 160, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(26, 100), Range = 8, Aggro = 14, Vision = 8,
                Damage = 16, Reload = 15, BuildTicks = 45 * TicksPerSecond, Explosive = true },
        // Artillery
        new() { Armoured = true, MaxHp = 60, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(20, 100), Range = 22, MinRange = 7, Aggro = 22, Vision = 8,
                Splash = Fix.Ratio(25, 10), Damage = 30, Reload = 40, BuildTicks = 60 * TicksPerSecond, Explosive = true },   // (30, up from 27: its shells now miss moving targets)
        // Tower: two stand ahead of each HQ from the start, so a side isn't overrun before it has an army
        // (the user, 2026-10-19: "starting watch towers so it's hard to get overwhelmed initially").
        new() { MaxHp = 1200, Radius = Fix.Ratio(15, 10), Range = 14, Aggro = 14, Vision = 18, Damage = 13, Reload = 10, HitsAir = true, Armoured = true, Explosive = true },
        // Outpost: a mobile base set up in the field: three wing spots, a light gun, a radar.
        new() { MaxHp = 900, Radius = Fix.Ratio(22, 10), Range = 10, Aggro = 10, Vision = 16, Damage = 5, Reload = 10, HitsAir = true, Armoured = true },
        // Extractor: mines a deposit. Unarmed, so it needs looking after.
        new() { MaxHp = 600, Radius = Fix.Ratio(16, 10), Vision = 8, Armoured = true },
        // Light tank: small, quick, fragile; a fast-firing light gun. Built every 10 s.
        new() { MaxHp = 90, Radius = Fix.Ratio(10, 10), Speed = Fix.Ratio(38, 100), Range = 7, Aggro = 12, Vision = 9,
                Damage = 7, Reload = 7, BuildTicks = 20 * TicksPerSecond, Explosive = true },
        // Mortar: a small artillery tank. Shorter range than artillery, a small blast, quick shells; built every 14 s.
        new() { Armoured = true, MaxHp = 55, Radius = Fix.Ratio(10, 10), Speed = Fix.Ratio(28, 100), Range = 15, MinRange = 4, Aggro = 15, Vision = 8,
                Splash = Fix.Ratio(17, 10), Damage = 16, Reload = 25, BuildTicks = 26 * TicksPerSecond, Explosive = true },
        // Spider (the user, 2026-10-20: "a small spider bot. 4 legs, small tank size, a turret and a laser that shoots
        // rapid weak shots"): quick on its legs, light armour, a laser firing three times a second for little each.
        new() { MaxHp = 100, Radius = Fix.Ratio(10, 10), Speed = Fix.Ratio(34, 100), Range = 7, Aggro = 12, Vision = 10,
                Damage = 3, Reload = 3, BuildTicks = 22 * TicksPerSecond },
        // ---- the Lattice (the user, 2026-10-21: the swarm civ; "they don't get artillery at tier 1 but they get scout
        // fliers and anti armour units") ----
        // Crawler: the swarm's body: cheap, quick, fragile, a little claw-gun. Built every 12 s.
        new() { MaxHp = 50, Radius = Fix.Ratio(8, 10), Speed = Fix.Ratio(42, 100), Range = 5, Aggro = 12, Vision = 9,
                Damage = 5, Reload = 6, BuildTicks = 12 * TicksPerSecond },
        // Piercer: anti-armour: slow, a heavy spike-gun every 3 s that does two and a half times to armour.
        new() { MaxHp = 80, Radius = Fix.Ratio(10, 10), Speed = Fix.Ratio(24, 100), Range = 9, Aggro = 13, Vision = 9,
                Damage = 26, Reload = 30, VsArmour = 250, BuildTicks = 30 * TicksPerSecond, Explosive = true },
        // Drone: the Lattice's scout: a flier, unarmed, quick and far-seeing; crosses water. Only HitsAir guns reach it.
        new() { MaxHp = 40, Radius = Fix.Ratio(7, 10), Speed = Fix.Ratio(65, 100), Vision = 26, Flying = true, BuildTicks = 8 * TicksPerSecond },
        // Node: unused (the network prototype's; kept so the kinds after it keep their numbers).
        new() { MaxHp = 300, Radius = Fix.Ratio(12, 10), Vision = 10, Armoured = true },
        // Mobile base: a big slow unarmed vehicle, steered like a scout, that sets up as a base (an Outpost) where it stops.
        new() { Armoured = true, MaxHp = 400, Radius = 2, Speed = Fix.Ratio(18, 100), Vision = 14, BuildTicks = 40 * TicksPerSecond },
        // ---- defences (the user, 2026-10-22: "I want it to kind of feel a bit like a tower defense too") ----
        // Turret: the all-round gun emplacement.
        new() { Armoured = true, MaxHp = 500, Radius = Fix.Ratio(12, 10), Range = 11, Aggro = 11, Vision = 12, Damage = 9, Reload = 8, Explosive = true },
        // Flak: a quick gun that reaches fliers too; light against the ground.
        new() { Armoured = true, MaxHp = 400, Radius = Fix.Ratio(12, 10), Range = 13, Aggro = 13, Vision = 12, Damage = 5, Reload = 4, HitsAir = true },
        // Bombard: an artillery emplacement: long reach, a blast, slow; nothing close; needs others to spot for it.
        new() { Armoured = true, MaxHp = 400, Radius = Fix.Ratio(15, 10), Range = 26, MinRange = 8, Aggro = 26, Vision = 8, Splash = Fix.Ratio(25, 10),
                Damage = 30, Reload = 50, Explosive = true },
        // Wall: a block of armour; a line of them funnels attackers.
        new() { Armoured = true, MaxHp = 1500, Radius = 1, Vision = 3 },
        // ---- feral machines: nobody's, wild scrap that attacks everyone (owner World.Feral) ----
        // Feral scrapper: quick and weak, in packs.
        new() { MaxHp = 40, Radius = Fix.Ratio(8, 10), Speed = Fix.Ratio(36, 100), Range = 4, Aggro = 12, Vision = 10, Damage = 4, Reload = 6 },
        // Feral brute: slow and tough.
        new() { Armoured = true, MaxHp = 140, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(24, 100), Range = 5, Aggro = 12, Vision = 10, Damage = 12, Reload = 12 },
        // Derelict: an old turret gone feral, guarding a deposit.
        new() { Armoured = true, MaxHp = 600, Radius = Fix.Ratio(14, 10), Range = 11, Aggro = 11, Vision = 12, Damage = 8, Reload = 8 },
        // ---- cheap and air units (the user, 2026-10-22: "the game still feels really swingy at the start ... add a
        // ranger (small arms, can attack air or land), add a flier (attacks other fliers) and a bomber (slow bombing
        // runs)"), both civilisations ----
        // Ranger: a small, quick, fragile robot with small arms that reach fliers too: the cheap answer to everything.
        new() { MaxHp = 40, Radius = Fix.Ratio(7, 10), Speed = Fix.Ratio(50, 100), Range = 7, Aggro = 12, Vision = 10,   // (quicker than the hover tank: the user, 2026-10-23, the rifle mech "moves faster")
                Damage = 4, Reload = 5, HitsAir = true, VsAir = 100, BuildTicks = 12 * TicksPerSecond },   // (a bomber takes about 4 of them)
        // Flier: a fast interceptor that only fights other fliers; with none about it keeps over its army.
        new() { MaxHp = 60, Radius = Fix.Ratio(9, 10), Speed = Fix.Ratio(95, 100), Range = 8, Aggro = 16, Vision = 14,
                Damage = 6, Reload = 6, Flying = true, HitsAir = true, AirOnly = true, BuildTicks = 25 * TicksPerSecond },
        // Bomber: fast (the user, 2026-10-22: "bombers should be fast"; interceptors a little faster); flies over a ground target, drops a heavy bomb with a wide blast, flies on and comes round again.
        new() { MaxHp = 150, Radius = Fix.Ratio(14, 10), Speed = Fix.Ratio(80, 100), Range = 2, Aggro = 18, Vision = 12,
                Splash = Fix.Ratio(25, 10), Damage = 45, Reload = 60, Flying = true, Explosive = true, BuildTicks = 40 * TicksPerSecond },
        // Power plant: a diesel generator house on a power vent; it powers the wings.
        new() { Armoured = true, MaxHp = 700, Radius = Fix.Ratio(18, 10), Vision = 8 },
        // Module: a link in a line of the base; the wings after it get what it does (Mod).
        new() { Armoured = true, MaxHp = 500, Radius = Fix.Ratio(14, 10), Vision = 6 },
        // Mender (the user, 2026-10-24: "Mender should have repair drones that fly over and repair"): an unarmed
        // tracked carrier that keeps behind its army and sends drones to the nearest damaged unit in reach, giving
        // back about what a light tank's gun takes off.
        new() { MaxHp = 70, Radius = Fix.Ratio(10, 10), Speed = Fix.Ratio(30, 100), Range = 7, Aggro = 14, Vision = 10,
                Damage = 12, Reload = 10, Heals = true, BuildTicks = 30 * TicksPerSecond },
        // Bulwark: a slow, heavy, unarmed walker carrying a shield emitter: everything of its side under its dome
        // (DomeReach) takes DomePercent less damage. It keeps with the army.
        new() { Armoured = true, MaxHp = 200, Radius = Fix.Ratio(14, 10), Speed = Fix.Ratio(22, 100), Vision = 9, Shields = true,
                BuildTicks = 45 * TicksPerSecond },
        // Hive: a carrier truck whose "gun" is a swarm of little drones out of its roof: a long reach, quick light
        // stings, and the drones chase fliers too. Fragile itself.
        new() { MaxHp = 110, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(26, 100), Range = 10, Aggro = 14, Vision = 10,
                Damage = 5, Reload = 8, HitsAir = true, BuildTicks = 40 * TicksPerSecond },
    };

    public int Tick;
    public readonly Player[] Players;
    public readonly List<Entity> Entities = new();   // always in Id order
    public readonly List<Shot> Shots = new();         // this tick only
    public readonly Rng Rng;
    public int Winner = -1;
    /// <summary>False during setup, until both sides have placed their first factory.</summary>
    public bool Started;
    public int StartTick;   // when both sides had their first wing: the waves count from here
    public int WaveNumber;   // feral waves sent so far

    /// <summary>Ticks until the next feral wave.</summary>
    public int NextWaveIn => Started ? StartTick + FirstWaveTicks + WaveNumber * WaveTicks - Tick : FirstWaveTicks;

    public int PausedBy = -1;
    public int PauseTicksLeft;
    public bool Paused => PausedBy >= 0;
    readonly List<Command> _heldDuringPause = new();
    /// <summary>Orders given during the pause, waiting for it to end.</summary>
    public IReadOnlyList<Command> HeldDuringPause => _heldDuringPause;

    int _nextId = 1;

    /// <param name="civs">each side's civilisation; by default the Coalition against the Lattice (the user,
    /// 2026-10-21: "make the default opponent lattice")</param>
    public World(ulong seed, Civ[] civs = null)
    {
        Rng = new Rng(seed);
        civs ??= new[] { Civ.Coalition, Civ.Coalition };   // (2026-10-22, the user: "lets focus on 1 civ": a mirror match while we test; the Lattice waits)
        Players = new Player[2];
        for (int i = 0; i < 2; i++)
        {
            var hq = HqStart(i);
            Players[i] = new Player { Id = i, Civ = civs[i], Metal = StartMetal, PausesLeft = PausesPerPlayer, HqPos = hq };
            Spawn(Kind.Hq, i, hq);
            // (No starting watch towers since 2026-10-22, the user: "get rid of the 2 starting watch towers".)
        }
        if (FeralsOn) PlaceGuards();
    }

    static Fix Clamp(Fix v, Fix limit) => Fix.Max(-limit, Fix.Min(limit, v));

    // Each HQ on the hill in its corner: player 0 bottom left, player 1 top right (the map turned round).
    public static FixVec2 HqStart(int player) =>
        Map.Test ? (player == 0 ? new FixVec2(-60, 10) : new FixVec2(60, -10))
                 : (player == 0 ? new FixVec2(-224, 100) : new FixVec2(224, -100));
    /// <summary>Where a starting watch tower stands: one each side of the way out of the base toward the middle.</summary>
    public static FixVec2 TowerStart(int player, int side)
    {
        var off = side < 0 ? new FixVec2(22, -5) : new FixVec2(7, -22);
        return player == 0 ? HqStart(0) + off : HqStart(1) - off;
    }

    /// <param name="from">a unit's factory: the unit takes its upgrade levels</param>
    public Entity Spawn(Kind kind, int owner, FixVec2 pos, Kind makes = Kind.Hq, Entity from = null)
    {
        var e = new Entity { Id = _nextId++, Kind = kind, Owner = owner, Pos = pos, Makes = makes };
        if (from != null)
        {
            from.Levels.CopyTo(e.Levels, 0);
            from.Mods.CopyTo(e.Mods, 0);
            e.FactoryId = from.Id;
            e.Fitting = from.Fitting == Fitting.Napalm && SulfurOf(owner) > 0 || from.Mods[(int)Mod.Napalm] > 0 ? Fitting.Napalm : Fitting.Standard;
            e.Enhanced = OnSulfur(from);
            // Its factory's own rally point; failing that the base's (the HQ's: the user, 2026-10-20, "main base
            // should have a way to rally").
            var rally = from.HasRally ? from : HqOf(owner) is { HasRally: true } hq ? hq : null;
            e.HasWaypoint = rally != null;
            if (rally != null) e.Waypoint = rally.Rally;
        }
        SetStats(e);
        e.Hp = e.MaxHp;
        e.Shield = e.MaxHp * ShieldPercent * e.Mods[(int)Mod.Shield] / 100;
        if (from != null) e.Shield += e.MaxHp * CrystalShieldPercent * MinedOf(owner, Resource.Crystal) / 100;   // (crystal fields)
        if (kind == Kind.Factory && makes != Kind.MobileBase) e.Timer = BuildTicksOf(e);
        e.Building = ConstructionTicks(kind);   // (towers and the HQ stand ready from the start)
        e.Height = Ground.HeightAt(pos);
        Entities.Add(e);
        if (e.Stats.IsBuilding) _nav.AddBuilding(e.Pos, e.Stats.Radius);   // units plan their way round it from now on
        return e;
    }

    /// <summary>Health and damage from the base stats, the upgrades it was made with, and its veterancy.</summary>
    static void SetStats(Entity e)
    {
        e.MaxHp = WithArmour(e.Stats.MaxHp * HealthScale / 2, e.Level(Upgrade.Armour)) * (100 + 10 * e.Rank) / 100;
        e.Damage = WithDamage(e.Stats.Damage, e.Level(Upgrade.Damage)) * (100 + 10 * e.Rank) / 100;
    }

    /// <summary>A kill for `by`; every KillsPerRank of them it ranks up, and heals by the health it gains.</summary>
    static void Credit(Entity by)
    {
        by.Kills++;
        if (!by.Stats.IsUnit || by.Rank >= MaxRank || by.Kills < (by.Rank + 1) * KillsPerRank) return;
        int before = by.MaxHp;
        by.Rank++;
        SetStats(by);
        if (by.Hp > 0) by.Hp += by.MaxHp - before;
    }

    /// <summary>How often a factory makes a unit, with its production upgrades.</summary>
    public static int BuildTicksOf(Entity factory) =>
        WithProduction(StatsFor[(int)factory.Makes].BuildTicks, factory.Level(Upgrade.Production));

    public Entity Find(int id) => Entities.Find(e => e.Id == id && !e.Dead);

    public bool CanUpgrade(int player, int factoryId, Upgrade which, out string why)
    {
        if (!CanQueueUpgrade(player, factoryId, which, out why)) return false;
        var f = Find(factoryId);
        if (f.UpgradeQueue.Count > 0) { why = "Others are queued first"; return false; }
        if (Players[player].Metal < UpgradeCost(f.Level(which) + 1)) { why = "Not enough metal"; return false; }
        return true;
    }

    /// <summary>Whether an upgrade can be ordered at all, bought now or queued (the user, 2026-10-20: "you should also
    /// be able to queue updates"): the player's factory or outpost, and that track not already full with what's queued.</summary>
    public bool CanQueueUpgrade(int player, int factoryId, Upgrade which, out string why)
    {
        var f = Find(factoryId);
        why = null;
        if (f == null || !(f.Kind is Kind.Turret or Kind.Flak or Kind.Bombard) || f.Owner != player) { why = "Not yours to upgrade"; return false; }
        if (f.Level(which) + Queued(f, which) >= MaxUpgradeLevel) { why = "Fully upgraded"; return false; }
        return true;
    }

    /// <summary>How many of a track's levels are waiting in a building's queue.</summary>
    public static int Queued(Entity f, Upgrade which)
    {
        int n = 0;
        foreach (var q in f.UpgradeQueue) if (q == (int)which) n++;
        return n;
    }

    void BuyUpgrade(Entity f, Upgrade which)
    {
        Players[f.Owner].Metal -= UpgradeCost(f.Level(which) + 1);
        f.Levels[(int)which]++;
        if (f.Kind == Kind.Outpost || IsDefence(f.Kind))   // a building's own guns and armour improve at once
        {
            int before = f.MaxHp;
            SetStats(f);
            f.Hp += f.MaxHp - before;
        }
    }

    /// <summary>Queued upgrades buy themselves, in order, once there's the metal (buildings oldest first).</summary>
    void BuyQueuedUpgrades()
    {
        foreach (var f in Entities)
        {
            if (f.Dead) continue;
            while (f.UpgradeQueue.Count > 0)
            {
                var which = (Upgrade)f.UpgradeQueue[0];
                if (Players[f.Owner].Metal < UpgradeCost(f.Level(which) + 1)) break;
                f.UpgradeQueue.RemoveAt(0);
                BuyUpgrade(f, which);
            }
        }
    }

    // ---- selling (the user, 2026-10-19: "sell for 75% of purchase cost incl upgrades") -------------------------

    public const int SellBackPercent = 75;

    /// <summary>Everything paid for a building: itself and every upgrade level bought.</summary>
    public static int SpentOn(Entity b)
    {
        int spent = b.Kind switch { Kind.Module => ModuleCost(b.Mod), Kind.Outpost => MobileBaseCost, Kind.Extractor => ExtractorCost, Kind.PowerPlant => PlantCost, _ when IsDefence(b.Kind) => CostOf(b.Kind), _ => WingCost(b.Makes) };
        if (IsDefence(b.Kind))   // (a wing's levels come from its modules, not bought)
            foreach (var level in b.Levels)
                for (int l = 1; l <= level; l++) spent += UpgradeCost(l);
        return spent;
    }

    public static int SellValue(Entity b) => SpentOn(b) * SellBackPercent / 100;

    public bool CanSell(int player, int id, out string why)
    {
        var b = Find(id);
        why = null;
        if (b == null || !(b.Kind is Kind.Factory or Kind.Module or Kind.Outpost or Kind.Extractor or Kind.PowerPlant || IsDefence(b.Kind)) || b.Owner != player) { why = "Not yours to sell"; return false; }
        return true;
    }

    static Fix VisionOf(Entity e) => e.Stats.Vision;

    // ---- construction (the user, 2026-10-21: "Building new factories is 1 minute. New mines is 2"; a mobile base
    // takes 30 s to set up) -------------------------------------------------------------------------------------------

    /// <summary>How long a building takes to put up once ordered (paid for up front; it does nothing till done).</summary>
    public static int ConstructionTicks(Kind k) => k switch
    {
        Kind.Factory => 60 * TicksPerSecond,
        Kind.Extractor => 120 * TicksPerSecond,
        Kind.Outpost => 30 * TicksPerSecond,
        Kind.Turret or Kind.Flak => 30 * TicksPerSecond,
        Kind.Bombard => 45 * TicksPerSecond,
        Kind.Wall => 8 * TicksPerSecond,
        Kind.PowerPlant => 45 * TicksPerSecond,
        Kind.Module => 30 * TicksPerSecond,
        _ => 0,
    };

    /// <summary>Buildings going up count down.</summary>
    void Construct()
    {
        foreach (var e in Entities) if (!e.Dead && e.Building > 0) e.Building--;
    }

    // ---- bases and their wings -----------------------------------------------------------------------------------

    /// <summary>A base: the HQ, or a mobile base set up (an Outpost). Wings are built onto it.</summary>
    public static bool IsBase(Entity e) => e.Kind is Kind.Hq or Kind.Outpost;

    /// <summary>What wings are built onto: the HQ, extractors and power plants.</summary>
    public static bool IsWingHost(Entity e) => e.Kind is Kind.Hq or Kind.Outpost or Kind.Extractor or Kind.PowerPlant;

    /// <summary>How many wings a host takes: the HQ three, an extractor or power plant two.</summary>
    public static int SlotsOf(Entity b) => b.Kind is Kind.Hq or Kind.Outpost ? HqWings : IsWingHost(b) ? ResourceWings : 0;

    /// <summary>A wing on a sulfur extractor: its units come out enhanced.</summary>
    public bool OnSulfur(Entity wing) => Find(wing.BaseId) is { Kind: Kind.Extractor } x && x.Deposit >= 0 && DepositResource[x.Deposit] == Resource.Sulfur;

    /// <summary>A wing on a power plant: it draws half the power.</summary>
    public bool OnPlant(Entity wing) => Find(wing.BaseId) is { Kind: Kind.PowerPlant };

    /// <summary>How many of a base's wing spots are taken.</summary>
    public int SlotsUsed(Entity b)
    {
        int n = 0;
        foreach (var e in Entities) if (!e.Dead && e.Kind is Kind.Factory or Kind.Module && e.BaseId == b.Id) n++;
        return n;
    }

    /// <summary>A utility wing: it builds scouts and mobile bases to order, not a stream of fighting units.</summary>
    public static bool IsUtility(Entity e) => e.Kind == Kind.Factory && e.Makes == Kind.MobileBase;

    // The wing spots round a base, for player 0 (player 1's are the same turned round, as the map is): the first faces
    // the enemy, the rest go round. Each one a wing's width off the base's edge, so wings sit against it.
    static readonly FixVec2[] HqWingSpots =
        { new(Fix.Ratio(548, 100), Fix.Ratio(-244, 100)), new(Fix.Ratio(401, 100), Fix.Ratio(446, 100)), new(-3, Fix.Ratio(520, 100)),
          new(Fix.Ratio(-587, 100), Fix.Ratio(-125, 100)), new(Fix.Ratio(-63, 100), Fix.Ratio(-597, 100)) };
    static readonly Fix ResourceWingReach = Fix.Ratio(58, 10);   // a resource building's wings: this far either side of it

    /// <summary>Where wing spot `slot` of host `b` is: round the HQ by the table; either side of an extractor or power
    /// plant, square to the way to the middle of the map (so they sit beside it, clear of its deposit or vent).</summary>
    public static FixVec2 WingSpot(Entity b, int slot)
    {
        if (b.Kind is Kind.Hq or Kind.Outpost)
        {
            var off = HqWingSpots[slot];
            return b.Owner == 0 ? b.Pos + off : b.Pos - off;
        }
        var toMiddle = b.Pos.LengthSq.Raw == 0 ? new FixVec2(1, 0) : (-b.Pos).WithLength(Fix.One);
        var side = new FixVec2(-toMiddle.Z, toMiddle.X) * ResourceWingReach;
        return slot == 0 ? b.Pos + side : b.Pos - side;
    }

    // ---- lines: the base as a machine (the user, 2026-10-23: "like a rube goldberg machine") ------------------
    // Each wing spot of a host starts a line. Wings, modules and blanks snap onto its end one after another, each a
    // fixed step from the last in whichever way the player points (bending at most TurnDegrees at a piece), so a line
    // can be steered out to a resource (the user: "blank blocks that just cost and move us closer, each block is a
    // certain number of px from the last"). A wing gets every finished module before it on its line.

    public const int ModCount = 10, MaxLink = 14, ShieldPercent = 40, TwinEvery = 3, MagnetPercent = 50, TurnDegrees = 50;
    public static readonly Fix LineStep = Fix.Ratio(45, 10);   // from one piece of a line to the next
    static readonly Fix BendCos = Fix.Ratio(643, 1000), BendSin = Fix.Ratio(766, 1000);   // (cos and sin of TurnDegrees)
    /// <summary>How near a deposit or vent one of your line's pieces has to be to put an extractor or plant on it
    /// (or a host itself: HostClaimReach).</summary>
    public static readonly Fix ClaimReach = 9, HostClaimReach = 14;

    /// <summary>What a module costs (a blank only carries the line on).</summary>
    public static int ModuleCost(Mod m) => m switch
    {
        Mod.Speed => 150, Mod.Armour => 200, Mod.Attack => 250, Mod.Build => 200, Mod.Energy => 150,
        Mod.Twin => 350, Mod.Napalm => 250, Mod.Shield => 250, Mod.Magnet => 200, Mod.Blank => 50, _ => 200,
    };

    /// <summary>How long a module takes to put up (a blank is quick).</summary>
    public static int ModuleBuildTicks(Mod m) => m == Mod.Blank ? 8 * TicksPerSecond : 30 * TicksPerSecond;

    /// <summary>A unit's speed, with the speed modules it was made behind (+20% each).</summary>
    public static Fix SpeedOf(Entity e) => e.Mods[(int)Mod.Speed] == 0 ? e.Stats.Speed : e.Stats.Speed * Fix.Ratio(100 + 20 * e.Mods[(int)Mod.Speed], 100);

    /// <summary>Which way line `slot` of host `b` leaves it (a unit vector, out from the host).</summary>
    public static FixVec2 LineDir(Entity b, int slot) => (WingSpot(b, slot) - b.Pos).WithLength(Fix.One);

    /// <summary>Where place `link` would be on a line run dead straight (laying out a showcase base).</summary>
    public static FixVec2 LineSpot(Entity b, int slot, int link) => WingSpot(b, slot) + LineDir(b, slot) * (LineStep * link);

    /// <summary>A line's piece at place `link`, or null.</summary>
    public Entity PieceAt(Entity b, int slot, int link) =>
        Entities.Find(e => !e.Dead && e.Kind is Kind.Factory or Kind.Module && e.BaseId == b.Id && e.Slot == slot && e.Link == link);

    /// <summary>The last piece of a line (the furthest along), or null when it has none.</summary>
    public Entity LineEnd(Entity b, int slot)
    {
        Entity end = null;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind is Kind.Factory or Kind.Module && e.BaseId == b.Id && e.Slot == slot && (end == null || e.Link > end.Link)) end = e;
        return end;
    }

    /// <summary>The way a line runs into its piece `e` (its first piece: straight out of the host).</summary>
    public FixVec2 HeadingOf(Entity e) => e.Heading.LengthSq.Raw != 0 ? e.Heading : Find(e.BaseId) is { } b ? LineDir(b, e.Slot) : new FixVec2(1, 0);

    /// <summary>Where the next piece of a line goes: one step on from its end toward `toward` (zero: straight on),
    /// bending at most TurnDegrees; the first piece at the host's wing spot. `dir`: the way it runs in.</summary>
    public FixVec2 NextSpot(Entity b, int slot, FixVec2 toward, out FixVec2 dir)
    {
        var end = LineEnd(b, slot);
        if (end == null) { dir = LineDir(b, slot); return WingSpot(b, slot); }
        var prev = HeadingOf(end);
        dir = prev;
        var want = toward - end.Pos;
        if (toward.LengthSq.Raw != 0 && want.LengthSq > Fix.Ratio(1, 4))
        {
            var w = want.WithLength(Fix.One);
            if (w.X * prev.X + w.Z * prev.Z >= BendCos) dir = w;
            else
            {
                var left = new FixVec2(-prev.Z, prev.X);
                var turn = prev * BendCos + left * BendSin;
                var other = prev * BendCos - left * BendSin;
                dir = (w.X * left.X + w.Z * left.Z >= Fix.Zero ? turn : other).WithLength(Fix.One);   // (as far round as it may toward it)
            }
        }
        return end.Pos + dir * LineStep;
    }

    /// <summary>Whether the next piece of a line could stand at `at`: on the map, on open ground, clear of other
    /// buildings (but the line's end and its host), deposits and vents. `why` when not.</summary>
    bool SpotOk(Entity b, int slot, FixVec2 at, out string why)
    {
        why = null;
        var r = StatsFor[(int)Kind.Factory].Radius;
        if (OffMap(at, r) || !Nav.OpenAt(at)) { why = "Off the ground it can stand on"; return false; }
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            if (!Ground.Passable(at + new FixVec2(dx, dz) * r)) { why = "Off the ground it can stand on"; return false; }
        var end = LineEnd(b, slot);
        foreach (var e in Entities)
        {
            if (e.Dead || !e.Stats.IsBuilding || e == b || e == end) continue;
            var gap = e.Stats.Radius + r + 1;
            if ((e.Pos - at).LengthSq < gap * gap) { why = "Something's in the way"; return false; }
        }
        foreach (var d in Deposits)
            if ((d - at).LengthSq < (DepositRadius + r) * (DepositRadius + r)) { why = "That's a deposit: put an extractor on it"; return false; }
        foreach (var v in Vents)
        {
            var gap = StatsFor[(int)Kind.PowerPlant].Radius + r;
            if ((v - at).LengthSq < gap * gap) { why = "That's a vent: put a power plant on it"; return false; }
        }
        return true;
    }

    /// <summary>The place a line's next piece takes going straight on, or -1 when it's full or blocked that way.</summary>
    public int FreeLink(Entity b, int slot)
    {
        int link = (LineEnd(b, slot)?.Link ?? -1) + 1;
        if (link > MaxLink) return -1;
        return SpotOk(b, slot, NextSpot(b, slot, FixVec2.Zero, out _), out _) ? link : -1;
    }

    /// <summary>The modules (finished) before place `link` on a line, counted by Mod.</summary>
    public void ModsBefore(Entity b, int slot, int link, int[] into)
    {
        System.Array.Clear(into);
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Module && e.Building == 0 && e.BaseId == b.Id && e.Slot == slot && e.Link < link) into[(int)e.Mod]++;
    }

    /// <summary>The line a wing would go on when none is named (the AI's): the one with the most modules before its
    /// free place, then the shortest, the first such; -1 if none has room.</summary>
    public int FreeWingSlot(Entity b)
    {
        int best = -1, bestMods = int.MinValue;
        var mods = new int[ModCount];
        for (int slot = 0; slot < SlotsOf(b); slot++)
        {
            int link = FreeLink(b, slot);
            if (link < 0) continue;
            ModsBefore(b, slot, link, mods);
            int n = 0;
            for (int m = 0; m < (int)Mod.Blank; m++) n += mods[m];
            int score = n * 10 - link;   // (modules first; else the shortest line)
            if (score > bestMods) { best = slot; bestMods = score; }
        }
        return best;
    }

    /// <summary>Every wing's modules brought up to date, and its levels from them: attack, armour, build speed.</summary>
    void RefreshLines()
    {
        foreach (var w in Entities)
        {
            if (w.Dead || w.Kind != Kind.Factory || Find(w.BaseId) is not { } host) continue;
            ModsBefore(host, w.Slot, w.Link, w.Mods);
            w.Levels[(int)Upgrade.Damage] = w.Mods[(int)Mod.Attack];
            w.Levels[(int)Upgrade.Armour] = w.Mods[(int)Mod.Armour];
            w.Levels[(int)Upgrade.Production] = System.Math.Min(4, w.Mods[(int)Mod.Build]);
        }
    }

    /// <summary>Whether a wing (`makes`) or a module (`mod`) can go on the end of line `slot` of host `baseId` now,
    /// heading toward `toward` (zero: straight on).</summary>
    public bool CanAddPiece(int player, int baseId, int slot, Kind makes, Mod? mod, out string why) => CanAddPiece(player, baseId, slot, makes, mod, FixVec2.Zero, out why);

    public bool CanAddPiece(int player, int baseId, int slot, Kind makes, Mod? mod, FixVec2 toward, out string why)
    {
        why = null;
        var p = Players[player];
        var b = Find(baseId);
        if (mod == null && makes != Kind.MobileBase && System.Array.IndexOf(FactoryKindsOf(p.Civ), makes) < 0) { why = "No wing makes that"; return false; }
        if (b == null || !IsWingHost(b) || b.Owner != player) { why = "Lines start from your HQ, bases, extractors and power plants"; return false; }
        if (b.Building > 0) { why = "It's still being built"; return false; }
        if (slot < 0 || slot >= SlotsOf(b)) { why = "No line there"; return false; }
        if (p.Metal < (mod is { } m ? ModuleCost(m) : WingCost(makes))) { why = "Not enough metal"; return false; }
        if ((LineEnd(b, slot)?.Link ?? -1) + 1 > MaxLink) { why = "This line is as long as it goes"; return false; }
        return SpotOk(b, slot, NextSpot(b, slot, toward, out _), out why);
    }

    Entity AddPiece(int player, Entity b, int slot, Kind makes, Mod? mod, FixVec2 toward)
    {
        int link = (LineEnd(b, slot)?.Link ?? -1) + 1;
        var at = NextSpot(b, slot, toward, out var dir);
        Players[player].Metal -= mod is { } m ? ModuleCost(m) : WingCost(makes);
        var piece = mod != null ? Spawn(Kind.Module, player, at) : Spawn(Kind.Factory, player, at, makes);
        if (mod is { } m2) { piece.Mod = m2; piece.Building = ModuleBuildTicks(m2); }
        piece.BaseId = b.Id;
        piece.Slot = slot;
        piece.Link = link;
        piece.Heading = dir;
        if (piece.Kind == Kind.Factory) { RefreshLines(); if (makes != Kind.MobileBase) piece.Timer = BuildTicksOf(piece); }
        return piece;
    }

    /// <summary>Which way a wing's door faces: square to its line (the line runs through it side to side), on the side
    /// toward the enemy.</summary>
    public FixVec2 DoorDir(Entity wing) => Find(wing.BaseId) is not null
        ? DoorDirAt(wing.Owner, HeadingOf(wing), wing.Pos)
        : (Players[1 - wing.Owner].HqPos - wing.Pos).WithLength(Fix.One);

    /// <summary>Which way a wing at `at` on a line running `lineDir` would face its door.</summary>
    public FixVec2 DoorDirAt(int owner, FixVec2 lineDir, FixVec2 at)
    {
        var side = new FixVec2(-lineDir.Z, lineDir.X);
        var toEnemy = Players[1 - owner].HqPos - at;
        return side.X * toEnemy.X + side.Z * toEnemy.Z < Fix.Zero ? -side : side;
    }

    /// <summary>Whether one of the player's lines reaches `pos`: a finished piece within ClaimReach of it, or one of
    /// their hosts (the HQ, a base, an extractor, a plant) within HostClaimReach.</summary>
    public bool LineReaches(int player, FixVec2 pos, bool built = true)
    {
        foreach (var e in Entities)
        {
            if (e.Dead || e.Owner != player || (built && e.Building > 0)) continue;
            var d2 = (e.Pos - pos).LengthSq;
            if (e.Kind is Kind.Factory or Kind.Module && d2 <= ClaimReach * ClaimReach) return true;
            if (IsWingHost(e) && d2 <= HostClaimReach * HostClaimReach) return true;
        }
        return false;
    }

    /// <summary>Whether a wing making `makes` (a fighting unit of the player's civilisation, or Kind.MobileBase for the
    /// utility wing) can be built onto base `baseId` now.</summary>
    public bool CanAddWing(int player, int baseId, Kind makes, out string why)
    {
        why = null;
        var p = Players[player];
        var b = Find(baseId);
        if (makes != Kind.MobileBase && System.Array.IndexOf(FactoryKindsOf(p.Civ), makes) < 0) { why = "No wing makes that"; return false; }
        if (b == null || !IsWingHost(b) || b.Owner != player) { why = "Wings go on your HQ, extractors or power plants"; return false; }
        if (b.Building > 0) { why = "It's still being built"; return false; }
        if (p.Metal < WingCost(makes)) { why = "Not enough metal"; return false; }
        if (FreeWingSlot(b) < 0) { why = "Every line here is full"; return false; }
        return true;
    }

    /// <summary>One of the player's set-up bases (built) within `reach` of `pos`, the oldest first; or null.</summary>
    public Entity BaseNear(int player, FixVec2 pos, Fix reach)
    {
        foreach (var e in Entities)
            if (!e.Dead && e.Owner == player && IsBase(e) && e.Building == 0 && (e.Pos - pos).LengthSq <= reach * reach) return e;
        return null;
    }

    /// <summary>Bases a player has or has coming: the HQ, set-up bases, mobile bases on the move, and ones ordered.</summary>
    public int BaseCount(int player)
    {
        int n = 0;
        foreach (var e in Entities)
        {
            if (e.Dead || e.Owner != player) continue;
            if (IsBase(e) || e.Kind == Kind.MobileBase) n++;
            if (IsUtility(e)) foreach (var o in e.Orders) if (o == (int)Kind.MobileBase) n++;
        }
        return n;
    }

    /// <summary>Whether a mobile base can set up where it stands: on open ground clear of buildings and deposits, and
    /// not within BaseSpacing of any base.</summary>
    public bool CanDeploy(int player, int unitId, out string why)
    {
        why = null;
        var u = Find(unitId);
        if (u == null || u.Kind != Kind.MobileBase || u.Owner != player) { why = "Not your mobile base"; return false; }
        if (Blocked(u.Pos, StatsFor[(int)Kind.Outpost].Radius)) { why = "No room to set up here"; return false; }
        if (GuardNear(u.Pos)) { why = "A derelict guards this ground: clear it first"; return false; }
        foreach (var e in Entities)
            if (!e.Dead && IsBase(e) && (e.Pos - u.Pos).LengthSq < BaseSpacing * BaseSpacing) { why = $"Too near a base (keep {BaseSpacing} apart)"; return false; }
        return true;
    }

    /// <summary>Whether a set-up base can pack up and move again: it has to have no wings on it.</summary>
    public bool CanPackUp(int player, int baseId, out string why)
    {
        why = null;
        var b = Find(baseId);
        if (b == null || b.Kind != Kind.Outpost || b.Owner != player) { why = "Not your mobile base"; return false; }
        if (b.Building > 0) { why = "Still setting up"; return false; }
        if (SlotsUsed(b) > 0) { why = "Sell its wings first"; return false; }
        return true;
    }

    /// <summary>Something new of radius `r` at `pos` overlaps a building, or sits on a deposit.</summary>
    bool Blocked(FixVec2 pos, Fix r)
    {
        // Not on water or the ridge: its middle and its edge all round must be ground units can reach.
        if (!Nav.OpenAt(pos)) return true;
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            if (!Ground.Passable(pos + new FixVec2(dx, dz) * r)) return true;
        foreach (var e in Entities)
        {
            if (e.Dead || !e.Stats.IsBuilding) continue;
            var gap = e.Stats.Radius + r + 1;
            if ((e.Pos - pos).LengthSq < gap * gap) return true;
        }
        foreach (var d in Deposits)
        {
            var gap = DepositRadius + r;
            if ((d - pos).LengthSq < gap * gap) return true;
        }
        foreach (var v in Vents)   // (vents are kept for power plants)
        {
            var gap = StatsFor[(int)Kind.PowerPlant].Radius + r;
            if ((v - pos).LengthSq < gap * gap) return true;
        }
        return false;
    }

    static bool OffMap(FixVec2 pos, Fix r) => Fix.Abs(pos.X) > MapHalfX - r || Fix.Abs(pos.Z) > MapHalfZ - r;

    // ---- resources: extractors on deposits -------------------------------------------------------------------

    /// <summary>The live extractor on a deposit, if any.</summary>
    public Entity ExtractorOn(int deposit) => Entities.Find(e => !e.Dead && e.Kind == Kind.Extractor && e.Deposit == deposit);

    /// <summary>The deposit at (or nearest within its radius of) `pos`, or -1.</summary>
    public static int DepositAt(FixVec2 pos)
    {
        for (int i = 0; i < Deposits.Length; i++)
            if ((Deposits[i] - pos).LengthSq <= DepositRadius * DepositRadius) return i;
        return -1;
    }

    /// <summary>An extractor goes on a free deposit within BaseReach of one of the player's bases.</summary>
    public bool CanPlaceExtractor(int player, int deposit, out string why)
    {
        why = null;
        if (deposit < 0 || deposit >= Deposits.Length) { why = "Put it on a deposit"; return false; }
        if (Players[player].Metal < ExtractorCost) { why = "Not enough metal"; return false; }
        if (ExtractorOn(deposit) != null) { why = "Already mined"; return false; }
        if (GuardNear(Deposits[deposit])) { why = "A derelict guards it: clear it first"; return false; }
        if (!LineReaches(player, Deposits[deposit])) { why = $"Run a line to within {ClaimReach} of it (blanks carry a line on)"; return false; }
        return true;
    }

    /// <summary>Metal a player makes each second: the trickle and their extractors (salvage aside).</summary>
    public int IncomeOf(int player)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Extractor && e.Owner == player && e.Building == 0 && DepositResource[e.Deposit] == Resource.Metal) n++;   // (mining once built)
        return TrickleIncome + ExtractorIncome * n + OilIncome * MinedOf(player, Resource.Oil);
    }

    // ---- power -------------------------------------------------------------------------------------------------

    /// <summary>The live power plant on a vent, if any.</summary>
    public Entity PlantOn(int vent) => Entities.Find(e => !e.Dead && e.Kind == Kind.PowerPlant && e.Vent == vent);

    public bool CanPlacePlant(int player, int vent, out string why)
    {
        why = null;
        if (vent < 0 || vent >= Vents.Length) { why = "Put it on a power vent"; return false; }
        if (Players[player].Metal < PlantCost) { why = "Not enough metal"; return false; }
        if (PlantOn(vent) != null) { why = "Already taken"; return false; }
        if (GuardNear(Vents[vent])) { why = "A derelict guards it: clear it first"; return false; }
        if (!LineReaches(player, Vents[vent])) { why = $"Run a line to within {ClaimReach} of it (blanks carry a line on)"; return false; }
        return true;
    }

    /// <summary>Power a side has: the HQ's and its finished plants'.</summary>
    public int PowerSupply(int player)
    {
        int n = 0;
        foreach (var e in Entities)
        {
            if (e.Dead || e.Owner != player || e.Building > 0) continue;
            if (e.Kind == Kind.Hq) n += HqPower;
            if (e.Kind == Kind.PowerPlant) n += PlantPower;
            if (e.Kind == Kind.Extractor && DepositResource[e.Deposit] == Resource.Oil) n += OilPower;
        }
        return n;
    }

    /// <summary>Power a side's finished wings draw.</summary>
    public int PowerDrawOf(int player)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Owner == player && e.Kind == Kind.Factory && e.Building == 0) n += WingDraw(e);
        return n;
    }

    /// <summary>What a wing draws: its unit's draw, halved on a power plant.</summary>
    public int WingDraw(Entity wing)
    {
        int d = System.Math.Max(0, PowerDraw(wing.Makes) - wing.Mods[(int)Mod.Energy]);   // (an energy module: one less each)
        return OnPlant(wing) ? d / 2 : d;
    }

    /// <summary>Whether a wing gets a tick of work done: always with power to spare; short of power, it banks the
    /// supply each tick and works whenever it has banked the draw, so every wing slows to supply/draw.</summary>
    bool Work(Entity wing, int supply, int draw)
    {
        if (draw <= supply) return true;
        wing.PowerAcc += supply;
        if (wing.PowerAcc < draw) return false;
        wing.PowerAcc -= draw;
        return true;
    }

    /// <summary>How many sulfur patches a player mines (built).</summary>
    public int SulfurOf(int player)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Extractor && e.Owner == player && e.Building == 0 && DepositResource[e.Deposit] == Resource.Sulfur) n++;
        return n;
    }

    // ---- defences -----------------------------------------------------------------------------------------------

    public bool CanPlaceDefence(int player, FixVec2 pos, Kind kind, out string why)
    {
        why = null;
        var r = StatsFor[(int)kind].Radius;
        if (!IsDefence(kind)) { why = "Not a defence"; return false; }
        if (Players[player].Metal < CostOf(kind)) { why = "Not enough metal"; return false; }
        if (OffMap(pos, r)) { why = "Off the map"; return false; }
        if (BaseNear(player, pos, BaseReach) == null) { why = $"Must be within {BaseReach} of one of your bases"; return false; }
        if (kind == Kind.Wall ? WallBlocked(pos, r) : Blocked(pos, r)) { why = "Blocked"; return false; }
        return true;
    }

    /// <summary>Walls may stand shoulder to shoulder with each other (a line of them is a wall); otherwise as Blocked.</summary>
    bool WallBlocked(FixVec2 pos, Fix r)
    {
        if (!Nav.OpenAt(pos)) return true;
        foreach (var e in Entities)
        {
            if (e.Dead || !e.Stats.IsBuilding) continue;
            var gap = e.Stats.Radius + r + (e.Kind == Kind.Wall ? Fix.Zero : Fix.One);
            if ((e.Pos - pos).LengthSq < gap * gap * Fix.Ratio(9, 10)) return true;
        }
        foreach (var d in Deposits)
        {
            var gap = DepositRadius + r;
            if ((d - pos).LengthSq < gap * gap) return true;
        }
        return false;
    }

    /// <summary>How far an entity's gun reaches and looks: a defence's range upgrades add to both.</summary>
    public static Fix RangeOf(Entity e) => e.Stats.Range + (IsDefence(e.Kind) ? 2 * e.Level(Upgrade.Production) : 0);
    static Fix AggroOf(Entity e) => e.Stats.Aggro + (IsDefence(e.Kind) ? 2 * e.Level(Upgrade.Production) : 0);

    // ---- feral machines --------------------------------------------------------------------------------------

    /// <summary>A derelict still standing near `pos` (a deposit, or where a base would set up).</summary>
    public bool GuardNear(FixVec2 pos)
    {
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Derelict && (e.Pos - pos).LengthSq <= GuardReach * GuardReach) return true;
        return false;
    }

    /// <summary>Every deposit away from the two home ones starts guarded: a derelict turret beside it and a pack of
    /// ferals that hold the ground (sulfur twice over, with a brute).</summary>
    void PlaceGuards()
    {
        for (int d = 0; d < Deposits.Length; d++)
        {
            var at = Deposits[d];
            if ((at - HqStart(0)).LengthSq < Fix.FromInt(40 * 40) || (at - HqStart(1)).LengthSq < Fix.FromInt(40 * 40)) continue;   // home
            bool sulfur = DepositResource[d] == Resource.Sulfur;
            var offsets = sulfur ? new[] { new FixVec2(7, 0), new FixVec2(-7, 0) } : new[] { new FixVec2(0, at.Z.Raw >= 0 ? -7 : 7) };
            foreach (var off in offsets) Spawn(Kind.Derelict, Feral, at + off);
            int pack = sulfur ? 4 : 2;
            for (int i = 0; i < pack; i++)
                Guard(Spawn(Kind.Feral, Feral, at + SpreadOffsets[1 + i] * 2));
            if (sulfur) Guard(Spawn(Kind.FeralBrute, Feral, at + new FixVec2(0, 4)));
        }
        static void Guard(Entity e) { e.HasRally = true; e.Rally = e.Pos; }   // (it holds this ground, and comes back to it)
    }

    /// <summary>A wave from both edges at once, every WaveTicks: a few scrappers more each time, brutes from the
    /// fourth. They go for the nearest building of either side.</summary>
    void SendWaves()
    {
        if (Tick < StartTick + FirstWaveTicks + WaveNumber * WaveTicks) return;
        WaveNumber++;
        int alive = 0;
        foreach (var e in Entities) if (!e.Dead && e.Owner == Feral && e.Stats.IsUnit) alive++;
        if (alive >= MaxFerals) return;
        int scrappers = System.Math.Min(10, 2 + WaveNumber), brutes = WaveNumber >= 4 ? System.Math.Min(4, (WaveNumber - 2) / 2) : 0;
        foreach (var gate in WaveGates)
        {
            for (int i = 0; i < scrappers + brutes; i++)
            {
                var at = gate + new FixVec2(Rng.Range(-6, 6), Rng.Range(-12, 12));
                Spawn(i < brutes ? Kind.FeralBrute : Kind.Feral, Feral, at);
            }
        }
    }

    /// <summary>Where a feral with nothing in reach goes: a guard back to its post; the rest to the nearest building of
    /// either side (the HQ at worst).</summary>
    FixVec2 FeralGoal(Entity e)
    {
        if (e.HasRally) return e.Rally;
        Entity best = null;
        var bestD = Fix.Zero;
        foreach (var b in Entities)
        {
            if (b.Dead || b.Owner == Feral || !b.Stats.IsBuilding) continue;
            var d = (b.Pos - e.Pos).LengthSq;
            if (best == null || d < bestD) { best = b; bestD = d; }
        }
        return best?.Pos ?? e.Pos;
    }

    // ---- radar -----------------------------------------------------------------------------------------------

    /// <summary>A base's radar reach: the HQ's wide, a set-up mobile base's less.</summary>
    public static Fix RadarOf(Entity b) => b.Kind == Kind.Hq ? HqRadar : OutpostRadar;

    /// <summary>Every RadarTicks, each side's finished bases sweep their range: every enemy unit in it that the side
    /// can't see shows as a blip where it is, and the side remembers it there (so its units go to meet it).</summary>
    void Radar()
    {
        if (Tick % RadarTicks != 0) return;
        foreach (var p in Players)
        {
            p.Blips.Clear();
            p.BlipTick = Tick;
            foreach (var e in Entities)
            {
                if (e.Dead || e.Owner == p.Id || !e.Stats.IsUnit || CanSee(p.Id, e)) continue;
                bool inRange = false;
                foreach (var b in Entities)
                {
                    if (b.Dead || b.Owner != p.Id || !IsBase(b) || b.Building > 0) continue;
                    var r = RadarOf(b);
                    if ((b.Pos - e.Pos).LengthSq <= r * r) { inRange = true; break; }
                }
                if (!inRange) continue;
                p.Blips.Add(e.Pos);
                p.Known[e.Id] = e.Pos;
                if (e.Stats.Flying) p.KnownFliers.Add(e.Id);
            }
        }
    }

    // ---- fittings and fire (sulfur) ------------------------------------------------------------------------------

    /// <summary>Which factories can fit napalm: the shell-firers, and the Lattice's crawlers (incendiary spit).</summary>
    public static bool CanFitNapalm(Kind makes) => makes is Kind.Mortar or Kind.Artillery or Kind.Crawler;

    public bool CanSetFitting(int player, int factoryId, Fitting fitting, out string why)
    {
        why = null;
        var f = Find(factoryId);
        if (f == null || f.Kind != Kind.Factory || f.Owner != player) { why = "Not your factory"; return false; }
        if (fitting == Fitting.Napalm && !CanFitNapalm(f.Makes)) { why = "These can't carry napalm"; return false; }
        if (fitting == Fitting.Napalm && SulfurOf(player) == 0) { why = "Needs sulfur: mine a sulfur patch"; return false; }
        return true;
    }

    /// <summary>A patch of burning napalm: hurts the other side's ground units standing in it.</summary>
    public sealed class FirePatch { public FixVec2 At; public Fix Radius; public int Owner, TicksLeft; public Entity By; }
    readonly List<FirePatch> _fires = new();
    public IReadOnlyList<FirePatch> Fires => _fires;

    void AddFire(FixVec2 at, Fix radius, Entity by)
    {
        foreach (var f in _fires)   // (one already burning there: it flares up again, rather than stacking)
            if (f.Owner == by.Owner && (f.At - at).LengthSq <= f.Radius * f.Radius / 4)
            {
                f.TicksLeft = FireTicks;
                f.Radius = Fix.Max(f.Radius, radius);
                return;
            }
        _fires.Add(new FirePatch { At = at, Radius = radius, Owner = by.Owner, TicksLeft = FireTicks, By = by });
    }

    /// <summary>Sets ground alight, as if a napalm hit from `by` landed there (for tests, and the game's --napalm view).</summary>
    public void Ignite(FixVec2 at, Fix radius, Entity by) => AddFire(at, radius, by);

    /// <summary>Every half second, every unit of the other side's on the ground in a fire takes 2 (1 if armoured).</summary>
    void BurnFires()
    {
        foreach (var f in _fires)
        {
            f.TicksLeft--;
            if (f.TicksLeft % 5 != 0) continue;
            foreach (var o in Entities)
            {
                if (o.Dead || o.Owner == f.Owner || o.Hp <= 0 || o.Stats.Flying || !o.Stats.IsUnit) continue;
                var reach = f.Radius + o.Stats.Radius;
                if ((o.Pos - f.At).LengthSq <= reach * reach) _hits.Add((o, o.Stats.Armoured ? 1 : 2, f.By));
            }
        }
        _fires.RemoveAll(f => f.TicksLeft <= 0);
    }

    /// <summary>A shot's damage now: sulfur boosts explosives; napalm trades some of the hit for the fire.</summary>
    int ShotDamage(Entity e)
    {
        int d = e.Damage;
        if (e.Stats.Explosive) d = d * (100 + SulfurBonusPercent * SulfurOf(e.Owner)) / 100;
        if (e.Fitting == Fitting.Napalm && !e.Enhanced) d = d * NapalmHitPercent / 100;
        if (e.Enhanced) d = d * SulfurHitPercent / 100;
        return d;
    }

    // ---- the utility wing: scouts and mobile bases to order ---------------------------------------------------

    /// <summary>Scouts a player has out, and ordered.</summary>
    public int ScoutCount(int player)
    {
        int n = 0;
        foreach (var e in Entities)
        {
            if (e.Dead || e.Owner != player) continue;
            if (IsScout(e.Kind)) n++;
            if (IsUtility(e)) foreach (var o in e.Orders) if (IsScout((Kind)o)) n++;
        }
        return n;
    }

    /// <summary>What an order at a utility wing costs.</summary>
    public static int OrderCost(Kind what) => what == Kind.MobileBase ? MobileBaseCost : ScoutCost;

    /// <summary>Whether the player can order `what` (their scout, or a mobile base) at utility wing `wingId`.</summary>
    public bool CanOrder(int player, int wingId, Kind what, out string why)
    {
        why = null;
        var w = Find(wingId);
        if (w == null || !IsUtility(w) || w.Owner != player) { why = "Needs a utility wing"; return false; }
        if (w.Building > 0) { why = "The utility wing is still going up"; return false; }
        if (what != Kind.MobileBase && what != ScoutKindOf(Players[player].Civ)) { why = "It doesn't make that"; return false; }
        if (Players[player].Metal < OrderCost(what)) { why = "Not enough metal"; return false; }
        if (IsScout(what) && ScoutCount(player) >= MaxScouts) { why = $"At most {MaxScouts} scouts"; return false; }
        if (what == Kind.MobileBase && BaseCount(player) >= MaxBases) { why = $"At most {MaxBases} bases"; return false; }
        if (w.Orders.Count >= 5) { why = "Its queue is full"; return false; }
        return true;
    }

    /// <summary>The player's first finished utility wing, if any (for the scout key, and the AI).</summary>
    public Entity UtilityOf(int player) => Entities.Find(e => !e.Dead && e.Owner == player && IsUtility(e) && e.Building == 0);

    Entity HqOf(int player) => Entities.Find(e => !e.Dead && e.Kind == Kind.Hq && e.Owner == player);

    /// <summary>Advances one tick. <paramref name="commands"/> must be in the same order on every machine.</summary>
    public void Step(IReadOnlyList<Command> commands)
    {
        Tick++;
        Shots.Clear();
        Salvaged.Clear();
        if (Winner >= 0) return;

        if (!Started)
        {
            // Setup: only building up the lines (wings, modules); nothing else moves until both sides have one.
            // (The user, 2026-10-19: the AI placing at once while a person is still choosing was a big edge.)
            foreach (var c in commands)
                if (c is AddWing or AddWingAt or AddModule or UpgradeFactory or CancelUpgrades or SetRally) Apply(c);
            Started = System.Array.TrueForAll(Players, p => Entities.Exists(e => e.Kind == Kind.Factory && e.Owner == p.Id));
            if (Started) StartTick = Tick;
            UpdateVision();
            return;
        }

        foreach (var c in commands) Apply(c);

        if (Paused)
        {
            if (--PauseTicksLeft <= 0) EndPause();
            UpdateVision();
            return;
        }

        if (Tick % TicksPerSecond == 0)
        {
            var income = new int[Players.Length];
            for (int i = 0; i < Players.Length; i++) income[i] = IncomeOf(i);
            for (int i = 0; i < Players.Length; i++) { Players[i].Metal += income[i]; Players[i].Mined += income[i]; }
        }
        BuyQueuedUpgrades();
        Construct();
        Produce();
        if (FeralsOn) SendWaves();
        Fight();
        Separate();
        Settle();
        Reap();
        UpdateVision();
        Radar();
    }

    /// <summary>
    /// Fog of war: which side sees what. Something is seen by its own side, and by the other side if anything of
    /// theirs is within its vision of it (edge to edge, so a tank sees exactly as far as it shoots). Units can only
    /// target what their side sees: artillery outranges its own sight and needs others to spot for it.
    /// </summary>
    void UpdateVision()
    {
        foreach (var e in Entities) e.SeenBy = 1 << e.Owner;
        foreach (var a in Entities)
        {
            if (a.Dead) continue;
            foreach (var b in Entities)
            {
                if (b.Dead || b.Owner == a.Owner || (a.SeenBy & (1 << b.Owner)) != 0) continue;
                var reach = VisionOf(b) + HighGround(b) + b.Stats.Radius + a.Stats.Radius;   // a hill lets it see further
                if ((a.Pos - b.Pos).LengthSq <= reach * reach) a.SeenBy |= 1 << b.Owner;
            }
        }
        Remember();
    }

    /// <summary>
    /// Each side's memory of enemies (Player.Known): where each was last seen. Updated while in sight; forgotten when
    /// it dies, or when the side looks at where it was and it's gone. The enemy HQ is always known.
    /// </summary>
    void Remember()
    {
        foreach (var p in Players)
        {
            int bit = 1 << p.Id;
            foreach (var e in Entities)
                if (e.Owner != p.Id && !e.Dead && ((e.SeenBy & bit) != 0 || e.Kind == Kind.Hq))
                {
                    p.Known[e.Id] = e.Pos;
                    if (e.Stats.Flying) p.KnownFliers.Add(e.Id);
                }
            _forget.Clear();
            foreach (var (id, at) in p.Known)
            {
                var e = Find(id);
                if (e == null) { _forget.Add(id); continue; }
                if ((e.SeenBy & bit) != 0 || e.Kind == Kind.Hq) continue;
                // Out of sight: forget it if any of ours can see where it was (it has moved on).
                foreach (var mine in Entities)
                {
                    if (mine.Owner != p.Id || mine.Dead) continue;
                    var reach = VisionOf(mine) + HighGround(mine) + mine.Stats.Radius;
                    if ((at - mine.Pos).LengthSq <= reach * reach) { _forget.Add(id); break; }
                }
            }
            foreach (var id in _forget) { p.Known.Remove(id); p.KnownFliers.Remove(id); }
        }
    }

    readonly List<int> _forget = new();

    // ---- high ground (the user, 2026-10-19: "hills should matter") ---------------------------------------------

    /// <summary>Extra sight from standing high: a unit of vision per unit of height, up to MaxHighGround.</summary>
    static Fix HighGround(Entity e) => Fix.Max(Fix.Zero, Fix.Min(MaxHighGround, e.Height));

    /// <summary>Extra reach firing down at something lower: the drop, up to MaxDownhillRange.</summary>
    static Fix DownhillRange(Entity from, Entity at) => Fix.Max(Fix.Zero, Fix.Min(MaxDownhillRange, from.Height - at.Height));

    /// <summary>A step over the ground: half as fast again climbing a 1-in-1 slope (at worst half speed), a little
    /// quicker going down (at most a quarter).</summary>
    static FixVec2 OverGround(Entity e, FixVec2 step)
    {
        var len = step.Length;
        if (len.Raw == 0) return step;
        var rise = (Ground.HeightAt(e.Pos + step) - e.Height) / len;
        var factor = Fix.Max(Fix.Ratio(1, 2), Fix.Min(Fix.Ratio(5, 4), Fix.One - rise / 2));
        return step * factor;
    }

    // ---- getting round the water and the ridge (Nav) ---------------------------------------------------------

    readonly Nav _nav = new();

    /// <summary>A way round the water and buildings from a to b (corners), or null (for the AI planning its network).</summary>
    public List<FixVec2> PathBetween(FixVec2 a, FixVec2 b) => _nav.Path(a, b);

    /// <summary>One tick's step for a unit heading for `goal`, at most `most` long (before the ground's slope): straight
    /// if the way is clear, else along a found path, corner by corner. The path is found again when the goal has moved
    /// on or every 5 s.</summary>
    FixVec2 Drive(Entity e, FixVec2 goal, Fix most, Fix goalRadius = default)
    {
        if (e.Stats.Flying)   // straight there, over water, hills and buildings alike
        {
            var line = goal - e.Pos;
            return line.LengthSq.Raw == 0 ? FixVec2.Zero : line.WithLength(Fix.Min(most, line.Length));
        }
        var aim = goal;
        var spare = goalRadius + 2;   // (a building it's driving up to doesn't count as in the way)
        if (_nav.ClearOfAll(e.Pos, goal, spare)) e.NavPath = null;
        else
        {
            // Planned again when the goal has moved on, every 5 s, or when a building has gone up or come down since.
            if (e.NavPath == null || e.NavPath.Count == 0 || (e.NavGoal - goal).LengthSq > Fix.FromInt(16) || Tick - e.NavTick > 50
                || e.NavVersion != _nav.Version)
            {
                e.NavPath = _nav.Path(e.Pos, goal);
                e.NavGoal = goal;
                e.NavTick = Tick;
                e.NavVersion = _nav.Version;
            }
            if (e.NavPath != null && e.NavPath.Count > 0)
            {
                // Past a corner (or one further on is already in view): on to the next.
                while (e.NavPath.Count > 1 && ((e.NavPath[0] - e.Pos).LengthSq <= Fix.One
                       || _nav.ClearOfAll(e.Pos, e.NavPath[1], e.NavPath.Count == 2 ? spare : Fix.Zero)))
                    e.NavPath.RemoveAt(0);
                aim = e.NavPath[0];
                most = Fix.Min(most, Fix.Max((aim - e.Pos).Length, s_minStep));
            }
        }
        var to = aim - e.Pos;
        if (to.LengthSq.Raw == 0) return FixVec2.Zero;
        return OverGround(e, to.WithLength(Fix.Min(most, to.Length)));
    }

    static readonly Fix s_minStep = Fix.Ratio(1, 10);

    /// <summary>A move that would end in water or up a cliff slides along it instead (or stops).</summary>
    static FixVec2 Slide(FixVec2 from, FixVec2 step)
    {
        var to = from + step;
        if (Nav.OpenAt(to) || !Nav.OpenAt(from)) return to;   // (already somewhere shut: let it out)
        var alongX = from + new FixVec2(step.X, Fix.Zero);
        if (Nav.OpenAt(alongX)) return alongX;
        var alongZ = from + new FixVec2(Fix.Zero, step.Z);
        return Nav.OpenAt(alongZ) ? alongZ : from;
    }

    /// <summary>Everyone's height, after they've moved.</summary>
    void Settle()
    {
        foreach (var e in Entities) e.Height = Ground.HeightAt(e.Pos);
    }

    /// <summary>Where the nearest enemy this side knows of is (or was last seen).</summary>
    FixVec2 NearestKnown(int player, FixVec2 from, bool hitsAir)
    {
        var me = Players[player];
        var best = Players[1 - player].HqPos;   // (the enemy HQ at worst)
        var bestD = (best - from).LengthSq;
        foreach (var (id, at) in me.Known)
        {
            if (!hitsAir && me.KnownFliers.Contains(id)) continue;   // (it can't shoot that: don't chase it)
            var d = (at - from).LengthSq;
            if (d < bestD) { best = at; bestD = d; }
        }
        return best;
    }

    public static bool CanSee(int player, Entity e) => (e.SeenBy & (1 << player)) != 0;

    void Apply(Command c)
    {
        var p = Players[c.Player];
        if (Paused)
        {
            if (c is Resume && c.Player == PausedBy) { EndPause(); return; }
            if (c is not Pause and not Resume) _heldDuringPause.Add(c);   // takes effect on resume
            return;
        }
        switch (c)
        {
            case Pause when p.PausesLeft > 0:
                p.PausesLeft--;
                PausedBy = c.Player;
                PauseTicksLeft = PauseTicks;
                break;
            case AddWing aw when CanAddWing(aw.Player, aw.BaseId, aw.Makes, out _):
                var onto = Find(aw.BaseId);
                AddPiece(aw.Player, onto, FreeWingSlot(onto), aw.Makes, null, FixVec2.Zero);
                break;
            case AddWingAt wa when CanAddPiece(wa.Player, wa.BaseId, wa.Slot, wa.Makes, null, wa.Toward, out _):
                AddPiece(wa.Player, Find(wa.BaseId), wa.Slot, wa.Makes, null, wa.Toward);
                break;
            case AddModule am when CanAddPiece(am.Player, am.BaseId, am.Slot, Kind.Hq, am.Mod, am.Toward, out _):
                AddPiece(am.Player, Find(am.BaseId), am.Slot, Kind.Hq, am.Mod, am.Toward);
                break;
            case Order od when CanOrder(od.Player, od.WingId, od.What, out _):
                p.Metal -= OrderCost(od.What);
                var maker = Find(od.WingId);
                if (maker.Orders.Count == 0) maker.Timer = StatsFor[(int)od.What].BuildTicks;
                maker.Orders.Add((int)od.What);
                break;
            case QueueScout when UtilityOf(c.Player) is { } util && CanOrder(c.Player, util.Id, ScoutKindOf(p.Civ), out _):
                p.Metal -= ScoutCost;
                if (util.Orders.Count == 0) util.Timer = StatsFor[(int)ScoutKindOf(p.Civ)].BuildTicks;
                util.Orders.Add((int)ScoutKindOf(p.Civ));
                break;
            case Deploy dp when CanDeploy(dp.Player, dp.UnitId, out _):
                var truck = Find(dp.UnitId);
                truck.Dead = true;   // (gone at the end of the tick; the base stands where it stopped)
                var set = Spawn(Kind.Outpost, dp.Player, truck.Pos);
                set.Hp = set.MaxHp * truck.Hp / truck.MaxHp;
                break;
            case PackUp pu when CanPackUp(pu.Player, pu.BaseId, out _):
                var packed = Find(pu.BaseId);
                packed.Dead = true;
                var moving = Spawn(Kind.MobileBase, pu.Player, packed.Pos);
                moving.Hp = moving.MaxHp * packed.Hp / packed.MaxHp;
                break;
            case SetFitting sf when CanSetFitting(sf.Player, sf.FactoryId, sf.Fitting, out _):
                Find(sf.FactoryId).Fitting = sf.Fitting;
                break;
            case PlaceDefence pd when CanPlaceDefence(pd.Player, pd.Pos, pd.What, out _):
                p.Metal -= CostOf(pd.What);
                Spawn(pd.What, pd.Player, pd.Pos);
                break;
            case PlacePlant pp when CanPlacePlant(pp.Player, pp.Vent, out _):
                p.Metal -= PlantCost;
                Spawn(Kind.PowerPlant, pp.Player, Vents[pp.Vent]).Vent = pp.Vent;
                break;
            case PlaceExtractor pe when CanPlaceExtractor(pe.Player, pe.Deposit, out _):
                p.Metal -= ExtractorCost;
                Spawn(Kind.Extractor, pe.Player, Deposits[pe.Deposit]).Deposit = pe.Deposit;
                break;
            case MoveUnits mu:
                // Each scout (or mobile base) gets its own spot round the point, so a group doesn't jostle for one place.
                int slot = 0;
                var given = new HashSet<int>();
                foreach (var id in mu.Ids)
                {
                    var u = Find(id);
                    if (u == null || !IsSteered(u.Kind) || u.Owner != mu.Player || !given.Add(id)) continue;
                    var spot = mu.Pos + SpreadOffsets[slot++ % SpreadOffsets.Length];
                    u.HasWaypoint = true;
                    u.Waypoint = new FixVec2(Clamp(spot.X, MapHalfX - 1), Clamp(spot.Z, MapHalfZ - 1));
                }
                break;
            case UpgradeFactory uf when CanQueueUpgrade(uf.Player, uf.FactoryId, uf.Which, out _):
                // Bought at once if it can be (nothing queued ahead, the metal there); otherwise queued.
                var f = Find(uf.FactoryId);
                if (CanUpgrade(uf.Player, uf.FactoryId, uf.Which, out _)) BuyUpgrade(f, uf.Which);
                else if (f.UpgradeQueue.Count < MaxUpgradeLevel * 3) f.UpgradeQueue.Add((int)uf.Which);
                break;
            case CancelUpgrades cu when Find(cu.BuildingId) is { } queued && queued.Owner == cu.Player:
                queued.UpgradeQueue.Clear();   // (nothing was paid for them)
                break;
            case Sell se when CanSell(se.Player, se.BuildingId, out _):
                var sold = Find(se.BuildingId);
                p.Metal += SellValue(sold);
                sold.Dead = true;   // gone at the end of this tick (Reap)
                break;
            case SetRally sr:
                var rallying = Find(sr.FactoryId);
                if (rallying == null || rallying.Kind is not (Kind.Factory or Kind.Hq or Kind.Outpost) || rallying.Owner != sr.Player) break;
                rallying.HasRally = !sr.Clear;
                rallying.Rally = new FixVec2(Clamp(sr.Pos.X, MapHalfX - 2), Clamp(sr.Pos.Z, MapHalfZ - 2));
                break;
        }
    }

    static readonly FixVec2[] SpreadOffsets =
    {
        new(0, 0), new(2, 0), new(-2, 0), new(0, 2), new(0, -2), new(2, 2), new(-2, -2), new(2, -2), new(-2, 2),
    };

    void EndPause()
    {
        PausedBy = -1;
        PauseTicksLeft = 0;
        var held = _heldDuringPause.ToArray();
        _heldDuringPause.Clear();
        foreach (var c in held) Apply(c);
    }

    void Produce()
    {
        RefreshLines();
        var supply = new int[Players.Length];
        var draw = new int[Players.Length];
        for (int i = 0; i < Players.Length; i++) { supply[i] = PowerSupply(i); draw[i] = PowerDrawOf(i); }
        var army = new int[Players.Length];
        foreach (var e in Entities) if (!e.Dead && IsArmy(e.Kind)) army[e.Owner]++;
        int count = Entities.Count;   // units made now don't act until next tick
        for (int i = 0; i < count; i++)
        {
            var f = Entities[i];
            if (f.Dead || f.Kind != Kind.Factory || f.Building > 0) continue;   // (making nothing till built)
            if (!Work(f, supply[f.Owner], draw[f.Owner])) continue;   // (short of power: this tick it waits)
            if (IsUtility(f)) { WorkOrders(f); continue; }
            // At the army cap a factory stands idle, its next unit not started: losses take a full build to replace, so
            // winning a fight opens a window (finished units waiting at the door refilled the army at once: stalemate).
            if (army[f.Owner] >= MaxArmyOf(Players[f.Owner].Civ)) { f.Timer = BuildTicksOf(f); continue; }
            f.Timer--;
            if (f.Timer > 0) continue;
            f.Timer = BuildTicksOf(f);
            army[f.Owner]++;
            Spawn(f.Makes, f.Owner, DoorOf(f, f.Makes), from: f);
            f.Made++;
            // A twin feed: every few units, a second one out with it (one twin feed every third, two every other).
            int twins = f.Mods[(int)Mod.Twin];
            if (twins > 0 && f.Made % System.Math.Max(2, TwinEvery + 1 - twins) == 0 && army[f.Owner] < MaxArmyOf(Players[f.Owner].Civ))
            {
                army[f.Owner]++;
                Spawn(f.Makes, f.Owner, DoorOf(f, f.Makes), from: f);
            }
        }
    }

    /// <summary>A fighting unit, counted against the army cap (scouts aren't).</summary>
    public static bool IsArmy(Kind k) => System.Array.IndexOf(FactoryKinds, k) >= 0 || System.Array.IndexOf(LatticeKinds, k) >= 0;

    public int ArmyCount(int player)
    {
        int n = 0;
        foreach (var e in Entities) if (!e.Dead && e.Owner == player && IsArmy(e.Kind)) n++;
        return n;
    }

    /// <summary>Where a wing's units roll out: out the far side from its base (away from the base and the other wings),
    /// with a little spread so they don't stack.</summary>
    FixVec2 DoorOf(Entity wing, Kind unit)
    {
        var dir = DoorDir(wing);
        return wing.Pos + dir.WithLength(wing.Stats.Radius + StatsFor[(int)unit].Radius + 1) + new FixVec2(0, Fix.Ratio(Rng.Range(-10, 10), 10));
    }

    /// <summary>A utility wing works through what's been ordered, one at a time; each rolls out and waits a few paces
    /// on for orders (at the wing's rally point, if it has one).</summary>
    void WorkOrders(Entity wing)
    {
        if (wing.Orders.Count == 0 || --wing.Timer > 0) return;
        var what = (Kind)wing.Orders[0];
        wing.Orders.RemoveAt(0);
        if (wing.Orders.Count > 0) wing.Timer = StatsFor[wing.Orders[0]].BuildTicks;
        var door = DoorOf(wing, what);
        var made = Spawn(what, wing.Owner, door);
        made.HasWaypoint = true;
        made.Waypoint = wing.HasRally ? wing.Rally : door + (door - wing.Pos).WithLength(Fix.FromInt(6));
    }

    /// <summary>
    /// Everyone decides from where everyone stood at the start of the tick, then all moves and all damage land
    /// together. Moving one by one let whoever came first in the list (player 0's things are made first) act on
    /// positions the others hadn't had a turn to change: player 0 won 37 of 60 AI matches even with the AIs swapped.
    /// </summary>
    void Fight()
    {
        _moves.Clear();
        LandShells();   // shells fired earlier come down now, with everyone where they stood at the start of the tick
        BurnFires();
        foreach (var e in Entities)
        {
            if (e.Dead || e.Hp <= 0) continue;
            var s = e.Stats;
            if (e.Cooldown > 0) e.Cooldown--;
            if (e.Building > 0) continue;   // (a gun going up doesn't fire yet)
            if (s.Shields) { Escort(e); continue; }
            if (s.Reload == 0)
            {
                // Unarmed (scouts): only go where they're sent, and stop there.
                if (!s.IsUnit || !e.HasWaypoint) continue;
                var to = e.Waypoint - e.Pos;
                if (to.LengthSq <= Fix.Ratio(1, 2) * Fix.Ratio(1, 2)) { e.HasWaypoint = false; continue; }
                _moves.Add((e, Drive(e, e.Waypoint, SpeedOf(e))));
                continue;
            }

            if (e.Kind == Kind.Bomber) { FlyBomber(e); continue; }
            if (s.Heals) { Mend(e); continue; }
            var target = NearestEnemy(e, AggroOf(e) + MaxDownhillRange, s.MinRange);   // (a hill may let it reach past its aggro)
            // Artillery with an enemy too close to shoot holds still: that's its weakness.
            if (target == null && s.MinRange.Raw > 0 && NearestEnemy(e, AggroOf(e) + MaxDownhillRange, Fix.Zero) != null) continue;
            if (target != null)
            {
                var gap = (target.Pos - e.Pos).Length - s.Radius - target.Stats.Radius;
                var reach = RangeOf(e) + DownhillRange(e, target);   // firing down from a hill reaches further
                if (e.Cooldown > 0 && s.IsUnit) reach += 1;   // (just fired: a nudge from a neighbour doesn't send it shuffling back in)
                if (gap <= reach)
                {
                    if (e.Cooldown == 0) Fire(e, WeakestInReach(e, target));
                    continue;
                }
                if (s.IsUnit)
                {
                    // Behind friends already firing: swing out round the target to a flank, so the whole group gets
                    // to shoot (the user, 2026-10-22: "some just sit behind the front line"). Otherwise close in, a
                    // little inside its reach: closing to the very edge, it never caught anything moving away.
                    if (ChaseRound(e, target, reach) is { } flank) _moves.Add((e, FanOut(e, Drive(e, flank, SpeedOf(e)))));
                    else _moves.Add((e, FanOut(e, Drive(e, target.Pos, Fix.Min(SpeedOf(e), gap - reach + Fix.Ratio(1, 2)), target.Stats.IsBuilding ? target.Stats.Radius : Fix.Zero))));
                }
            }
            else if (s.IsUnit && s.AirOnly)
                _moves.Add((e, Drive(e, AirGoal(e), SpeedOf(e))));
            else if (s.IsUnit && e.Owner == Feral)
            {
                var home = FeralGoal(e);
                if (!e.HasRally || (home - e.Pos).LengthSq > Fix.FromInt(9)) _moves.Add((e, FanOut(e, Drive(e, home, SpeedOf(e)))));
            }
            else if (s.IsUnit)
            {
                // No enemy in sight: first to its rally point if it was given one, then on to the nearest enemy its side
                // knows of (seen now, or where last seen; the enemy HQ at worst).
                // (The user, 2026-10-19: "the rallied units should move there first and then to the nearest known or
                // last seen enemy".) Close enough counts as there: a crowd can't all stand on the spot.
                if (e.HasWaypoint && (e.Waypoint - e.Pos).LengthSq <= WaypointReach * WaypointReach) e.HasWaypoint = false;
                var goal = e.HasWaypoint ? e.Waypoint : NearestKnown(e.Owner, e.Pos, s.HitsAir);
                _moves.Add((e, FanOut(e, Drive(e, goal, SpeedOf(e)))));
            }
        }
        foreach (var (e, step) in _moves) Move(e, Steer(e, step));
        _domes.Clear();
        foreach (var e in Entities)
            if (!e.Dead && e.Hp > 0 && e.Building == 0 && e.Stats.Shields) _domes.Add(e);
        foreach (var (target, hit, by) in _hits)
        {
            int damage = _domes.Count > 0 && Domed(target) ? hit * (100 - DomePercent) / 100 : hit;   // (under a bulwark's dome)
            bool alive = target.Hp > 0;
            int soaked = System.Math.Min(target.Shield, damage);   // a shield coil's shield goes first
            target.Shield -= soaked;
            target.Hp -= damage - soaked;
            if (alive && target.Hp <= 0 && by != null)   // the shot that finished it
            {
                Credit(by);
                if (by.Owner != Feral && by.Owner != target.Owner && Salvage(target.Kind) > 0)   // salvage
                {
                    int pay = Salvage(target.Kind) * (100 + MagnetPercent * by.Mods[(int)Mod.Magnet]) / 100;   // (a salvage magnet pays more)
                    Players[by.Owner].Metal += pay;
                    Players[by.Owner].Salvaged += pay;
                    Salvaged.Add((target.Pos, by.Owner, pay));
                }
            }
        }
        _hits.Clear();
    }

    /// <summary>
    /// A mender: to the nearest damaged unit of its side it can see to (its Aggro), and once in reach sends a drone
    /// that gives back its Damage in health. With nobody to mend it keeps a few lengths behind the nearest friendly
    /// fighter (or goes where it was sent).
    /// </summary>
    void Mend(Entity e)
    {
        var s = e.Stats;
        Entity patient = null;
        Fix best = Fix.FromInt(1 << 20);
        foreach (var o in Entities)
        {
            if (o == e || o.Dead || o.Owner != e.Owner || !o.Stats.IsUnit || o.Building > 0) continue;
            var d = (o.Pos - e.Pos).LengthSq;
            if (o.Hp < o.MaxHp && d < best && d <= (s.Aggro + o.Stats.Radius) * (s.Aggro + o.Stats.Radius)) { best = d; patient = o; }
        }
        if (patient != null)
        {
            var gap = (patient.Pos - e.Pos).Length - s.Radius - patient.Stats.Radius;
            if (gap <= s.Range)
            {
                if (e.Cooldown == 0)
                {
                    e.Cooldown = s.Reload;
                    patient.Hp = System.Math.Min(patient.MaxHp, patient.Hp + e.Damage);
                    Shots.Add(new Shot(e.Pos, patient.Pos, e.Owner, e.Kind, e.Id, patient.Id, 0));
                }
                return;
            }
            _moves.Add((e, FanOut(e, Drive(e, patient.Pos, Fix.Min(SpeedOf(e), gap - s.Range + Fix.Ratio(1, 2))))));
            return;
        }
        Escort(e);
    }

    /// <summary>A support unit with nothing to do: where it was sent, else a few lengths behind the nearest friendly
    /// fighter.</summary>
    void Escort(Entity e)
    {
        if (e.HasWaypoint && (e.Waypoint - e.Pos).LengthSq <= WaypointReach * WaypointReach) e.HasWaypoint = false;
        if (e.HasWaypoint) { _moves.Add((e, FanOut(e, Drive(e, e.Waypoint, SpeedOf(e))))); return; }
        Entity escort = null;
        Fix near = Fix.FromInt(1 << 20);
        foreach (var o in Entities)
        {
            if (o == e || o.Dead || o.Owner != e.Owner || !o.Stats.IsUnit || o.Building > 0 || o.Stats.Reload == 0 || o.Stats.Heals) continue;
            var d = (o.Pos - e.Pos).LengthSq;
            if (d < near) { near = d; escort = o; }
        }
        if (escort != null && near > Fix.FromInt(5 * 5)) _moves.Add((e, FanOut(e, Drive(e, escort.Pos, SpeedOf(e)))));
    }

    /// <summary>Whether `e` is under a dome of its own side (<see cref="DomeReach"/>).</summary>
    bool Domed(Entity e)
    {
        foreach (var b in _domes)
            if (b.Owner == e.Owner && (b.Pos - e.Pos).LengthSq <= (DomeReach + e.Stats.Radius) * (DomeReach + e.Stats.Radius)) return true;
        return false;
    }
    readonly List<Entity> _domes = new();

    /// <summary>
    /// Records the damage a shot does; it lands after everyone has fired (<see cref="Fight"/>). Applying it at once
    /// let whichever unit came first in the list kill before the other could shoot back, and player 0's units tend to
    /// come first (their commands apply first): he won 21 of 30 AI matches.
    /// </summary>
    void Fire(Entity e, Entity target)
    {
        var s = e.Stats;
        e.Cooldown = s.Reload;
        int damage = ShotDamage(e);
        if (s.Splash.Raw == 0)
        {
            Shots.Add(new Shot(e.Pos, target.Pos, e.Owner, e.Kind, e.Id, target.Id, 0));
            _hits.Add((target, Hurt(e, target, damage), e));
            if (e.Fitting == Fitting.Napalm || e.Enhanced) AddFire(target.Pos, Fix.Ratio(15, 10), e);
            return;
        }

        // A shell: it's lobbed at the ground where the target stands and lands a while later, hurting whoever is
        // there then. Something moving gets out from under it (the user, 2026-10-20: "artillery should be able to
        // miss, it attacks ground where a unit is but if it's moving it misses").
        int flight = ShellFlightTicks(e.Pos, target.Pos);
        Shots.Add(new Shot(e.Pos, target.Pos, e.Owner, e.Kind, e.Id, target.Id, flight));
        _shells.Add(new Shell { At = target.Pos, Owner = e.Owner, Damage = damage, Splash = s.Splash, By = e, TicksLeft = flight, Napalm = e.Fitting == Fitting.Napalm || e.Enhanced });
    }

    /// <summary>What a hit from `by` does to `to`: anti-armour weapons do more to armoured things (Stats.VsArmour).</summary>
    static int Hurt(Entity by, Entity to, int damage)
    {
        if (to.Stats.Armoured) damage = damage * by.Stats.VsArmour / 100;
        if (to.Stats.Flying) damage = damage * by.Stats.VsAir / 100;
        return damage;
    }

    /// <summary>How long a shell is in the air: 0.6 s, and 0.1 s for every 3 units it goes.</summary>
    public static int ShellFlightTicks(FixVec2 from, FixVec2 to) => 6 + (to - from).Length.ToInt() / 3;

    sealed class Shell { public FixVec2 At; public int Owner, Damage, TicksLeft; public Fix Splash; public Entity By; public bool Napalm; }
    readonly List<Shell> _shells = new();

    /// <summary>Shells that come down this tick: their damage joins this tick's hits.</summary>
    void LandShells()
    {
        for (int i = 0; i < _shells.Count; i++)
        {
            var sh = _shells[i];
            if (--sh.TicksLeft > 0) continue;
            if (sh.Napalm) AddFire(sh.At, sh.Splash + 1, sh.By);
            foreach (var o in Entities)
            {
                if (o.Dead || o.Owner == sh.Owner || o.Hp <= 0 || o.Stats.Flying) continue;   // (shells don't reach fliers)
                var reach = sh.Splash + o.Stats.Radius;
                if ((o.Pos - sh.At).LengthSq <= reach * reach) _hits.Add((o, Hurt(sh.By, o, sh.Damage), sh.By));
            }
        }
        _shells.RemoveAll(sh => sh.TicksLeft <= 0);
    }

    readonly List<(Entity Target, int Damage, Entity By)> _hits = new();
    readonly List<(Entity Who, FixVec2 Step)> _moves = new();

    /// <summary>The nearest live enemy within <paramref name="within"/> and no nearer than <paramref name="notNearer"/> (edge to edge).</summary>
    Entity NearestEnemy(Entity from, Fix within, Fix notNearer)
    {
        Entity best = null;
        var bestD = Fix.Zero;
        foreach (var e in Entities)
        {
            if (e.Dead || e.Owner == from.Owner || e.Hp <= 0 || !CanSee(from.Owner, e)) continue;
            if (e.Stats.Flying && !from.Stats.HitsAir) continue;   // (only anti-air guns reach fliers)
            if (!e.Stats.Flying && from.Stats.AirOnly) continue;   // (interceptors fight fliers only)
            var reach = within + e.Stats.Radius;
            var d = (e.Pos - from.Pos).LengthSq;
            if (d > reach * reach) continue;
            if (notNearer.Raw > 0)
            {
                var close = notNearer + e.Stats.Radius + from.Stats.Radius;
                if (d < close * close) continue;
            }
            if (best == null || d < bestD) { best = e; bestD = d; }
        }
        return best;
    }

    /// <summary>
    /// Where a ground unit out of reach of its target goes when friends stand between it and the target (it's behind the
    /// line): a point on the ring round the target at its firing distance, about an eighth of a turn round from where it
    /// is, to one side (the side it picked, kept while it's blocked). Recomputed every tick, it works its way round the
    /// ring until it has a clear line, then closes in. Null when nothing's in the way.
    /// </summary>
    FixVec2? ChaseRound(Entity e, Entity target, Fix reach)
    {
        if (e.Stats.Flying) return null;
        var line = target.Pos - e.Pos;
        var len = line.Length;
        if (len.Raw == 0) return null;
        var dir = line * (Fix.One / len);
        bool blocked = false;
        foreach (var b in Entities)
        {
            if (b == e || b.Dead || b.Owner != e.Owner || !b.Stats.IsUnit || b.Stats.Flying) continue;
            var d = b.Pos - e.Pos;
            var ahead = d.X * dir.X + d.Z * dir.Z;
            if (ahead.Raw <= 0 || ahead >= len) continue;
            var across = Fix.Abs(d.X * dir.Z - d.Z * dir.X);
            if (across < (e.Stats.Radius + b.Stats.Radius) * Fix.Ratio(3, 2)) { blocked = true; break; }
        }
        // Once it starts round it keeps going a second after the way clears (flicking between going round and going
        // straight in made it weave).
        if (blocked) e.FlankTicks = TicksPerSecond;
        else if (e.FlankTicks > 0) e.FlankTicks--;
        if (!blocked && e.FlankTicks == 0) return null;
        if (e.SwaySide == 0) e.SwaySide = e.Id % 2 == 0 ? 1 : -1;
        var from = -dir;
        var round = new FixVec2(-from.Z, from.X) * Fix.FromInt(e.SwaySide);
        var ring = reach + e.Stats.Radius + target.Stats.Radius - Fix.Ratio(1, 2);
        var at = target.Pos + (from + round * Fix.Ratio(45, 100)).WithLength(ring);
        return Nav.OpenAt(at) ? at : null;
    }

    /// <summary>Focus fire: of the enemies this one can shoot right now, the most damaged (ties: the nearest, then
    /// the lowest id). Shooting the nearest spread a side's fire over many and killed none, while the other side
    /// happened to pile onto one: even fights ended 5 to 0.</summary>
    Entity WeakestInReach(Entity from, Entity nearest)
    {
        var best = nearest;
        foreach (var o in Entities)
        {
            if (o == nearest || o.Dead || o.Owner == from.Owner || o.Hp <= 0 || !CanSee(from.Owner, o)) continue;
            if (o.Stats.Flying && !from.Stats.HitsAir || !o.Stats.Flying && from.Stats.AirOnly) continue;
            var gap = (o.Pos - from.Pos).Length - from.Stats.Radius - o.Stats.Radius;
            if (gap > RangeOf(from) + DownhillRange(from, o)) continue;
            if (from.Stats.MinRange.Raw > 0 && gap < from.Stats.MinRange) continue;
            if (o.Hp < best.Hp || o.Hp == best.Hp && o.Id < best.Id) best = o;
        }
        return best;
    }

    /// <summary>
    /// A step turned aside round friends in the way, so a group heading for a fight spreads into a line where all can
    /// shoot instead of queueing behind each other in a clump (the user, 2026-10-22: "they need to try form up in a
    /// line so they can all shoot, they just seem to get stuck in clusters"). A friend counts as in the way if it's
    /// close ahead and near the line of travel; the unit steps off to whichever side the friend isn't on (ties by id,
    /// so both machines agree), mostly sideways the closer the friend is.
    /// </summary>
    FixVec2 FanOut(Entity e, FixVec2 step)
    {
        if (e.Stats.Flying || step.LengthSq.Raw == 0) return step;
        var len = step.Length;
        var fwd = step * (Fix.One / len);                 // unit direction
        var side = new FixVec2(-fwd.Z, fwd.X);            // its left
        Fix sway = Fix.Zero;
        foreach (var b in Entities)
        {
            if (b == e || b.Dead || b.Owner != e.Owner || !b.Stats.IsUnit || b.Stats.Flying) continue;
            var d = b.Pos - e.Pos;
            var gap = e.Stats.Radius + b.Stats.Radius;
            var ahead = d.X * fwd.X + d.Z * fwd.Z;
            if (ahead.Raw <= 0 || ahead > gap * 3) continue;
            var across = d.X * side.X + d.Z * side.Z;
            if (Fix.Abs(across) >= gap) continue;
            // Nearer and more squarely in the way pushes harder.
            var weight = (Fix.One - ahead / (gap * 3)) * (Fix.One - Fix.Abs(across) / gap);
            int away = across.Raw > 0 ? -1 : across.Raw < 0 ? 1 : (e.Id < b.Id ? 1 : -1);
            sway += away > 0 ? weight : -weight;
        }
        if (sway.Raw == 0) { e.SwaySide = 0; return step; }
        // Keep to the side it chose while it's still blocked: flipping back and forth made a crowd weave.
        if (e.SwaySide != 0 && (sway.Raw > 0 ? 1 : -1) != e.SwaySide) sway = e.SwaySide > 0 ? Fix.Abs(sway) : -Fix.Abs(sway);
        e.SwaySide = sway.Raw > 0 ? 1 : -1;
        sway = Fix.Max(-Fix.One, Fix.Min(Fix.One, sway));
        var turned = fwd * (Fix.One - Fix.Abs(sway) / 2) + side * sway;
        if (turned.LengthSq.Raw == 0) return step;
        turned = turned.WithLength(len);
        return Nav.OpenAt(e.Pos + turned) ? turned : step;   // (no sidestepping into water or up a cliff)
    }

    /// <summary>Pushes overlapping units apart (and out of buildings) so they don't stack: every push is worked out
    /// from the same positions, then all are applied (one by one favoured whoever went first).</summary>
    void Separate()
    {
        _moves.Clear();
        for (int i = 0; i < Entities.Count; i++)
        {
            var a = Entities[i];
            if (a.Dead || a.Stats.IsBuilding) continue;
            var total = FixVec2.Zero;
            for (int j = 0; j < Entities.Count; j++)
            {
                if (i == j) continue;
                var b = Entities[j];
                if (b.Dead || b.Stats.Flying != a.Stats.Flying) continue;   // (fliers only jostle fliers)
                var min = a.Stats.Radius + b.Stats.Radius;
                var d = a.Pos - b.Pos;
                var lsq = d.LengthSq;
                if (lsq >= min * min) continue;
                var len = Fix.Sqrt(lsq);
                // Exactly on top of each other: split by id so both machines agree.
                var dir = len.Raw == 0 ? new FixVec2(a.Id < b.Id ? -1 : 1, 0) : d;
                var push = b.Stats.IsBuilding ? min - len : (min - len) / 3;   // (friends ease apart over a few ticks: shoving them apart at once made a crowd shake)
                total += dir.WithLength(push);
            }
            if (total.X.Raw != 0 || total.Z.Raw != 0) _moves.Add((a, total));
        }
        foreach (var (e, step) in _moves) Move(e, step);
    }

    /// <summary>Where an interceptor with nothing in reach goes (the user, 2026-10-22: "fliers should patrol your base
    /// and go out and fight bombers"): after the nearest enemy flier its side knows of (radar finds them), wherever it
    /// is; otherwise round and round over one of its side's bases, each on its own part of the circle.</summary>
    FixVec2 AirGoal(Entity e)
    {
        var me = Players[e.Owner];
        FixVec2? best = null;
        var bestD = Fix.Zero;
        foreach (var (id, at) in me.Known)
        {
            if (!me.KnownFliers.Contains(id)) continue;
            var d = (at - e.Pos).LengthSq;
            if (best == null || d < bestD) { best = at; bestD = d; }
        }
        if (best is { } flier) return flier;
        // Patrol: over the base nearest it, along a circle of PatrolRadius, a point ahead every few ticks.
        Entity home = null;
        foreach (var b in Entities)
            if (!b.Dead && b.Owner == e.Owner && IsBase(b) && (home == null || (b.Pos - e.Pos).LengthSq < (home.Pos - e.Pos).LengthSq)) home = b;
        if (home == null) return me.HqPos;
        int step = (Tick / 15 + e.Id * 5) % PatrolCircle.Length;
        return home.Pos + PatrolCircle[step] * PatrolRadius;
    }

    static readonly Fix PatrolRadius = 18;
    // Sixteen points round a unit circle (fixed point, the same on every machine).
    static readonly FixVec2[] PatrolCircle =
    {
        new(1, 0), new(Fix.Ratio(924, 1000), Fix.Ratio(383, 1000)), new(Fix.Ratio(707, 1000), Fix.Ratio(707, 1000)), new(Fix.Ratio(383, 1000), Fix.Ratio(924, 1000)),
        new(0, 1), new(Fix.Ratio(-383, 1000), Fix.Ratio(924, 1000)), new(Fix.Ratio(-707, 1000), Fix.Ratio(707, 1000)), new(Fix.Ratio(-924, 1000), Fix.Ratio(383, 1000)),
        new(-1, 0), new(Fix.Ratio(-924, 1000), Fix.Ratio(-383, 1000)), new(Fix.Ratio(-707, 1000), Fix.Ratio(-707, 1000)), new(Fix.Ratio(-383, 1000), Fix.Ratio(-924, 1000)),
        new(0, -1), new(Fix.Ratio(383, 1000), Fix.Ratio(-924, 1000)), new(Fix.Ratio(707, 1000), Fix.Ratio(-707, 1000)), new(Fix.Ratio(924, 1000), Fix.Ratio(-383, 1000)),
    };

    static readonly Fix TurnCos = Fix.Ratio(866, 1000), TurnSin = Fix.Ratio(1, 2);   // at most 30 degrees a tick
    static readonly Fix BankCos = Fix.Ratio(974, 1000), BankSin = Fix.Ratio(225, 1000);   // a bomber: at most 13 degrees a tick

    /// <summary>
    /// A bomber flies like a plane (the user, 2026-10-22: "bombers should drop a bomb and fly over in loops"): always
    /// on at full speed, turning only so fast, toward its target (or where its side's army is heading). It drops its
    /// bomb as it passes over, overshoots, loops round and comes over again: with its turn it circles about 3.5 across.
    /// </summary>
    void FlyBomber(Entity e)
    {
        var s = e.Stats;
        var target = NearestEnemy(e, AggroOf(e), Fix.Zero);
        if (target != null && e.Cooldown == 0)
        {
            var gap = (target.Pos - e.Pos).Length - s.Radius - target.Stats.Radius;
            if (gap <= RangeOf(e)) Fire(e, WeakestInReach(e, target));
        }
        var goal = target?.Pos ?? NearestKnown(e.Owner, e.Pos, false);
        var to = goal - e.Pos;
        var h = e.Heading.LengthSq.Raw == 0 ? (to.LengthSq.Raw == 0 ? new FixVec2(1, 0) : to.WithLength(Fix.One)) : e.Heading;
        if (to.LengthSq.Raw > 0)
        {
            var want = to.WithLength(Fix.One);
            if (h.X * want.X + h.Z * want.Z >= BankCos) h = want;
            else
            {
                var left = new FixVec2(-h.Z, h.X);
                h = (h * BankCos + left * ((left.X * want.X + left.Z * want.Z).Raw >= 0 ? BankSin : -BankSin)).WithLength(Fix.One);
            }
        }
        e.Heading = h;
        var next = e.Pos + h * SpeedOf(e);
        if (Fix.Abs(next.X) > MapHalfX - 2 || Fix.Abs(next.Z) > MapHalfZ - 2) next = e.Pos + to.WithLength(SpeedOf(e));   // (turn in from the map's edge)
        _moves.Add((e, next - e.Pos));
    }

    /// <summary>
    /// A ground unit's step, turned from the way it's already heading by at most 30 degrees a tick, and slowed by how far
    /// it still has to turn (so it turns nearly on the spot rather than swinging wide). Without this a unit could flip
    /// straight round from one tick to the next, and a crowd shoved and steered round each other visibly weaved (the user,
    /// 2026-10-22: "large groups of tanks are very bad at pathing still": about one step in ten swung over 60 degrees).
    /// </summary>
    FixVec2 Steer(Entity e, FixVec2 step)
    {
        if (!e.Stats.IsUnit || step.LengthSq < Fix.Ratio(1, 10000)) return step;
        if (e.Stats.Flying) { e.Heading = step.WithLength(Fix.One); return step; }   // (fliers turn on the spot; the heading is for bombing runs)
        var len = step.Length;
        var want = step * (Fix.One / len);
        var h = e.Heading;
        if (h.LengthSq.Raw == 0) { e.Heading = want; return step; }
        var cos = h.X * want.X + h.Z * want.Z;
        if (cos >= TurnCos) { e.Heading = want; return step; }
        var left = new FixVec2(-h.Z, h.X);
        var toLeft = left.X * want.X + left.Z * want.Z;
        var turned = h * TurnCos + left * (toLeft.Raw >= 0 ? TurnSin : -TurnSin);
        turned = turned.WithLength(Fix.One);
        e.Heading = turned;
        var along = turned.X * want.X + turned.Z * want.Z;   // how well it now points the way it wants to go
        return turned * (len * Fix.Max(Fix.Zero, along));
    }

    /// <summary>A step taken: fliers straight over anything, the rest sliding along water and cliffs.</summary>
    void Move(Entity e, FixVec2 step) => e.Pos = e.Stats.Flying ? e.Pos + step : Slide(e.Pos, step);

    void Reap()
    {
        foreach (var e in Entities)
        {
            if (e.Dead || e.Hp > 0) continue;
            e.Dead = true;
            if (e.Kind == Kind.Hq) Winner = 1 - e.Owner;
        }
        // A base's wings go down with it.
        foreach (var e in Entities)
            if (!e.Dead && e.Kind is Kind.Factory or Kind.Module && (Find(e.BaseId) == null || Find(e.BaseId).Dead)) { e.Dead = true; e.Hp = 0; }
        // Buildings gone (destroyed, or sold earlier this tick) no longer stand in anyone's way.
        foreach (var e in Entities)
            if (e.Dead && e.Stats.IsBuilding) _nav.RemoveBuilding(e.Pos, e.Stats.Radius);
        Entities.RemoveAll(e => e.Dead);
    }

    /// <summary>FNV-1a hash of the whole state. Machines in a match compare these to catch desyncs.</summary>
    public ulong Checksum()
    {
        ulong h = 14695981039346656037UL;
        void Mix(long v) { for (int i = 0; i < 8; i++) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; } }
        Mix(Tick); Mix(Winner); Mix(PausedBy); Mix(PauseTicksLeft); Mix((long)Rng.State); Mix(Started ? 1 : 0); Mix(StartTick); Mix(WaveNumber);
        foreach (var p in Players)
        {
            Mix(p.Metal); Mix(p.PausesLeft); Mix(p.Mined); Mix(p.Salvaged);
            foreach (var (id, at) in p.Known) { Mix(id); Mix(at.X.Raw); Mix(at.Z.Raw); }
            Mix(p.BlipTick); foreach (var b in p.Blips) { Mix(b.X.Raw); Mix(b.Z.Raw); }
        }
        foreach (var e in Entities)
        {
            Mix(e.Id); Mix((int)e.Kind); Mix(e.Owner); Mix(e.Pos.X.Raw); Mix(e.Pos.Z.Raw);
            Mix(e.Hp); Mix(e.Cooldown); Mix(e.Timer); Mix((int)e.Makes); Mix(e.MaxHp); Mix(e.Damage); Mix(e.SeenBy); Mix(e.Kills); Mix(e.Rank); Mix(e.FactoryId); Mix(e.HasRally ? 1 : 0); Mix(e.Rally.X.Raw); Mix(e.Rally.Z.Raw); Mix(e.HasWaypoint ? 1 : 0); Mix(e.Waypoint.X.Raw); Mix(e.Waypoint.Z.Raw); Mix(e.BaseId); Mix(e.Deposit); Mix(e.Slot); Mix(e.Building); Mix((int)e.Fitting); Mix(e.Heading.X.Raw); Mix(e.Heading.Z.Raw); Mix(e.SwaySide); Mix(e.FlankTicks); Mix(e.PowerAcc); Mix(e.Enhanced ? 1 : 0); Mix(e.Vent);
            Mix(e.Orders.Count); foreach (var o in e.Orders) Mix(o);
            foreach (var l in e.Levels) Mix(l);
            Mix((int)e.Mod); Mix(e.Link); Mix(e.Shield); Mix(e.Made); foreach (var m in e.Mods) Mix(m);
            Mix(e.UpgradeQueue.Count); foreach (var q in e.UpgradeQueue) Mix(q);
        }
        foreach (var f in _fires) { Mix(f.At.X.Raw); Mix(f.At.Z.Raw); Mix(f.Radius.Raw); Mix(f.Owner); Mix(f.TicksLeft); }
        foreach (var sh in _shells)
        {
            Mix(sh.At.X.Raw); Mix(sh.At.Z.Raw); Mix(sh.Owner); Mix(sh.Damage); Mix(sh.TicksLeft); Mix(sh.By.Id);
        }
        return h;
    }
}
