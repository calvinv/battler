using System.Collections.Generic;

namespace Battler.Sim;

public enum Kind { Hq, Factory, Scout, Tank, Artillery, Tower, Outpost, Extractor, LightTank, Mortar, Spider, Crawler, Piercer, Drone, Node, MobileBase }   // (new kinds go at the end: StatsFor and the wire use the numbers)

/// <summary>A factory's upgrade tracks. They improve the units it makes from then on (not ones already out).</summary>
public enum Upgrade { Production, Damage, Armour }

/// <summary>What a deposit gives (the user, 2026-10-22: resources "impact the way your units can fit and offer upgrades,
/// like you can find a sulfur patch that could increase explosive damage for all units or give you access to napalm").
/// Metal is income; the others are mined for what having them does, not stockpiled.</summary>
public enum Resource { Metal, Sulfur }

/// <summary>What a factory fits its units with. Napalm needs sulfur: hits do a quarter less, but leave the ground
/// burning (World.Fires).</summary>
public enum Fitting { Standard, Napalm }

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
    public bool Armoured;     // heavy plate: anti-armour weapons do extra to it
    public int VsArmour = 100;   // percent of its damage it does to Armoured targets
    public bool Explosive;    // shells, cannon and spikes: sulfur makes these hit harder

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
    public readonly List<int> Orders = new();   // a utility wing: what's paid for and waiting to be built (by Kind), in order
    public readonly List<int> UpgradeQueue = new();   // a factory or outpost: upgrades (by Upgrade) to buy, in order, once affordable
    public List<FixVec2> NavPath;      // a unit: corners to drive through to NavGoal (Nav), when there is no straight way
    public FixVec2 NavGoal;
    public int NavTick;                // when that path was found
    public int NavVersion;             // the buildings as they stood when it was found (Nav.Version)
    public int Building;               // a building: ticks until it's finished (World.ConstructionTicks); it does nothing till then
    public Fitting Fitting;    // a factory: what it fits its units with; a unit: what it was made with

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
    // Resources (the user, 2026-10-20): a trickle of metal comes in regardless; the rest is mined from deposits by
    // extractors, so expanding is how a side gets rich and the middle is worth fighting over.
    public const int IncomePerTick = 1;
    public const int ExtractorIncome = 1;   // per extractor, per tick
    public const int ExtractorCost = 300;
    public static readonly Fix DepositRadius = 3;   // nothing else can be built this near a deposit's centre
    public const int FactoryCost = 500;   // every kind: they're balanced by how often they make a unit
    // Bases and wings (the user, 2026-10-22: "the main hq can build wings on it which produce vehicles, one of the
    // wings is a utility vehicle, that lets you build scouts and mobile bases, you can move a mobile base like a scout
    // and set it up somewhere else"). The HQ takes five wings, a set-up mobile base (Kind.Outpost) three; a wing makes
    // one kind of unit, or (the utility wing) scouts and mobile bases to order. Extractors go near a base.
    // (The branching network this replaced is on the network-prototype branch.)
    public const int HqWings = 5, OutpostWings = 3, MobileBaseCost = 600, MaxBases = 4;
    public static readonly Fix BaseReach = 30, BaseSpacing = 40;
    // Radar (the user, 2026-10-22: "a radar that pings every 30s to show where units are in the fog so you can't get
    // cheesed"): each base sweeps its range every 30 s; hidden enemy units in it show as blips, and become known.
    public const int RadarTicks = 30 * TicksPerSecond;
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
    public static readonly Fix MapHalfX = 140 * Ground.Scale, MapHalfZ = 85 * Ground.Scale;   // (2026-10-20: "4x bigger", four times the area)
    public static readonly Ground Ground = new(140 * Ground.Scale, 85 * Ground.Scale, new[] { HqStart(0), HqStart(1) });
    static readonly Fix MaxHighGround = 5, MaxDownhillRange = 4;

    /// <summary>What factories can make (scouts come from the HQ).</summary>
    // In key order: light tank, tank, mortar, artillery (the user, 2026-10-20: "new cheaper units, a smaller artillery
    // tank and a combat tank": units stay free, the light ones just come quicker).
    public static readonly Kind[] FactoryKinds = { Kind.LightTank, Kind.Tank, Kind.Mortar, Kind.Artillery };   // the Coalition's

    /// <summary>What a civilisation's factories can make, in build-menu order. The Lattice (the user, 2026-10-21: "they
    /// don't get artillery at tier 1 but they get scout fliers and anti armour units"): crawlers, spiders, piercers.</summary>
    public static Kind[] FactoryKindsOf(Civ civ) => civ == Civ.Lattice ? LatticeKinds : FactoryKinds;
    static readonly Kind[] LatticeKinds = { Kind.Crawler, Kind.Spider, Kind.Piercer };

    /// <summary>The scout a civilisation's HQ builds: the Coalition's hovercraft, the Lattice's flying drone.</summary>
    public static Kind ScoutKindOf(Civ civ) => civ == Civ.Lattice ? Kind.Drone : Kind.Scout;
    public static bool IsScout(Kind k) => k is Kind.Scout or Kind.Drone;
    /// <summary>A unit its player steers (scouts and mobile bases); the rest fight on their own.</summary>
    public static bool IsSteered(Kind k) => IsScout(k) || k == Kind.MobileBase;

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

    static World()
    {
        // (deposit, its pad) on player 0's half; player 1's are the same turned round about the middle.
        var west = new (FixVec2 At, FixVec2? Pad)[]
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
        all.Add((new FixVec2(0, 45), null));
        all.Add((new FixVec2(0, -45), null));
        Deposits = all.ConvertAll(d => d.At).ToArray();
        Pads = all.ConvertAll(d => d.Pad ?? d.At).ToArray();
        HasPad = new bool[all.Count];   // (outposts are gone: the network replaced them)
        DepositResource = new Resource[all.Count];
        for (int i = metal; i < all.Count; i++) DepositResource[i] = Resource.Sulfur;
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
    };

    public int Tick;
    public readonly Player[] Players;
    public readonly List<Entity> Entities = new();   // always in Id order
    public readonly List<Shot> Shots = new();         // this tick only
    public readonly Rng Rng;
    public int Winner = -1;
    /// <summary>False during setup, until both sides have placed their first factory.</summary>
    public bool Started;

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
        civs ??= new[] { Civ.Coalition, Civ.Lattice };
        Players = new Player[2];
        for (int i = 0; i < 2; i++)
        {
            var hq = HqStart(i);
            Players[i] = new Player { Id = i, Civ = civs[i], Metal = StartMetal, PausesLeft = PausesPerPlayer, HqPos = hq };
            Spawn(Kind.Hq, i, hq);
            foreach (var side in new[] { -10, 10 }) Spawn(Kind.Tower, i, TowerStart(i, side));
        }
    }

    static Fix Clamp(Fix v, Fix limit) => Fix.Max(-limit, Fix.Min(limit, v));

    // Each HQ on the hill in its corner: player 0 bottom left, player 1 top right (the map turned round).
    public static FixVec2 HqStart(int player) => player == 0 ? new FixVec2(-224, 100) : new FixVec2(224, -100);
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
            e.FactoryId = from.Id;
            e.Fitting = from.Fitting == Fitting.Napalm && SulfurOf(owner) > 0 ? Fitting.Napalm : Fitting.Standard;
            // Its factory's own rally point; failing that the base's (the HQ's: the user, 2026-10-20, "main base
            // should have a way to rally").
            var rally = from.HasRally ? from : HqOf(owner) is { HasRally: true } hq ? hq : null;
            e.HasWaypoint = rally != null;
            if (rally != null) e.Waypoint = rally.Rally;
        }
        SetStats(e);
        e.Hp = e.MaxHp;
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
        if (f == null || f.Kind != Kind.Factory || f.Makes == Kind.MobileBase || f.Owner != player) { why = "Not yours to upgrade"; return false; }
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
        if (f.Kind == Kind.Outpost)   // an outpost's own guns and armour improve at once
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
        int spent = b.Kind switch { Kind.Outpost => MobileBaseCost, Kind.Extractor => ExtractorCost, _ => FactoryCost };
        foreach (var level in b.Levels)
            for (int l = 1; l <= level; l++) spent += UpgradeCost(l);
        return spent;
    }

    public static int SellValue(Entity b) => SpentOn(b) * SellBackPercent / 100;

    public bool CanSell(int player, int id, out string why)
    {
        var b = Find(id);
        why = null;
        if (b == null || b.Kind is not (Kind.Factory or Kind.Outpost or Kind.Extractor) || b.Owner != player) { why = "Not yours to sell"; return false; }
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

    /// <summary>How many wings a base takes: the HQ five, a set-up mobile base three.</summary>
    public static int SlotsOf(Entity b) => b.Kind == Kind.Hq ? HqWings : OutpostWings;

    /// <summary>How many of a base's wing spots are taken.</summary>
    public int SlotsUsed(Entity b)
    {
        int n = 0;
        foreach (var e in Entities) if (!e.Dead && e.Kind == Kind.Factory && e.BaseId == b.Id) n++;
        return n;
    }

    /// <summary>A utility wing: it builds scouts and mobile bases to order, not a stream of fighting units.</summary>
    public static bool IsUtility(Entity e) => e.Kind == Kind.Factory && e.Makes == Kind.MobileBase;

    // The wing spots round a base, for player 0 (player 1's are the same turned round, as the map is): the first faces
    // the enemy, the rest go round. Each one a wing's width off the base's edge, so wings sit against it.
    static readonly FixVec2[] HqWingSpots =
        { new(Fix.Ratio(548, 100), Fix.Ratio(-244, 100)), new(Fix.Ratio(401, 100), Fix.Ratio(446, 100)), new(-3, Fix.Ratio(520, 100)),
          new(Fix.Ratio(-587, 100), Fix.Ratio(-125, 100)), new(Fix.Ratio(-63, 100), Fix.Ratio(-597, 100)) };
    static readonly FixVec2[] OutpostWingSpots =
        { new(Fix.Ratio(475, 100), Fix.Ratio(-212, 100)), new(Fix.Ratio(-54, 100), Fix.Ratio(517, 100)), new(Fix.Ratio(-421, 100), Fix.Ratio(-306, 100)) };

    /// <summary>Where wing spot `slot` of base `b` is.</summary>
    public static FixVec2 WingSpot(Entity b, int slot)
    {
        var off = (b.Kind == Kind.Hq ? HqWingSpots : OutpostWingSpots)[slot];
        return b.Owner == 0 ? b.Pos + off : b.Pos - off;
    }

    /// <summary>The first free wing spot on a base that a wing fits in (on open ground, clear of other buildings than
    /// the base and its wings), or -1.</summary>
    public int FreeWingSlot(Entity b)
    {
        var r = StatsFor[(int)Kind.Factory].Radius;
        for (int slot = 0; slot < SlotsOf(b); slot++)
        {
            if (Entities.Exists(e => !e.Dead && e.Kind == Kind.Factory && e.BaseId == b.Id && e.Slot == slot)) continue;
            var at = WingSpot(b, slot);
            if (OffMap(at, r) || !Nav.OpenAt(at)) continue;
            bool clear = true;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (!Ground.Passable(at + new FixVec2(dx, dz) * r)) clear = false;
            foreach (var e in Entities)
            {
                if (e.Dead || !e.Stats.IsBuilding || e == b || (e.Kind == Kind.Factory && e.BaseId == b.Id)) continue;
                var gap = e.Stats.Radius + r + 1;
                if ((e.Pos - at).LengthSq < gap * gap) clear = false;
            }
            if (clear) return slot;
        }
        return -1;
    }

    /// <summary>Whether a wing making `makes` (a fighting unit of the player's civilisation, or Kind.MobileBase for the
    /// utility wing) can be built onto base `baseId` now.</summary>
    public bool CanAddWing(int player, int baseId, Kind makes, out string why)
    {
        why = null;
        var p = Players[player];
        var b = Find(baseId);
        if (makes != Kind.MobileBase && System.Array.IndexOf(FactoryKindsOf(p.Civ), makes) < 0) { why = "No wing makes that"; return false; }
        if (b == null || !IsBase(b) || b.Owner != player) { why = "Pick one of your bases"; return false; }
        if (b.Building > 0) { why = "The base is still setting up"; return false; }
        if (p.Metal < FactoryCost) { why = "Not enough metal"; return false; }
        if (SlotsUsed(b) >= SlotsOf(b)) { why = $"No free wing spot ({SlotsOf(b)} on this base)"; return false; }
        if (FreeWingSlot(b) < 0) { why = "No room for another wing here"; return false; }
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
        if (BaseNear(player, Deposits[deposit], BaseReach) == null) { why = $"Needs one of your bases within {BaseReach} (set up a mobile base near it)"; return false; }
        return true;
    }

    /// <summary>Metal a player makes each tick: the trickle and their extractors.</summary>
    public int IncomeOf(int player)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Extractor && e.Owner == player && e.Building == 0 && DepositResource[e.Deposit] == Resource.Metal) n++;   // (mining once built)
        return IncomePerTick + ExtractorIncome * n;
    }

    /// <summary>How many sulfur patches a player mines (built).</summary>
    public int SulfurOf(int player)
    {
        int n = 0;
        foreach (var e in Entities)
            if (!e.Dead && e.Kind == Kind.Extractor && e.Owner == player && e.Building == 0 && DepositResource[e.Deposit] == Resource.Sulfur) n++;
        return n;
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
        if (e.Fitting == Fitting.Napalm) d = d * NapalmHitPercent / 100;
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
        if (Winner >= 0) return;

        if (!Started)
        {
            // Setup: only placing (and upgrading) factories; nothing else moves until both sides have one.
            // (The user, 2026-10-19: the AI placing at once while a person is still choosing was a big edge.)
            foreach (var c in commands)
                if (c is AddWing or UpgradeFactory or CancelUpgrades or SetRally) Apply(c);
            Started = System.Array.TrueForAll(Players, p => Entities.Exists(e => e.Kind == Kind.Factory && e.Owner == p.Id));
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

        var income = new int[Players.Length];
        for (int i = 0; i < Players.Length; i++) income[i] = IncomeOf(i);
        for (int i = 0; i < Players.Length; i++) Players[i].Metal += income[i];
        BuyQueuedUpgrades();
        Construct();
        Produce();
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
        var best = Players[1 - player].HqPos;
        var bestD = (best - from).LengthSq;
        var me = Players[player];
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
                p.Metal -= FactoryCost;
                var onto = Find(aw.BaseId);
                int wingSlot = FreeWingSlot(onto);
                var wing = Spawn(Kind.Factory, aw.Player, WingSpot(onto, wingSlot), aw.Makes);
                wing.BaseId = onto.Id;
                wing.Slot = wingSlot;
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
        var army = new int[Players.Length];
        foreach (var e in Entities) if (!e.Dead && IsArmy(e.Kind)) army[e.Owner]++;
        int count = Entities.Count;   // units made now don't act until next tick
        for (int i = 0; i < count; i++)
        {
            var f = Entities[i];
            if (f.Dead || f.Kind != Kind.Factory || f.Building > 0) continue;   // (making nothing till built)
            if (IsUtility(f)) { WorkOrders(f); continue; }
            // At the army cap a factory stands idle, its next unit not started: losses take a full build to replace, so
            // winning a fight opens a window (finished units waiting at the door refilled the army at once: stalemate).
            if (army[f.Owner] >= MaxArmyOf(Players[f.Owner].Civ)) { f.Timer = BuildTicksOf(f); continue; }
            f.Timer--;
            if (f.Timer > 0) continue;
            f.Timer = BuildTicksOf(f);
            army[f.Owner]++;
            Spawn(f.Makes, f.Owner, DoorOf(f, f.Makes), from: f);
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
        var dir = Find(wing.BaseId) is { } b && b != wing ? wing.Pos - b.Pos : Players[1 - wing.Owner].HqPos - wing.Pos;
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
            if (s.Reload == 0)
            {
                // Unarmed (scouts): only go where they're sent, and stop there.
                if (!s.IsUnit || !e.HasWaypoint) continue;
                var to = e.Waypoint - e.Pos;
                if (to.LengthSq <= Fix.Ratio(1, 2) * Fix.Ratio(1, 2)) { e.HasWaypoint = false; continue; }
                _moves.Add((e, Drive(e, e.Waypoint, s.Speed)));
                continue;
            }

            var target = NearestEnemy(e, s.Aggro + MaxDownhillRange, s.MinRange);   // (a hill may let it reach past its aggro)
            // Artillery with an enemy too close to shoot holds still: that's its weakness.
            if (target == null && s.MinRange.Raw > 0 && NearestEnemy(e, s.Aggro + MaxDownhillRange, Fix.Zero) != null) continue;
            if (target != null)
            {
                var gap = (target.Pos - e.Pos).Length - s.Radius - target.Stats.Radius;
                var reach = s.Range + DownhillRange(e, target);   // firing down from a hill reaches further
                if (gap <= reach)
                {
                    if (e.Cooldown == 0) Fire(e, target);
                    continue;
                }
                if (s.IsUnit) _moves.Add((e, FanOut(e, Drive(e, target.Pos, Fix.Min(s.Speed, gap - reach), target.Stats.IsBuilding ? target.Stats.Radius : Fix.Zero))));
            }
            else if (s.IsUnit)
            {
                // No enemy in sight: first to its rally point if it was given one, then on to the nearest enemy its side
                // knows of (seen now, or where last seen; the enemy HQ at worst).
                // (The user, 2026-10-19: "the rallied units should move there first and then to the nearest known or
                // last seen enemy".) Close enough counts as there: a crowd can't all stand on the spot.
                if (e.HasWaypoint && (e.Waypoint - e.Pos).LengthSq <= WaypointReach * WaypointReach) e.HasWaypoint = false;
                var goal = e.HasWaypoint ? e.Waypoint : NearestKnown(e.Owner, e.Pos, s.HitsAir);
                _moves.Add((e, FanOut(e, Drive(e, goal, s.Speed))));
            }
        }
        foreach (var (e, step) in _moves) e.Pos = e.Stats.Flying ? e.Pos + step : Slide(e.Pos, step);   // (fliers go over water)
        foreach (var (target, damage, by) in _hits)
        {
            bool alive = target.Hp > 0;
            target.Hp -= damage;
            if (alive && target.Hp <= 0 && by != null) Credit(by);   // the shot that finished it
        }
        _hits.Clear();
    }

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
            if (e.Fitting == Fitting.Napalm) AddFire(target.Pos, Fix.Ratio(15, 10), e);
            return;
        }

        // A shell: it's lobbed at the ground where the target stands and lands a while later, hurting whoever is
        // there then. Something moving gets out from under it (the user, 2026-10-20: "artillery should be able to
        // miss, it attacks ground where a unit is but if it's moving it misses").
        int flight = ShellFlightTicks(e.Pos, target.Pos);
        Shots.Add(new Shot(e.Pos, target.Pos, e.Owner, e.Kind, e.Id, target.Id, flight));
        _shells.Add(new Shell { At = target.Pos, Owner = e.Owner, Damage = damage, Splash = s.Splash, By = e, TicksLeft = flight, Napalm = e.Fitting == Fitting.Napalm });
    }

    /// <summary>What a hit from `by` does to `to`: anti-armour weapons do more to armoured things (Stats.VsArmour).</summary>    static int Hurt(Entity by, Entity to, int damage) => to.Stats.Armoured ? damage * by.Stats.VsArmour / 100 : damage;
    /// <summary>What a hit from `by` does to `to`: anti-armour weapons do more to armoured things (Stats.VsArmour).</summary>
    static int Hurt(Entity by, Entity to, int damage) => to.Stats.Armoured ? damage * by.Stats.VsArmour / 100 : damage;

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
        if (sway.Raw == 0) return step;
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
                var push = b.Stats.IsBuilding ? min - len : (min - len) / 2;
                total += dir.WithLength(push);
            }
            if (total.X.Raw != 0 || total.Z.Raw != 0) _moves.Add((a, total));
        }
        foreach (var (e, step) in _moves) e.Pos = e.Stats.Flying ? e.Pos + step : Slide(e.Pos, step);
    }

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
            if (!e.Dead && e.Kind == Kind.Factory && (Find(e.BaseId) == null || Find(e.BaseId).Dead)) { e.Dead = true; e.Hp = 0; }
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
        Mix(Tick); Mix(Winner); Mix(PausedBy); Mix(PauseTicksLeft); Mix((long)Rng.State); Mix(Started ? 1 : 0);
        foreach (var p in Players)
        {
            Mix(p.Metal); Mix(p.PausesLeft);
            foreach (var (id, at) in p.Known) { Mix(id); Mix(at.X.Raw); Mix(at.Z.Raw); }
            Mix(p.BlipTick); foreach (var b in p.Blips) { Mix(b.X.Raw); Mix(b.Z.Raw); }
        }
        foreach (var e in Entities)
        {
            Mix(e.Id); Mix((int)e.Kind); Mix(e.Owner); Mix(e.Pos.X.Raw); Mix(e.Pos.Z.Raw);
            Mix(e.Hp); Mix(e.Cooldown); Mix(e.Timer); Mix((int)e.Makes); Mix(e.MaxHp); Mix(e.Damage); Mix(e.SeenBy); Mix(e.Kills); Mix(e.Rank); Mix(e.FactoryId); Mix(e.HasRally ? 1 : 0); Mix(e.Rally.X.Raw); Mix(e.Rally.Z.Raw); Mix(e.HasWaypoint ? 1 : 0); Mix(e.Waypoint.X.Raw); Mix(e.Waypoint.Z.Raw); Mix(e.BaseId); Mix(e.Deposit); Mix(e.Slot); Mix(e.Building); Mix((int)e.Fitting);
            Mix(e.Orders.Count); foreach (var o in e.Orders) Mix(o);
            foreach (var l in e.Levels) Mix(l);
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
