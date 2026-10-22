using System;
using System.Collections.Generic;

namespace Battler.Sim;

/// <summary>
/// A computer opponent. It sits outside the simulation and only issues commands, exactly like a
/// human player, so it needs no special handling in a networked match.
///
/// What it does, most urgent first: mines every deposit its network reaches; keeps a scout out; fills free factory
/// spots on its network; grows the network, node by node, toward the nearest deposit nobody mines (sulfur included);
/// with no free spot, adds a node at home for more factories; fits napalm once it has sulfur; and spends what's left
/// on upgrades.
/// </summary>
public sealed class SimpleAi
{
    readonly int _player;
    readonly Rng _rng;
    const int WantScouts = 1, MaxNodesGoingUp = 2, MaxFactories = 10;

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
        var scouts = mine.FindAll(e => World.IsScout(e.Kind));

        if (w.Started)
        {
            SendScouts(w, scouts, issue);

            // Mine whatever the network reaches: nearest home first.
            int best = -1;
            for (int d = 0; d < World.Deposits.Length; d++)
                if (w.CanPlaceExtractor(_player, d, out _) && (best < 0 || Dist(World.Deposits[d], p.HqPos) < Dist(World.Deposits[best], p.HqPos))) best = d;
            if (best >= 0) { issue(new PlaceExtractor(_player, best)); return; }
            // The network already reaches a free deposit: save for its extractor (nodes are cheap, and would eat it all).
            for (int d = 0; d < World.Deposits.Length; d++)
                if (w.ExtractorOn(d) == null && w.NetworkNear(_player, World.Deposits[d], World.ExtractorReach, false) != null && p.Metal < World.ExtractorCost) return;

            if (w.ScoutCount(_player) < WantScouts && w.CanQueueScout(_player, out _)) { issue(new QueueScout(_player)); return; }

            // Napalm once there's sulfur: on about half the factories that can take it.
            if (w.SulfurOf(_player) > 0)
                foreach (var f in mine)
                    if (f.Kind == Kind.Factory && World.CanFitNapalm(f.Makes) && f.Fitting == Fitting.Standard && f.Id % 2 == 0)
                    { issue(new SetFitting(_player, f.Id, Fitting.Napalm)); return; }
        }

        // A free factory spot: fill it, up to as many factories as its mines can keep busy (three, and two a mine).
        int factories = mine.FindAll(e => e.Kind == Kind.Factory).Count;
        int extractors = mine.FindAll(e => e.Kind == Kind.Extractor).Count;
        bool wantFactory = factories < Math.Min(MaxFactories, 3 + 2 * extractors);
        var freeBase = mine.Find(b => World.IsBase(b) && b.Building == 0 && b.Powered && w.SlotsUsed(b) < World.SlotsOf(b));
        if (freeBase != null && wantFactory)
        {
            if (p.Metal < World.FactoryCost) return;   // saving for it
            var roll = _rng.Next(20);
            var makes = p.Civ == Civ.Lattice
                ? (roll < 8 ? Kind.Crawler : roll < 14 ? Kind.Spider : Kind.Piercer)            // crawlers 40%, spiders 30%, piercers 30%
                : (roll < 7 ? Kind.Tank : roll < 12 ? Kind.LightTank : roll < 16 ? Kind.Artillery : Kind.Mortar);   // tanks 35%, light 25%, artillery 20%, mortars 20%
            var r = World.BaseRadius(freeBase);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var pos = freeBase.Pos + new FixVec2(_rng.Range(-r.ToInt(), r.ToInt()), _rng.Range(-r.ToInt(), r.ToInt()));
                if (w.CanPlaceFactory(_player, pos, makes, out _)) { issue(new PlaceFactory(_player, pos, makes)); return; }
            }
        }
        if (!w.Started) return;

        // Grow the network: toward the nearest deposit nobody mines, and (with no free spot) a node at home for factories.
        int goingUp = mine.FindAll(e => e.Kind == Kind.Node && e.Building > 0).Count;
        if (goingUp < MaxNodesGoingUp && p.Metal >= World.NodeCost)
        {
            if (TowardDeposit(w, mine, issue)) return;
            if (freeBase == null && wantFactory && HomeNode(w, mine, issue)) return;
        }
        MaybeUpgrade(w, mine, issue, p.Metal > 800 ? 1 : 2);   // (with metal piling up, every time)
    }

    /// <summary>A node a step along the way from the nearest piece of network to the nearest deposit nobody mines.</summary>
    bool TowardDeposit(World w, List<Entity> mine, Action<Command> issue)
    {
        var hq = w.Players[_player].HqPos;
        int target = -1;
        for (int d = 0; d < World.Deposits.Length; d++)
        {
            if (w.ExtractorOn(d) != null || w.NetworkNear(_player, World.Deposits[d], World.ExtractorReach, false) != null) continue;
            if (target < 0 || Dist(World.Deposits[d], hq) < Dist(World.Deposits[target], hq)) target = d;
        }
        if (target < 0) return false;
        var goal = World.Deposits[target];
        Entity from = null;
        foreach (var e in mine)
            if (e.Kind is Kind.Hq or Kind.Node && (from == null || Dist(e.Pos, goal) < Dist(from.Pos, goal))) from = e;
        if (from == null) return false;
        var path = w.PathBetween(from.Pos, goal);
        if (path == null) return false;
        foreach (var step in new[] { 20, 17, 14, 11, 9 })
        {
            var at = Along(from.Pos, path, step);
            if ((at - goal).LengthSq < Fix.FromInt(36)) at = goal + (from.Pos - goal).WithLength(Fix.FromInt(6));   // (beside the deposit, not on it)
            foreach (var (dx, dz) in new[] { (0, 0), (3, 0), (-3, 0), (0, 3), (0, -3) })
            {
                var spot = at + new FixVec2(dx, dz);
                if (w.CanPlaceNode(_player, spot, out _)) { issue(new PlaceNode(_player, spot)); return true; }
            }
        }
        return false;
    }

    /// <summary>A node beside one near home (within 50 of the HQ), for more factory spots.</summary>
    bool HomeNode(World w, List<Entity> mine, Action<Command> issue)
    {
        var hq = w.Players[_player].HqPos;
        var near = mine.FindAll(e => e.Kind is Kind.Hq or Kind.Node && e.Building == 0 && Dist(e.Pos, hq) < 50);
        if (near.Count == 0) return false;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var b = near[_rng.Next(near.Count)];
            var spot = b.Pos + new FixVec2(_rng.Range(-18, 18), _rng.Range(-18, 18));
            if (w.CanPlaceNode(_player, spot, out _)) { issue(new PlaceNode(_player, spot)); return true; }
        }
        return false;
    }

    /// <summary>The point `dist` along a path of corners starting at `start` (its end if shorter).</summary>
    static FixVec2 Along(FixVec2 start, List<FixVec2> path, int dist)
    {
        var at = start;
        var left = Fix.FromInt(dist);
        foreach (var c in path)
        {
            var seg = (c - at).Length;
            if (seg >= left) return at + (c - at).WithLength(left);
            left -= seg;
            at = c;
        }
        return at;
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

    /// <summary>One in `oneIn` thinks, buy a random upgrade on a factory if it can.</summary>
    void MaybeUpgrade(World w, List<Entity> mine, Action<Command> issue, int oneIn)
    {
        if (_rng.Next(oneIn) != 0) return;
        var upgradable = mine.FindAll(e => e.Kind == Kind.Factory);
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
