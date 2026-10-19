using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Battler.Net;
using Battler.Sim;

// Usage: dotnet run [seed]          AI vs AI, then replay the recorded commands and compare checksums
//        dotnet run [seed] lan      two lockstep sessions over a localhost socket, each with its own AI
ulong Seed = args.Length > 0 && ulong.TryParse(args[0], out var s) ? s : 12345;
const int MaxTicks = 20 * 60 * World.TicksPerSecond;   // 20 minutes

return args.Contains("lan") ? LanTest() : ReplayTest();

int ReplayTest()
{
    // Live run: two AIs, plus player 0 calls a pause at tick 600 and ends it early at 700.
    var world = new World(Seed);
    var swap = Environment.GetEnvironmentVariable("SWAP_AI") == "1";   // each AI personality on the other side
    var ais = new[] { new SimpleAi(0, Seed * 7 + (swap ? 2UL : 1UL)), new SimpleAi(1, Seed * 7 + (swap ? 1UL : 2UL)) };
    var log = new List<Command[]>();
    var sums = new List<ulong>();
    int pausedTicks = 0;
    while (world.Winner < 0 && world.Tick < MaxTicks)
    {
        var cmds = new List<Command>();
        foreach (var ai in ais) ai.Think(world, cmds.Add);
        bool script = Environment.GetEnvironmentVariable("NOSCRIPT") != "1";   // NOSCRIPT=1: just the AIs, for fair win counts
        if (script && world.Tick == 600) cmds.Add(new Pause(0));
        if (script && world.Tick == 700) cmds.Add(new Resume(0));
        // Player 0 holds its first factory's units near home for a while, then lets them go.
        var first0 = world.Entities.Find(e => e.Kind == Kind.Factory && e.Owner == 0);
        if (script && first0 != null && world.Tick == 300) cmds.Add(new SetRally(0, first0.Id, false, new FixVec2(-30, 5)));
        if (script && first0 != null && world.Tick == 1500) cmds.Add(new SetRally(0, first0.Id, true, FixVec2.Zero));
        // ...and later sells its newest factory or outpost.
        if (script && world.Tick == 2000 && world.Entities.FindLast(e => e.Kind is Kind.Factory or Kind.Outpost && e.Owner == 0) is { } sold)
        {
            Console.WriteLine($"  t={world.Tick}: player 0 sells {sold.Kind} {sold.Id} for {World.SellValue(sold)} (metal {world.Players[0].Metal})");
            cmds.Add(new Sell(0, sold.Id));
        }
        var ordered = cmds.OrderBy(c => c.Player).ToArray();   // the agreed order
        log.Add(ordered);
        world.Step(ordered);
        if (world.Paused) pausedTicks++;
        sums.Add(world.Checksum());
        if (world.Tick % 1200 == 0) Report(world);
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
}

int LanTest()
{
    const int Port = TcpTransport.DefaultPort + 1;
    var listener = TcpTransport.Listen(Port);
    var connecting = TcpTransport.ConnectAsync("127.0.0.1", Port);
    TcpTransport hostNet = null;
    while (hostNet == null) { hostNet = TcpTransport.TryAccept(listener); Thread.Sleep(1); }
    var clientNet = connecting.Result;
    listener.Stop();

    // The host picks the seed and tells the client, exactly as the game does.
    hostNet.Send(Wire.Start(Seed));
    byte[] msg;
    while (!clientNet.TryReceive(out msg)) Thread.Sleep(1);
    var clientSeed = Wire.ReadStart(msg);

    var sessions = new[] { new LockstepSession(hostNet, Seed, 0), new LockstepSession(clientNet, clientSeed, 1) };
    var ais = new[] { new SimpleAi(0, Seed * 7 + 1), new SimpleAi(1, Seed * 7 + 2) };
    var thoughtAt = new[] { -1, -1 };
    var start = DateTime.UtcNow;
    long stalls = 0;
    var pausedTicks = new[] { 0, 0 };

    bool Done(LockstepSession x) => x.Problem != null || x.World.Winner >= 0 || x.World.Tick >= MaxTicks;
    while (!sessions.All(Done))
    {
        bool any = false;
        for (int i = 0; i < 2; i++)
        {
            var ss = sessions[i];
            if (Done(ss)) continue;
            var w = ss.World;
            if (thoughtAt[i] != w.Tick)   // once per tick, even while waiting on the network
            {
                thoughtAt[i] = w.Tick;
                ais[i].Think(w, ss.Issue);
                // The client calls a pause and ends it early.
                if (i == 1 && w.Tick == 600) ss.Issue(new Pause(1));
                if (i == 1 && w.Tick == 700) ss.Issue(new Resume(1));
                var first1 = w.Entities.Find(e => e.Kind == Kind.Factory && e.Owner == 1);
                if (i == 1 && first1 != null && w.Tick == 300) ss.Issue(new SetRally(1, first1.Id, false, new FixVec2(30, -5)));
                if (i == 1 && first1 != null && w.Tick == 1500) ss.Issue(new SetRally(1, first1.Id, true, FixVec2.Zero));
                if (i == 1 && w.Tick == 2000 && w.Entities.FindLast(e => e.Kind is Kind.Factory or Kind.Outpost && e.Owner == 1) is { } sold) ss.Issue(new Sell(1, sold.Id));
            }
            if (ss.TryStep()) { any = true; if (w.Paused) pausedTicks[i]++; if (i == 0 && w.Tick % 1200 == 0) Report(w); }
            else stalls++;
        }
        if (!any) Thread.Yield();
    }

    var (a, b) = (sessions[0], sessions[1]);
    Console.WriteLine($"Host ended at tick {a.World.Tick}, client at {b.World.Tick}, winner {a.World.Winner}/{b.World.Winner}, " +
                      $"{(DateTime.UtcNow - start).TotalSeconds:0.0}s wall, {stalls} waits");
    Console.WriteLine($"Checksums verified over the wire: host {a.VerifiedTicks}, client {b.VerifiedTicks}");
    Console.WriteLine($"Paused ticks: host {pausedTicks[0]}, client {pausedTicks[1]}");
    if (a.Problem != null || b.Problem != null) { Console.WriteLine($"PROBLEM: {a.Problem} / {b.Problem}"); return 1; }
    if (a.World.Tick != b.World.Tick || a.World.Checksum() != b.World.Checksum()) { Console.WriteLine("FINAL STATES DIFFER"); return 1; }
    Console.WriteLine($"LAN lockstep OK (final checksum {a.World.Checksum():X16})");
    hostNet.Dispose(); clientNet.Dispose();
    return 0;
}

static int MaxRank(World w) { int r = 0; foreach (var e in w.Entities) r = Math.Max(r, e.Rank); return r; }

static void Report(World world)
{
    int m0 = 0, m1 = 0, f0 = 0, f1 = 0, o0 = 0, o1 = 0;
    foreach (var e in world.Entities)
    {
        if (e.Stats.IsUnit) { if (e.Owner == 0) m0++; else m1++; }
        if (e.Kind == Kind.Factory) { if (e.Owner == 0) f0++; else f1++; }
        if (e.Kind == Kind.Outpost) { if (e.Owner == 0) o0++; else o1++; }
    }
    var hq = world.Entities.FindAll(e => e.Kind == Kind.Hq);
    Console.WriteLine($"t={world.Tick / 10,4}s  factories {f0}v{f1}  outposts {o0}v{o1}  units {m0}v{m1}  HQ hp {string.Join("/", hq.ConvertAll(h => h.Hp))}  top rank {MaxRank(world)}");
}
