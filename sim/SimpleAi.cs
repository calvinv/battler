using System;
using System.Collections.Generic;

namespace Battler.Sim;

/// <summary>
/// A computer opponent. It sits outside the simulation and only issues commands, exactly like a
/// human player, so it needs no special handling in a networked match.
///
/// What it does: keeps its HQ's queue topped up with a mix of what it has unlocked (Repeat keeps the stream going);
/// works down a research plan, a step or two ahead; keeps a scout out; and puts a turret or two by its HQ, and a flak
/// once there are fliers about.
/// </summary>
public sealed class SimpleAi
{
    readonly int _player;
    readonly Rng _rng;
    readonly List<Tech> _plan;
    const int WantQueued = 4, WantScouts = 1;

    public SimpleAi(int player, ulong seed)
    {
        _player = player;
        _rng = new Rng(seed);
        // Doors and the main units first, then upgrades and the rest, a little shuffled so two AIs differ.
        _plan = new List<Tech> { Tech.Wheel, Tech.Door2, Tech.Lobber, Tech.Plating1, Tech.Gunnery1, Tech.Tooling1, Tech.Mender, Tech.Dart,
                                 Tech.LongLeg, Tech.Door3, Tech.Servos1, Tech.Bulwark, Tech.Blimp, Tech.Plating2, Tech.Gunnery2, Tech.Hive,
                                 Tech.Tooling2, Tech.ShieldCoil, Tech.Napalm, Tech.Door4, Tech.Servos2, Tech.Twin, Tech.Magnet };
        for (int i = 2; i < _plan.Count - 1; i++)
            if (_rng.Next(3) == 0) (_plan[i], _plan[i + 1]) = (_plan[i + 1], _plan[i]);
    }

    /// <summary>Call once per tick, before the world steps.</summary>
    public void Think(World w, Action<Command> issue)
    {
        if (w.Paused || w.Tick % 20 != 0) return;
        var p = w.Players[_player];
        var hq = w.HqOf(_player);
        if (hq == null) return;

        // Units: keep a few queued, picked from what it has.
        if (p.Queue.Count < WantQueued)
        {
            var k = PickUnit(w);
            if (w.CanQueue(_player, k, out _)) issue(new QueueUnit(_player, k));
        }
        if (!w.Started) return;

        if (w.ScoutCount(_player) < WantScouts && w.CanQueue(_player, Kind.Scout, out _)) issue(new QueueUnit(_player, Kind.Scout));
        SendScouts(w, issue);

        // Research: the next of the plan, one queued ahead at most, once the queue has been fed.
        if (p.Research.Count < 2 && p.Metal > 150)
            foreach (var t in _plan)
                if (!p.Has[(int)t] && !p.Research.Contains((int)t))
                {
                    if (w.CanResearch(_player, t, out _)) issue(new Research(_player, t));
                    break;
                }

        // Defences once it's under way.
        if (w.Tick - w.StartTick > 90 * World.TicksPerSecond) Defend(w, hq, issue);
    }

    /// <summary>What to build next: mostly the main line, some support, from what it has unlocked.</summary>
    Kind PickUnit(World w)
    {
        for (int tries = 0; tries < 8; tries++)
        {
            int roll = _rng.Next(20);
            var k = roll switch
            {
                < 5 => Kind.LightTank,
                < 9 => Kind.Tank,
                < 11 => Kind.Ranger,
                < 13 => Kind.Mortar,
                < 15 => Kind.Artillery,
                15 => Kind.Mender,
                16 => Kind.Bulwark,
                17 => Kind.Flier,
                18 => Kind.Bomber,
                _ => Kind.Hive,
            };
            if (w.Unlocked(_player, k)) return k;
        }
        return _rng.Next(3) == 0 ? Kind.Ranger : Kind.LightTank;
    }

    void Defend(World w, Entity hq, Action<Command> issue)
    {
        var mine = w.Entities.FindAll(e => e.Owner == _player && !e.Dead);
        int turrets = mine.FindAll(e => e.Kind == Kind.Turret).Count, flak = mine.FindAll(e => e.Kind == Kind.Flak).Count;
        bool fliers = w.Entities.Exists(e => e.Owner != _player && e.Stats.Flying && !e.Dead);
        int want = 1 + (w.Tick - w.StartTick) / (240 * World.TicksPerSecond);
        var kind = turrets < Math.Min(3, want) ? Kind.Turret : fliers && flak == 0 ? Kind.Flak : (Kind?)null;
        if (kind == null || w.Players[_player].Metal < World.CostOf(kind.Value) + 200) return;   // (units first)
        var toward = (w.Players[1 - _player].HqPos - hq.Pos).WithLength(Fix.FromInt(14));
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var spot = hq.Pos + toward + new FixVec2(_rng.Range(-9, 9), _rng.Range(-9, 9));
            if (w.CanPlaceDefence(_player, spot, kind.Value, out _)) { issue(new PlaceDefence(_player, spot, kind.Value)); return; }
        }
    }

    /// <summary>Idle scouts go to a random spot in the middle of the map, so the AI sees what's coming.</summary>
    void SendScouts(World w, Action<Command> issue)
    {
        foreach (var s in w.Entities)
        {
            if (s.Dead || s.Owner != _player || !World.IsScout(s.Kind) || s.HasWaypoint) continue;
            var mid = (w.Players[0].HqPos + w.Players[1].HqPos) * Fix.Ratio(1, 2);
            var spot = mid + new FixVec2(_rng.Range(-30, 30), _rng.Range(-20, 20));
            issue(new MoveUnits(_player, new[] { s.Id }, spot));
        }
    }
}
