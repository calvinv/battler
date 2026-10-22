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
            if (!mine.Exists(e => e.Kind == Kind.Factory) && w.CanAddWing(_player, hq.Id, PickUnit(p.Civ), out _)) issue(new AddWing(_player, hq.Id, PickUnit(p.Civ)));
            return;
        }

        SendScouts(w, mine.FindAll(e => World.IsScout(e.Kind)), issue);
        DriveBases(w, mine, issue);

        // Mine whatever its bases reach: nearest home first; save for it if short.
        int best = -1;
        for (int d = 0; d < World.Deposits.Length; d++)
            if (w.ExtractorOn(d) == null && w.BaseNear(_player, World.Deposits[d], World.BaseReach) != null
                && (best < 0 || Dist(World.Deposits[d], p.HqPos) < Dist(World.Deposits[best], p.HqPos))) best = d;
        if (best >= 0)
        {
            if (w.CanPlaceExtractor(_player, best, out _)) issue(new PlaceExtractor(_player, best));
            return;
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

        // Fighting wings: as many as its mines can keep busy (three, and two a mine), on whichever base has room.
        int extractors = mine.FindAll(e => e.Kind == Kind.Extractor).Count;
        if (wings < Math.Min(MaxWings, 3 + 2 * extractors))
        {
            var onto = mine.Find(b => World.IsBase(b) && b.Building == 0 && w.SlotsUsed(b) < World.SlotsOf(b) && w.FreeWingSlot(b) >= 0);
            if (onto != null)
            {
                var makes = PickUnit(p.Civ);
                if (w.CanAddWing(_player, onto.Id, makes, out _)) issue(new AddWing(_player, onto.Id, makes));
                if (p.Metal < World.FactoryCost) return;   // saving for it
            }
        }
        MaybeUpgrade(w, mine, issue, p.Metal > 800 ? 1 : 2);   // (with metal piling up, every time)
    }

    Kind PickUnit(Civ civ)
    {
        var roll = _rng.Next(20);
        return civ == Civ.Lattice
            ? (roll < 8 ? Kind.Crawler : roll < 14 ? Kind.Spider : Kind.Piercer)            // crawlers 40%, spiders 30%, piercers 30%
            : (roll < 7 ? Kind.Tank : roll < 12 ? Kind.LightTank : roll < 16 ? Kind.Artillery : Kind.Mortar);   // tanks 35%, light 25%, artillery 20%, mortars 20%
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
            if (Dist(pos, World.Deposits[d]) < 26 && w.ExtractorOn(d) == null && w.BaseNear(_player, World.Deposits[d], World.BaseReach) == null) return true;
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

    /// <summary>One in `oneIn` thinks, buy a random upgrade on a fighting wing if it can.</summary>
    void MaybeUpgrade(World w, List<Entity> mine, Action<Command> issue, int oneIn)
    {
        if (_rng.Next(oneIn) != 0) return;
        var upgradable = mine.FindAll(e => e.Kind == Kind.Factory && !World.IsUtility(e));
        if (upgradable.Count == 0) return;
        for (int attempt = 0; attempt < 6; attempt++)   // a few random picks, in case the first is maxed or too dear
        {
            var f = upgradable[_rng.Next(upgradable.Count)];
            var which = (Upgrade)_rng.Next(3);
            if (w.CanUpgrade(_player, f.Id, which, out _)) { issue(new UpgradeFactory(_player, f.Id, which)); return; }
        }
    }

    static int Dist(FixVec2 a, FixVec2 b) => (a - b).Length.ToInt();
}
