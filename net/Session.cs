using System.Collections.Generic;
using System.Linq;
using Battler.Sim;

namespace Battler.Net;

/// <summary>Owns the world and decides when it may advance. The game only talks to this.</summary>
public interface ISession
{
    World World { get; }
    int LocalPlayer { get; }
    /// <summary>Queue a command from the local player.</summary>
    void Issue(Command c);
    /// <summary>Advance one tick if allowed; false means waiting (on the network).</summary>
    bool TryStep();
    /// <summary>Why the session can't continue (disconnect, desync), or null.</summary>
    string Problem { get; }
}

/// <summary>Against the computer: no network, the AI issues the other side's commands.</summary>
/// <summary>A straight fight to watch, no bases or economy: `count` units of `kind` a side, facing each other across
/// the middle of the map (the user, 2026-10-23: "place 5 of these on a battlefield and have them fight"). They close
/// and fight on their own; the winners go on to the enemy HQ.</summary>
public sealed class SkirmishSession : ISession
{
    readonly List<Command> _pending = new();

    public SkirmishSession(ulong seed, Kind kind, int count, int apart = 34)
    {
        World = new World(seed) { Started = true };
        for (int i = 0; i < count; i++)
        {
            int across = (i - count / 2) * 4;
            World.Spawn(kind, 0, new FixVec2(-apart / 2, across));
            World.Spawn(kind, 1, new FixVec2(apart / 2, -across));
        }
    }

    public World World { get; }
    public int LocalPlayer => 0;
    public string Problem => null;
    public void Issue(Command c) => _pending.Add(c);

    public bool TryStep()
    {
        World.Step(_pending.ToList());
        _pending.Clear();
        return true;
    }
}

public sealed class LocalSession : ISession
{
    readonly SimpleAi _ai;
    readonly List<Command> _pending = new();

    public LocalSession(ulong seed, int localPlayer = 0)
    {
        World = new World(seed);
        LocalPlayer = localPlayer;
        _ai = new SimpleAi(1 - localPlayer, seed ^ 0xA1);
    }

    public World World { get; }
    public int LocalPlayer { get; }
    public string Problem => null;

    public void Issue(Command c) => _pending.Add(c);

    public bool TryStep()
    {
        _ai.Think(World, _pending.Add);
        var cmds = _pending.OrderBy(c => c.Player).ToList();   // stable: keeps each player's order
        _pending.Clear();
        World.Step(cmds);
        return true;
    }
}

/// <summary>
/// Lockstep over a transport. Commands given now are scheduled <see cref="InputDelay"/> ticks
/// ahead and sent to the other player; a tick is only stepped once both players' inputs for it
/// have arrived, so both machines apply identical commands on identical ticks. Every input also
/// carries the sender's latest checksum, which is compared to catch desyncs.
/// </summary>
public sealed class LockstepSession : ISession
{
    public const int InputDelay = 2;   // ticks; 200 ms at 10/s, plenty for LAN

    readonly ITransport _net;
    readonly Dictionary<int, List<Command>>[] _inputs = { new(), new() };
    readonly List<Command> _pending = new();
    readonly Dictionary<int, ulong> _mySums = new();
    readonly Dictionary<int, ulong> _theirSums = new();
    int _lastSent;

    public LockstepSession(ITransport net, ulong seed, int localPlayer)
    {
        _net = net;
        World = new World(seed);
        LocalPlayer = localPlayer;
        // The first ticks happen before anyone could have sent anything: empty for both.
        for (int t = 1; t <= InputDelay; t++)
            foreach (var d in _inputs) d[t] = new List<Command>();
        _lastSent = InputDelay;
    }

    public World World { get; }
    public int LocalPlayer { get; }
    public string Problem { get; private set; }
    /// <summary>Ticks whose checksums both sides have compared.</summary>
    public int VerifiedTicks { get; private set; }

    public void Issue(Command c)
    {
        if (c.Player == LocalPlayer) _pending.Add(c);
    }

    public bool TryStep()
    {
        if (Problem != null) return false;
        int next = World.Tick + 1;

        // Send my input for the tick InputDelay ahead (once).
        int sendTick = next + InputDelay;
        if (_lastSent < sendTick)
        {
            var input = new Wire.Input { Tick = sendTick, Commands = new List<Command>(_pending), SumTick = World.Tick };
            input.Sum = _mySums.TryGetValue(World.Tick, out var s) ? s : 0;
            _pending.Clear();
            _inputs[LocalPlayer][sendTick] = input.Commands;
            _net.Send(Wire.EncodeInput(input));
            _lastSent = sendTick;
        }

        Receive();
        if (Problem != null) return false;

        if (!_inputs[0].TryGetValue(next, out var c0) || !_inputs[1].TryGetValue(next, out var c1))
        {
            if (_net.Closed) Problem = "Opponent disconnected";
            return false;   // waiting for the other player
        }
        _inputs[0].Remove(next);
        _inputs[1].Remove(next);

        var cmds = new List<Command>(c0.Count + c1.Count);
        cmds.AddRange(c0);
        cmds.AddRange(c1);
        World.Step(cmds);

        var sum = World.Checksum();
        _mySums[World.Tick] = sum;
        Compare(World.Tick);
        _mySums.Remove(World.Tick - 100);   // the other side is never that far behind
        return true;
    }

    void Receive()
    {
        int remote = 1 - LocalPlayer;
        while (_net.TryReceive(out var msg))
        {
            if (Wire.Type(msg) != Wire.MsgInput) continue;
            var input = Wire.DecodeInput(msg);
            // A peer may only speak for its own player.
            input.Commands.RemoveAll(c => c.Player != remote);
            _inputs[remote][input.Tick] = input.Commands;
            if (input.SumTick > 0)
            {
                _theirSums[input.SumTick] = input.Sum;
                Compare(input.SumTick);
            }
        }
    }

    void Compare(int tick)
    {
        if (!_mySums.TryGetValue(tick, out var mine) || !_theirSums.TryGetValue(tick, out var theirs)) return;
        _theirSums.Remove(tick);
        if (mine != theirs) Problem = $"DESYNC at tick {tick} ({mine:X16} vs {theirs:X16})";
        else VerifiedTicks++;
    }
}
