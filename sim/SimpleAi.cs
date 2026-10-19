using System;

namespace Battler.Sim;

/// <summary>
/// A computer opponent. It sits outside the simulation and only issues commands, exactly like a
/// human player, so it needs no special handling in a networked match.
/// </summary>
public sealed class SimpleAi
{
    readonly int _player;
    readonly Rng _rng;

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
        // A third of the time, try upgrading one of its factories instead of saving for a new one.
        if (_rng.Next(3) == 0)
        {
            var mine = w.Entities.FindAll(e => e.Kind is Kind.Factory or Kind.Outpost && e.Owner == _player);
            if (mine.Count > 0)
            {
                var f = mine[_rng.Next(mine.Count)];
                var which = (Upgrade)_rng.Next(3);
                if (w.CanUpgrade(_player, f.Id, which, out _)) { issue(new UpgradeFactory(_player, f.Id, which)); return; }
            }
        }
        // With two factories, push out an outpost or two toward the middle (wherever it can see).
        var factories = w.Entities.FindAll(e => e.Kind == Kind.Factory && e.Owner == _player).Count;
        var outposts = w.Entities.FindAll(e => e.Kind == Kind.Outpost && e.Owner == _player);
        if (factories >= 2 && outposts.Count < 2)
        {
            if (p.Metal < World.OutpostCost) return;   // saving up for it
            int ahead = _player == 0 ? 1 : -1;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var pos = p.HqPos + new FixVec2(ahead * _rng.Range(20, 45), _rng.Range(-30, 30));
                if (w.CanPlaceOutpost(_player, pos, out _)) { issue(new PlaceOutpost(_player, pos)); return; }
            }
        }
        // Mostly tanks, with some scouts and artillery; near the HQ or, sometimes, an outpost.
        var roll = _rng.Next(4);
        var makes = roll < 2 ? Kind.Tank : roll == 2 ? Kind.Scout : Kind.Artillery;
        var anchor = outposts.Count > 0 && _rng.Next(3) == 0 ? outposts[_rng.Next(outposts.Count)].Pos : p.HqPos;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var pos = anchor + new FixVec2(_rng.Range(-14, 14), _rng.Range(-14, 14)) * (anchor.X == p.HqPos.X ? Fix.Ratio(3, 2) : Fix.One);
            if (w.CanPlaceFactory(_player, pos, makes, out _)) { issue(new PlaceFactory(_player, pos, makes)); return; }
        }
    }
}
