using System.Collections.Generic;

namespace Battler.Sim;

public enum Kind { Hq, Factory, Scout, Tank, Artillery, Tower }   // (new kinds go at the end: StatsFor and the wire use the numbers)

/// <summary>A factory's upgrade tracks. They improve the units it makes from then on (not ones already out).</summary>
public enum Upgrade { Production, Damage, Armour }

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
    // A factory's upgrade levels by Upgrade; a unit carries its factory's at the time it was made.
    public readonly int[] Levels = new int[3];
    public int MaxHp;      // with armour upgrades
    public int Damage;     // with damage upgrades and veterancy
    public int Kills;      // what it has finished off
    public int Rank;       // veterancy, 0 to World.MaxRank: every World.KillsPerRank kills
    public int FactoryId;  // a unit: the factory that made it
    public bool HasWaypoint;   // a unit: its factory's rally point when it was made, to reach before attacking
    public FixVec2 Waypoint;
    public bool HasRally;  // a factory: whether its units gather at Rally instead of attacking
    public FixVec2 Rally;

    public int Level(Upgrade u) => Levels[(int)u];

    public Stats Stats => World.StatsFor[(int)Kind];
}

public sealed class Player
{
    public int Id;
    public int Metal;
    public int PausesLeft;
    public FixVec2 HqPos;
    /// <summary>Enemies this side has seen, by id, at where they were last seen (World.UpdateVision keeps it).</summary>
    public readonly SortedDictionary<int, FixVec2> Known = new();
}

/// <summary>A shot fired this tick, kept for drawing tracers.</summary>
public readonly record struct Shot(FixVec2 From, FixVec2 To, int Owner, Kind By);

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
    public const int IncomePerTick = 2;
    public const int FactoryCost = 500;   // every kind: they're balanced by how often they make a unit

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
    public static readonly Fix MapHalfX = 60, MapHalfZ = 40;

    public static readonly Kind[] UnitKinds = { Kind.Scout, Kind.Tank, Kind.Artillery };

    // Roles: the scout is fast and fragile; the tank is tough, the all-rounder; artillery outranges everything and
    // hits an area, but can't fire at anything close, so it's helpless once reached.
    public static readonly Stats[] StatsFor =
    {
        // Hq
        new() { MaxHp = 3000, Radius = 3, Range = 10, Aggro = 12, Vision = 14, Damage = 15, Reload = 10 },
        // Factory
        new() { MaxHp = 800, Radius = 2, Vision = 8 },
        // Scout
        new() { MaxHp = 60, Radius = Fix.Ratio(8, 10), Speed = Fix.Ratio(55, 100), Range = 6, Aggro = 18, Vision = 24,
                Damage = 5, Reload = 5, BuildTicks = 6 * TicksPerSecond },
        // Tank
        new() { MaxHp = 240, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(26, 100), Range = 8, Aggro = 14, Vision = 8,
                Damage = 24, Reload = 15, BuildTicks = 12 * TicksPerSecond },
        // Artillery
        new() { MaxHp = 90, Radius = Fix.Ratio(13, 10), Speed = Fix.Ratio(20, 100), Range = 22, MinRange = 7, Aggro = 22, Vision = 8,
                Splash = Fix.Ratio(25, 10), Damage = 40, Reload = 40, BuildTicks = 16 * TicksPerSecond },
        // Tower: two stand ahead of each HQ from the start, so a side isn't overrun before it has an army
        // (the user, 2026-10-19: "starting watch towers so it's hard to get overwhelmed initially").
        new() { MaxHp = 1200, Radius = Fix.Ratio(15, 10), Range = 14, Aggro = 14, Vision = 18, Damage = 20, Reload = 10 },
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

    public World(ulong seed)
    {
        Rng = new Rng(seed);
        Players = new Player[2];
        for (int i = 0; i < 2; i++)
        {
            var hq = HqStart(i);
            Players[i] = new Player { Id = i, Metal = StartMetal, PausesLeft = PausesPerPlayer, HqPos = hq };
            Spawn(Kind.Hq, i, hq);
            foreach (var side in new[] { -10, 10 }) Spawn(Kind.Tower, i, TowerStart(i, side));
        }
    }

    static Fix Clamp(Fix v, Fix limit) => Fix.Max(-limit, Fix.Min(limit, v));

    public static FixVec2 HqStart(int player) => new(player == 0 ? -45 : 45, 0);
    /// <summary>Where a starting watch tower stands: 12 toward the enemy from the HQ, `side` across.</summary>
    public static FixVec2 TowerStart(int player, int side) => new(player == 0 ? -33 : 33, side);

    /// <param name="from">a unit's factory: the unit takes its upgrade levels</param>
    public Entity Spawn(Kind kind, int owner, FixVec2 pos, Kind makes = Kind.Hq, Entity from = null)
    {
        var e = new Entity { Id = _nextId++, Kind = kind, Owner = owner, Pos = pos, Makes = makes };
        if (from != null)
        {
            from.Levels.CopyTo(e.Levels, 0);
            e.FactoryId = from.Id;
            e.HasWaypoint = from.HasRally;
            e.Waypoint = from.Rally;
        }
        SetStats(e);
        e.Hp = e.MaxHp;
        if (kind == Kind.Factory) e.Timer = BuildTicksOf(e);
        Entities.Add(e);
        return e;
    }

    /// <summary>Health and damage from the base stats, the upgrades it was made with, and its veterancy.</summary>
    static void SetStats(Entity e)
    {
        e.MaxHp = WithArmour(e.Stats.MaxHp, e.Level(Upgrade.Armour)) * (100 + 10 * e.Rank) / 100;
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
        var f = Find(factoryId);
        why = null;
        if (f == null || f.Kind != Kind.Factory || f.Owner != player) { why = "Not your factory"; return false; }
        if (f.Level(which) >= MaxUpgradeLevel) { why = "Fully upgraded"; return false; }
        if (Players[player].Metal < UpgradeCost(f.Level(which) + 1)) { why = "Not enough metal"; return false; }
        return true;
    }

    public bool CanPlaceFactory(int player, FixVec2 pos, Kind makes, out string why)
    {
        var p = Players[player];
        var r = StatsFor[(int)Kind.Factory].Radius;
        why = null;
        if (System.Array.IndexOf(UnitKinds, makes) < 0) { why = "Not a unit"; return false; }
        if (p.Metal < FactoryCost) { why = "Not enough metal"; return false; }
        if (Fix.Abs(pos.X) > MapHalfX - r || Fix.Abs(pos.Z) > MapHalfZ - r) { why = "Off the map"; return false; }
        if ((pos - p.HqPos).LengthSq > BuildRadius * BuildRadius) { why = "Too far from your HQ"; return false; }
        foreach (var e in Entities)
        {
            if (e.Dead || !e.Stats.IsBuilding) continue;
            var gap = e.Stats.Radius + r + 1;
            if ((e.Pos - pos).LengthSq < gap * gap) { why = "Blocked"; return false; }
        }
        return true;
    }

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
                if (c is PlaceFactory or UpgradeFactory or SetRally) Apply(c);
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

        foreach (var p in Players) p.Metal += IncomePerTick;
        Produce();
        Fight();
        Separate();
        Reap();
        UpdateVision();
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
                var reach = b.Stats.Vision + b.Stats.Radius + a.Stats.Radius;
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
                if (e.Owner != p.Id && !e.Dead && ((e.SeenBy & bit) != 0 || e.Kind == Kind.Hq)) p.Known[e.Id] = e.Pos;
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
                    var reach = mine.Stats.Vision + mine.Stats.Radius;
                    if ((at - mine.Pos).LengthSq <= reach * reach) { _forget.Add(id); break; }
                }
            }
            foreach (var id in _forget) p.Known.Remove(id);
        }
    }

    readonly List<int> _forget = new();

    /// <summary>Where the nearest enemy this side knows of is (or was last seen).</summary>
    FixVec2 NearestKnown(int player, FixVec2 from)
    {
        var best = Players[1 - player].HqPos;
        var bestD = (best - from).LengthSq;
        foreach (var (_, at) in Players[player].Known)
        {
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
            case PlaceFactory pf when CanPlaceFactory(pf.Player, pf.Pos, pf.Makes, out _):
                p.Metal -= FactoryCost;
                Spawn(Kind.Factory, pf.Player, pf.Pos, pf.Makes);
                break;
            case UpgradeFactory uf when CanUpgrade(uf.Player, uf.FactoryId, uf.Which, out _):
                var f = Find(uf.FactoryId);
                p.Metal -= UpgradeCost(f.Level(uf.Which) + 1);
                f.Levels[(int)uf.Which]++;
                break;
            case SetRally sr:
                var rallying = Find(sr.FactoryId);
                if (rallying == null || rallying.Kind != Kind.Factory || rallying.Owner != sr.Player) break;
                rallying.HasRally = !sr.Clear;
                rallying.Rally = new FixVec2(Clamp(sr.Pos.X, MapHalfX - 2), Clamp(sr.Pos.Z, MapHalfZ - 2));
                break;
        }
    }

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
        int count = Entities.Count;   // units made now don't act until next tick
        for (int i = 0; i < count; i++)
        {
            var f = Entities[i];
            if (f.Dead || f.Kind != Kind.Factory || --f.Timer > 0) continue;
            f.Timer = BuildTicksOf(f);
            // Roll out on the side facing the enemy, with a little spread so they don't stack.
            var dir = Players[1 - f.Owner].HqPos - f.Pos;
            var spawn = f.Pos + dir.WithLength(f.Stats.Radius + StatsFor[(int)f.Makes].Radius + 1)
                        + new FixVec2(0, Fix.Ratio(Rng.Range(-10, 10), 10));
            Spawn(f.Makes, f.Owner, spawn, from: f);
        }
    }

    /// <summary>
    /// Everyone decides from where everyone stood at the start of the tick, then all moves and all damage land
    /// together. Moving one by one let whoever came first in the list (player 0's things are made first) act on
    /// positions the others hadn't had a turn to change: player 0 won 37 of 60 AI matches even with the AIs swapped.
    /// </summary>
    void Fight()
    {
        _moves.Clear();
        foreach (var e in Entities)
        {
            if (e.Dead || e.Hp <= 0) continue;
            var s = e.Stats;
            if (e.Cooldown > 0) e.Cooldown--;
            if (s.Reload == 0) continue;

            var target = NearestEnemy(e, s.Aggro, s.MinRange);
            // Artillery with an enemy too close to shoot holds still: that's its weakness.
            if (target == null && s.MinRange.Raw > 0 && NearestEnemy(e, s.Aggro, Fix.Zero) != null) continue;
            if (target != null)
            {
                var gap = (target.Pos - e.Pos).Length - s.Radius - target.Stats.Radius;
                if (gap <= s.Range)
                {
                    if (e.Cooldown == 0) Fire(e, target);
                    continue;
                }
                if (s.IsUnit) _moves.Add((e, (target.Pos - e.Pos).WithLength(Fix.Min(s.Speed, gap - s.Range))));
            }
            else if (s.IsUnit)
            {
                // No enemy in sight: first to its rally point if it was given one, then on to the nearest enemy its side
                // knows of (seen now, or where last seen; the enemy HQ at worst).
                // (The user, 2026-10-19: "the rallied units should move there first and then to the nearest known or
                // last seen enemy".) Close enough counts as there: a crowd can't all stand on the spot.
                if (e.HasWaypoint && (e.Waypoint - e.Pos).LengthSq <= WaypointReach * WaypointReach) e.HasWaypoint = false;
                var goal = e.HasWaypoint ? e.Waypoint : NearestKnown(e.Owner, e.Pos);
                var away = goal - e.Pos;
                _moves.Add((e, away.WithLength(Fix.Min(s.Speed, away.Length))));
            }
        }
        foreach (var (e, step) in _moves) e.Pos += step;
        foreach (var (target, damage, by) in _hits)
        {
            bool alive = target.Hp > 0;
            target.Hp -= damage;
            if (alive && target.Hp <= 0) Credit(by);   // the shot that finished it
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
        Shots.Add(new Shot(e.Pos, target.Pos, e.Owner, e.Kind));
        if (s.Splash.Raw == 0) { _hits.Add((target, e.Damage, e)); return; }
        foreach (var o in Entities)
        {
            if (o.Dead || o.Owner == e.Owner) continue;
            var reach = s.Splash + o.Stats.Radius;
            if ((o.Pos - target.Pos).LengthSq <= reach * reach) _hits.Add((o, e.Damage, e));
        }
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
                if (b.Dead) continue;
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
        foreach (var (e, step) in _moves) e.Pos += step;
    }

    void Reap()
    {
        foreach (var e in Entities)
        {
            if (e.Dead || e.Hp > 0) continue;
            e.Dead = true;
            if (e.Kind == Kind.Hq) Winner = 1 - e.Owner;
        }
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
        }
        foreach (var e in Entities)
        {
            Mix(e.Id); Mix((int)e.Kind); Mix(e.Owner); Mix(e.Pos.X.Raw); Mix(e.Pos.Z.Raw);
            Mix(e.Hp); Mix(e.Cooldown); Mix(e.Timer); Mix((int)e.Makes); Mix(e.MaxHp); Mix(e.Damage); Mix(e.SeenBy); Mix(e.Kills); Mix(e.Rank); Mix(e.FactoryId); Mix(e.HasRally ? 1 : 0); Mix(e.Rally.X.Raw); Mix(e.Rally.Z.Raw); Mix(e.HasWaypoint ? 1 : 0); Mix(e.Waypoint.X.Raw); Mix(e.Waypoint.Z.Raw);
            foreach (var l in e.Levels) Mix(l);
        }
        return h;
    }
}
