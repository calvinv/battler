using System;
using System.Collections.Generic;
using Battler.Sim;

ulong Seed = args.Length > 0 ? ulong.Parse(args[0]) : 12345;
const int MaxTicks = 20 * 60 * World.TicksPerSecond;   // 20 minutes

// Live run: two AIs, plus player 0 calls a pause at tick 600 and ends it early at 700.
var world = new World(Seed);
var ais = new[] { new SimpleAi(0, Seed * 7 + 1), new SimpleAi(1, Seed * 7 + 2) };
var log = new List<Command[]>();
var sums = new List<ulong>();
int pausedTicks = 0;
while (world.Winner < 0 && world.Tick < MaxTicks)
{
    var cmds = new List<Command>();
    foreach (var ai in ais) ai.Think(world, cmds.Add);
    if (world.Tick == 600) cmds.Add(new Pause(0));
    if (world.Tick == 700) cmds.Add(new Resume(0));
    cmds.Sort((a, b) => a.Player.CompareTo(b.Player));   // the agreed order
    log.Add(cmds.ToArray());
    world.Step(cmds);
    if (world.Paused) pausedTicks++;
    sums.Add(world.Checksum());
    if (world.Tick % 1200 == 0)
    {
        int m0 = 0, m1 = 0, f0 = 0, f1 = 0;
        foreach (var e in world.Entities)
        {
            if (e.Kind == Kind.Mech) { if (e.Owner == 0) m0++; else m1++; }
            if (e.Kind == Kind.Factory) { if (e.Owner == 0) f0++; else f1++; }
        }
        var hq = world.Entities.FindAll(e => e.Kind == Kind.Hq);
        Console.WriteLine($"t={world.Tick / 10,4}s  factories {f0}v{f1}  mechs {m0}v{m1}  HQ hp {string.Join("/", hq.ConvertAll(h => h.Hp))}");
    }
}
Console.WriteLine($"Ended at t={world.Tick / 10}s, winner: {(world.Winner < 0 ? "none" : "player " + world.Winner)}, paused for {pausedTicks} ticks");

// Replay: a fresh world fed only the recorded commands must match on every tick.
var replay = new World(Seed);
for (int i = 0; i < log.Count; i++)
{
    replay.Step(log[i]);
    if (replay.Checksum() != sums[i]) { Console.WriteLine($"DESYNC at tick {i + 1}"); return 1; }
}
Console.WriteLine($"Replay matched all {log.Count} ticks (final checksum {sums[^1]:X16})");
return 0;
