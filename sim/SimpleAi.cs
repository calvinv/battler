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
            var mine = w.Entities.FindAll(e => e.Kind == Kind.Factory && e.Owner == _player);
            if (mine.Count > 0)
            {
                var f = mine[_rng.Next(mine.Count)];
                var which = (Upgrade)_rng.Next(3);
                if (w.CanUpgrade(_player, f.Id, which, out _)) { issue(new UpgradeFactory(_player, f.Id, which)); return; }
            }
        }
        // Mostly tanks, with some scouts and artillery.
        var roll = _rng.Next(4);
        var makes = roll < 2 ? Kind.Tank : roll == 2 ? Kind.Scout : Kind.Artillery;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var pos = p.HqPos + new FixVec2(_rng.Range(-20, 20), _rng.Range(-20, 20));
            if (w.CanPlaceFactory(_player, pos, makes, out _)) { issue(new PlaceFactory(_player, pos, makes)); return; }
        }
    }
}
