using System;
using System.Collections.Generic;

namespace Battler.Sim;

/// <summary>
/// A computer opponent. It sits outside the simulation and only issues commands, exactly like a
/// human player, so it needs no special handling in a networked match.
///
/// What it does, most urgent first: mines every deposit its bases reach; builds a utility wing, keeps a scout out,
/// and orders mobile bases (up to three bases); drives each new mobile base to the nearest deposit no base covers and
/// sets it up there; fills wing spots with fighting wings, as many as its mines can keep busy; fits napalm once it has
/// sulfur; and spends what's left on upgrades.
/// </summary>
public sealed class SimpleAi
{
    readonly int _player;
    readonly Rng _rng;
    const int WantScouts = 1, WantBases = World.MaxBases, MaxWings = 10;

    public SimpleAi(int player, ulong seed)
    {
        _player = player;
        _rng = new Rng(seed);
    }

    /// <summary>Call once per tick, before the world steps.</summary>
    public void Think(World w, Action<Command> issue)
    {
        if (w.Paused || w.Tick % 20 != 0) return;
        var p = w.Players[_player];
        var mine = w.Entities.FindAll(e => e.Owner == _player && !e.Dead);
        var hq = mine.Find(e => e.Kind == Kind.Hq);
        if (hq == null) return;

        if (!w.Started)   // setup: one fighting wing on the HQ
        {
            var first = PickUnit(p.Civ);
            if (!mine.Exists(e => e.Kind == Kind.Factory) && w.CanAddWing(_player, hq.Id, first, out _)) issue(new AddWing(_player, hq.Id, first));
            return;
        }

        SendScouts(w, mine.FindAll(e => World.IsScout(e.Kind)), issue);
        DriveBases(w, mine, issue);

        // Mine what's near its bases: nearest home first. A line has to reach it: grow one out to it with blanks
        // (giving up on one it can't get a line to); save for it if short.
        int best = -1;
        for (int d = 0; d < World.Deposits.Length; d++)
            if (w.ExtractorOn(d) == null && !_unreachable.Contains(d) && w.BaseNear(_player, World.Deposits[d], Fix.FromInt(Reach)) != null
                && (best < 0 || Dist(World.Deposits[d], p.HqPos) < Dist(World.Deposits[best], p.HqPos))) best = d;
        if (best >= 0)
        {
            if (w.CanPlaceExtractor(_player, best, out _)) issue(new PlaceExtractor(_player, best));
            else if (w.LineReaches(_player, World.Deposits[best], built: false)) { }   // (a line's last piece is going up there: wait for it)
            else if (!w.LineReaches(_player, World.Deposits[best]) && !GrowToward(w, mine, World.Deposits[best], issue) && p.Metal >= World.ModuleCost(Mod.Blank)) _unreachable.Add(best);
            return;
        }

        // Power: a plant on a free vent in reach whenever the wings draw (nearly) all there is.
        if (w.PowerDrawOf(_player) >= w.PowerSupply(_player) - 1)
            for (int v = 0; v < World.Vents.Length; v++)
                if (w.PlantOn(v) == null && !_unreachableVents.Contains(v) && w.BaseNear(_player, World.Vents[v], Fix.FromInt(Reach)) != null)
                {
                    if (w.CanPlacePlant(_player, v, out _)) issue(new PlacePlant(_player, v));
                    else if (w.LineReaches(_player, World.Vents[v], built: false)) { }
                    else if (!w.LineReaches(_player, World.Vents[v]) && !GrowToward(w, mine, World.Vents[v], issue) && p.Metal >= World.ModuleCost(Mod.Blank)) { _unreachableVents.Add(v); continue; }
                    return;   // (or saving for it)
                }

        // A utility wing, then a scout, then mobile bases.
        var util = mine.Find(World.IsUtility);
        int wings = mine.FindAll(e => e.Kind == Kind.Factory && !World.IsUtility(e)).Count;
        if (util == null)
        {
            if (wings >= 1)
            {
                if (w.CanAddWing(_player, hq.Id, Kind.MobileBase, out _)) issue(new AddWing(_player, hq.Id, Kind.MobileBase));
                return;
            }
        }
        else if (util.Building == 0)
        {
            if (w.ScoutCount(_player) < WantScouts && w.CanOrder(_player, util.Id, World.ScoutKindOf(p.Civ), out _)) { issue(new Order(_player, util.Id, World.ScoutKindOf(p.Civ))); return; }
            if (w.BaseCount(_player) < WantBases && wings >= 2)
            {
                if (w.CanOrder(_player, util.Id, Kind.MobileBase, out _)) issue(new Order(_player, util.Id, Kind.MobileBase));
                return;   // (saving for it)
            }
        }

        // Napalm once there's sulfur: on about half the wings that can take it.
        if (w.SulfurOf(_player) > 0)
            foreach (var f in mine)
                if (f.Kind == Kind.Factory && World.CanFitNapalm(f.Makes) && f.Fitting == Fitting.Standard && f.Id % 2 == 0)
                { issue(new SetFitting(_player, f.Id, Fitting.Napalm)); return; }

        // Defences: a turret or two at each base, more as the waves grow; a flak once there are fliers about.
        if (wings >= 2 && Defend(w, mine, issue)) return;

        // Fighting wings: as many as its mines can keep busy (three, and two a mine), on whichever base has room.
        int extractors = mine.FindAll(e => e.Kind == Kind.Extractor).Count;
        if (wings < Math.Min(MaxWings, 3 + 2 * extractors))
        {
            // (sulfur hosts first: their units come out enhanced; then power plants, which halve the draw)
            Entity onto = null;
            foreach (var b in mine)
            {
                if (!World.IsWingHost(b) || b.Building > 0 || w.FreeWingSlot(b) < 0) continue;
                int rank = b.Kind == Kind.Extractor && World.DepositResource[b.Deposit] == Resource.Sulfur ? 0 : b.Kind == Kind.PowerPlant ? 1 : 2;
                int ontoRank = onto == null ? 9 : onto.Kind == Kind.Extractor && World.DepositResource[onto.Deposit] == Resource.Sulfur ? 0 : onto.Kind == Kind.PowerPlant ? 1 : 2;
                if (rank < ontoRank) onto = b;
            }
            if (onto != null)
            {
                var makes = PickUnit(p.Civ);
                if (w.CanAddWing(_player, onto.Id, makes, out _)) issue(new AddWing(_player, onto.Id, makes));
                if (p.Metal < World.WingCost(makes)) return;   // saving for it
            }
        }
        MaybeUpgrade(w, mine, issue, p.Metal > 800 ? 1 : 2);   // (with metal piling up, every time)
    }

    const int Reach = 45;   // how far from a base it will run lines out to a resource
    readonly HashSet<int> _unreachable = new(), _unreachableVents = new();

    /// <summary>A blank on whichever of its lines ends nearest `goal`, a step toward it. False if no line can.</summary>
    bool GrowToward(World w, List<Entity> mine, FixVec2 goal, Action<Command> issue)
    {
        Entity bestHost = null;
        int bestSlot = -1, bestDist = int.MaxValue;
        var bestAim = goal;
        foreach (var b in mine)
        {
            if (!World.IsWingHost(b) || b.Building > 0) continue;
            for (int slot = 0; slot < World.SlotsOf(b); slot++)
            {
                // Straight at it, or (if something's in the way) aimed off to one side or the other to go round.
                var from = w.LineEnd(b, slot)?.Pos ?? b.Pos;
                var at = goal - from;
                var side = at.LengthSq.Raw == 0 ? FixVec2.Zero : new FixVec2(-at.Z, at.X).WithLength(Fix.FromInt(10));
                for (int k = 0; k < 3; k++)
                {
                    var aim = k == 0 ? goal : k == 1 ? goal + side : goal - side;
                    if (!w.CanAddPiece(_player, b.Id, slot, Kind.Hq, Mod.Blank, aim, out _)) continue;
                    int d = Dist(w.NextSpot(b, slot, aim, out _), goal);
                    if (d >= Dist(from, goal) + (k == 0 ? 0 : 3)) continue;   // (it has to get nearer, or nearly, going round)
                    if (d < bestDist) { bestDist = d; bestHost = b; bestSlot = slot; bestAim = aim; }
                }
            }
        }
        if (bestHost == null) return false;
        issue(new AddModule(_player, bestHost.Id, bestSlot, Mod.Blank, bestAim));
        return true;
    }

    bool Defend(World w, List<Entity> mine, Action<Command> issue)
    {
        int want = Math.Min(3, 1 + w.WaveNumber / 3);
        bool fliers = w.Entities.Exists(e => e.Owner != _player && e.Stats.Flying && !e.Dead);
        foreach (var b in mine)
        {
            if (!World.IsBase(b) || b.Building > 0) continue;
            int turrets = 0, flak = 0;
            foreach (var d in mine)
                if ((d.Pos - b.Pos).LengthSq < Fix.FromInt(30 * 30)) { if (d.Kind == Kind.Turret) turrets++; if (d.Kind == Kind.Flak) flak++; }
            var kind = turrets < want ? Kind.Turret : fliers && flak == 0 ? Kind.Flak : (Kind?)null;
            if (kind == null) continue;
            if (w.Players[_player].Metal < World.CostOf(kind.Value)) return true;   // saving for it
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var spot = b.Pos + new FixVec2(_rng.Range(-16, 16), _rng.Range(-16, 16));
                if ((spot - b.Pos).LengthSq < Fix.FromInt(10 * 10)) continue;   // (clear of its wings)
                if (w.CanPlaceDefence(_player, spot, kind.Value, out _)) { issue(new PlaceDefence(_player, spot, kind.Value)); return true; }
            }
        }
        return false;
    }

    Kind PickUnit(Civ civ)
    {
        var roll = _rng.Next(20);
        if (roll == 16 && civ != Civ.Lattice) return Kind.Mender;   // 5%
        if (roll >= 17) return roll == 17 ? Kind.Ranger : roll == 18 ? Kind.Flier : Kind.Bomber;   // 5% each
        return civ == Civ.Lattice
            ? (roll < 7 ? Kind.Crawler : roll < 12 ? Kind.Spider : Kind.Piercer)
            : (roll < 6 ? Kind.Tank : roll < 10 ? Kind.LightTank : roll < 14 ? Kind.Artillery : Kind.Mortar);
    }

    /// <summary>Mobile bases on the move: each heads for the nearest deposit no base covers (and no other is heading
    /// for), stopping short of it on the way from home; once there it sets up, or edges about until it can.</summary>
    void DriveBases(World w, List<Entity> mine, Action<Command> issue)
    {
        var trucks = mine.FindAll(e => e.Kind == Kind.MobileBase);
        var claimed = new List<FixVec2>();
        foreach (var t in trucks) if (t.HasWaypoint) claimed.Add(t.Waypoint);
        var hq = w.Players[_player].HqPos;
        foreach (var t in trucks)
        {
            if (t.HasWaypoint) continue;   // (still driving)
            if (w.CanDeploy(_player, t.Id, out _) && Near(w, t.Pos))
            {
                issue(new Deploy(_player, t.Id));
                continue;
            }
            int target = -1;
            for (int d = 0; d < World.Deposits.Length; d++)
            {
                var at = World.Deposits[d];
                if (w.ExtractorOn(d) != null || w.BaseNear(_player, at, World.BaseReach) != null) continue;
                if (w.GuardNear(at)) continue;   // (cleared ones only)
                if (claimed.Exists(c => Dist(c, at) < 30)) continue;
                if (target < 0 || Dist(at, t.Pos) < Dist(World.Deposits[target], t.Pos)) target = d;
            }
            if (target < 0) continue;
            var deposit = World.Deposits[target];
            // Already beside it but can't set up: edge about a little. Otherwise drive to a spot short of it.
            bool there = Dist(t.Pos, deposit) < 22;
            var spot = there
                ? t.Pos + new FixVec2(_rng.Range(-8, 8), _rng.Range(-8, 8))
                : deposit + (hq - deposit).WithLength(Fix.FromInt(14));
            claimed.Add(deposit);
            issue(new MoveUnits(_player, new[] { t.Id }, spot));
        }
    }

    /// <summary>Whether a spot is near a deposit no base covers yet (worth setting up at).</summary>
    bool Near(World w, FixVec2 pos)
    {
        for (int d = 0; d < World.Deposits.Length; d++)
            if (Dist(pos, World.Deposits[d]) < 26 && w.ExtractorOn(d) == null && w.BaseNear(_player, World.Deposits[d], World.BaseReach) == null && !w.GuardNear(World.Deposits[d])) return true;
        return false;
    }

    /// <summary>Idle scouts go to the nearest deposit nobody mines and no other scout of ours is heading for.</summary>
    void SendScouts(World w, List<Entity> scouts, Action<Command> issue)
    {
        var claimed = new List<FixVec2>();
        foreach (var s in scouts) claimed.Add(s.HasWaypoint ? s.Waypoint : s.Pos);
        foreach (var s in scouts)
        {
            if (s.HasWaypoint) continue;
            int here = NearestDeposit(s.Pos, 6);
            if (here >= 0 && w.ExtractorOn(here) == null) continue;   // (watching a free deposit)
            int best = -1;
            for (int d = 0; d < World.Deposits.Length; d++)
            {
                var at = World.Deposits[d];
                if (w.ExtractorOn(d) != null) continue;
                bool taken = false;
                foreach (var c in claimed) if (Dist(c, at) < 6) taken = true;
                if (taken) continue;
                if (best < 0 || Dist(at, s.Pos) < Dist(World.Deposits[best], s.Pos)) best = d;
            }
            if (best < 0) continue;
            var spot = World.Deposits[best] + new FixVec2(0, World.Deposits[best].Z.Raw >= 0 ? -4 : 4);   // beside it, not on it
            claimed.Add(spot);
            issue(new MoveUnits(_player, new[] { s.Id }, spot));
        }
    }

    static int NearestDeposit(FixVec2 pos, int within)
    {
        for (int d = 0; d < World.Deposits.Length; d++)
            if (Dist(World.Deposits[d], pos) <= within) return d;
        return -1;
    }

    /// <summary>One in `oneIn` thinks, snap a module onto one of its lines if it can (mostly attack, armour, speed and
    /// build speed; now and then a wildcard), so the wings it adds later come out stronger.</summary>
    void MaybeUpgrade(World w, List<Entity> mine, Action<Command> issue, int oneIn)
    {
        if (_rng.Next(oneIn) != 0) return;
        var hosts = mine.FindAll(e => World.IsWingHost(e) && e.Building == 0);
        if (hosts.Count == 0) return;
        for (int attempt = 0; attempt < 6; attempt++)   // a few random picks, in case the first is full or too dear
        {
            var b = hosts[_rng.Next(hosts.Count)];
            int slot = _rng.Next(World.SlotsOf(b));
            var mod = _rng.Next(5) == 0 ? (Mod)(5 + _rng.Next(4)) : new[] { Mod.Attack, Mod.Armour, Mod.Speed, Mod.Build }[_rng.Next(4)];
            if (w.CanAddPiece(_player, b.Id, slot, Kind.Hq, mod, out _)) { issue(new AddModule(_player, b.Id, slot, mod)); return; }
        }
    }

    static int Dist(FixVec2 a, FixVec2 b) => (a - b).Length.ToInt();
}
