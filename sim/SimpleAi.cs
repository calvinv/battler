using System;
using System.Collections.Generic;

namespace Battler.Sim;

/// <summary>
/// A computer opponent. It sits outside the simulation and only issues commands, exactly like a
/// human player, so it needs no special handling in a networked match.
///
/// What it does, most urgent first: keeps a couple of scouts; mines every deposit it can reach; sends idle scouts to
/// the nearest unmined deposit; fills its free factory slots; with every slot full, sets up an outpost beside a scout
/// out in the field; and spends what's left on upgrades.
/// </summary>
public sealed class SimpleAi
{
    readonly int _player;
    readonly Rng _rng;
    const int WantScouts = 2, MaxOutposts = 3;

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
        var scouts = mine.FindAll(e => e.Kind == Kind.Scout);
        var outposts = mine.FindAll(e => e.Kind == Kind.Outpost);

        if (w.Started)
        {
            SendScouts(w, scouts, issue);

            // Mine whatever it can reach: nearest home first.
            int best = -1;
            for (int d = 0; d < World.Deposits.Length; d++)
                if (w.CanPlaceExtractor(_player, d, out _) && (best < 0 || Dist(World.Deposits[d], p.HqPos) < Dist(World.Deposits[best], p.HqPos))) best = d;
            if (best >= 0) { issue(new PlaceExtractor(_player, best)); return; }

            if (w.ScoutCount(_player) < WantScouts)
            {
                if (w.CanQueueScout(_player, out _)) { issue(new QueueScout(_player)); return; }
                if (p.Metal < World.ScoutCost) return;   // saving for it
            }
        }

        // A free factory slot: fill it (mostly tanks).
        var freeBase = mine.Find(b => World.IsBase(b) && w.SlotsUsed(b) < World.SlotsOf(b));
        if (freeBase != null)
        {
            if (p.Metal < World.FactoryCost) return;   // saving for it
            var makes = _rng.Next(3) < 2 ? Kind.Tank : Kind.Artillery;
            var r = World.BaseRadius(freeBase);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var pos = freeBase.Pos + new FixVec2(_rng.Range(-r.ToInt(), r.ToInt()), _rng.Range(-r.ToInt(), r.ToInt()));
                if (w.CanPlaceFactory(_player, pos, makes, out _)) { issue(new PlaceFactory(_player, pos, makes)); return; }
            }
        }
        if (!w.Started) return;

        // Every slot full: a new base beside a scout that's out by a deposit (or, half the time, upgrade instead).
        if (freeBase == null && outposts.Count < MaxOutposts && _rng.Next(2) == 0)
        {
            if (p.Metal < World.OutpostCost) return;   // saving for it
            foreach (var s in scouts)
            {
                if (s.HasWaypoint || Dist(s.Pos, p.HqPos) < 30) continue;   // not one still on its way, nor one at home
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var pos = s.Pos + new FixVec2(_rng.Range(-7, 7), _rng.Range(-7, 7));
                    if (w.CanPlaceOutpost(_player, pos, out _)) { issue(new PlaceOutpost(_player, pos)); return; }
                }
            }
        }
        MaybeUpgrade(w, mine, issue, p.Metal > 800 ? 1 : 2);   // (with metal piling up, every time)
    }

    /// <summary>Idle scouts go to the nearest deposit nobody mines and no other scout of ours is heading for.</summary>
    void SendScouts(World w, List<Entity> scouts, Action<Command> issue)
    {
        var claimed = new List<FixVec2>();
        foreach (var s in scouts) claimed.Add(s.HasWaypoint ? s.Waypoint : s.Pos);
        foreach (var s in scouts)
        {
            if (s.HasWaypoint) continue;
            // Staying put by a free deposit until it's mined (or an outpost goes up beside it).
            int here = NearestDeposit(s.Pos, 6);
            if (here >= 0 && w.ExtractorOn(here) == null) continue;
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

    /// <summary>One in `oneIn` thinks, buy a random upgrade on a factory or outpost if it can.</summary>
    void MaybeUpgrade(World w, List<Entity> mine, Action<Command> issue, int oneIn)
    {
        if (_rng.Next(oneIn) != 0) return;
        var upgradable = mine.FindAll(e => e.Kind is Kind.Factory or Kind.Outpost);
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
