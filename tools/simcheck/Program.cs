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

if (args.Contains("battle"))   // how long a straight fight between equal groups lasts (the user: "time to kill ... 1 minute")
{
    foreach (var (kind, n) in new[] { (Kind.Tank, 1), (Kind.Tank, 5), (Kind.LightTank, 5), (Kind.Spider, 5) })
    {
        var w = new World(3, new[] { Civ.Coalition, Civ.Lattice }) { Started = true };
        for (int i = 0; i < n; i++)
        {
            w.Spawn(kind, 0, new FixVec2(-40, -6 + i * 3));
            w.Spawn(kind, 1, new FixVec2(-28, -6 + i * 3));
        }
        int first = -1, t = 0;
        while (t++ < 6000)
        {
            w.Step(Array.Empty<Command>());
            bool hit = w.Entities.Exists(e => e.Kind == kind && e.Hp < e.MaxHp);
            if (first < 0 && hit) first = w.Tick;
            int a = w.Entities.Count(e => e.Kind == kind && e.Owner == 0), b = w.Entities.Count(e => e.Kind == kind && e.Owner == 1);
            if (a == 0 || b == 0) { Console.WriteLine($"{n} {kind} v {n}: {(w.Tick - first) / (float)World.TicksPerSecond:0.0} s of fighting, {a} v {b} left"); break; }
        }
    }
    return 0;
}
if (args.Contains("matchups"))   // the cheap and air units against each other and the rest
{
    foreach (var (ka, na, kb, nb) in new[] { (Kind.Ranger, 5, Kind.Ranger, 5), (Kind.Ranger, 5, Kind.LightTank, 3), (Kind.Flier, 5, Kind.Flier, 5),
                                             (Kind.Bomber, 3, Kind.Tank, 5), (Kind.Ranger, 3, Kind.Bomber, 1), (Kind.Ranger, 4, Kind.Bomber, 1), (Kind.Ranger, 5, Kind.Bomber, 1), (Kind.Flier, 2, Kind.Bomber, 2) })
    {
        var w = new World(3, new[] { Civ.Coalition, Civ.Coalition }) { Started = true };
        foreach (var t in w.Entities.FindAll(e => e.Kind == Kind.Tower)) t.Hp = 0;
        w.Step(Array.Empty<Command>());
        int apart = ka == Kind.Flier || kb == Kind.Flier ? 6 : 12;   // (interceptors only go for fliers they've seen)
        bool swap = Environment.GetEnvironmentVariable("SWAP") == "1";   // (player 1's made first: does the order matter?)
        if (swap) for (int i = 0; i < nb; i++) w.Spawn(kb, 1, -new FixVec2(-apart, -6 + i * 3));
        for (int i = 0; i < na; i++) w.Spawn(ka, 0, new FixVec2(-apart, -6 + i * 3));
        if (!swap) for (int i = 0; i < nb; i++) w.Spawn(kb, 1, -new FixVec2(-apart, -6 + i * 3));   // (turned round, as the map is)
        int first = -1, t2 = 0;
        string end = "still going after 3 minutes";
        while (t2++ < 1800)
        {
            w.Step(Array.Empty<Command>());
            if (Environment.GetEnvironmentVariable("TRACE") == "1" && t2 % 3 == 0 && ka == Kind.Bomber && t2 > 60 && t2 < 130)
                Console.WriteLine($"  t{t2}: " + string.Join("  ", w.Entities.FindAll(e => e.Kind == ka && e.Id == w.Entities.Find(b => b.Kind == ka).Id).ConvertAll(e => $"p{e.Owner}({e.Pos.X.ToInt()},{e.Pos.Z.ToInt()}) {e.Hp} cd{e.Cooldown} sh{w.Shots.Count}")));
            if (first < 0 && w.Entities.Exists(e => (e.Kind == ka || e.Kind == kb) && e.Hp < e.MaxHp)) first = w.Tick;
            int a = w.Entities.Count(e => e.Kind == ka && e.Owner == 0), b = w.Entities.Count(e => e.Kind == kb && e.Owner == 1);
            if (a == 0 || b == 0) { end = $"{(w.Tick - first) / (float)World.TicksPerSecond:0.0} s of fighting, {a} v {b} left"; break; }
        }
        Console.WriteLine($"{na} {ka} v {nb} {kb}: {end}");
    }
    return 0;
}
if (args.Contains("crowd"))   // a big group of tanks marching out of a busy base to the enemy HQ: how well do they get there?
{
    int n = 30;
    var w = new World(9, new[] { Civ.Coalition, Civ.Coalition }) { Started = true };
    foreach (var p in w.Players) p.Metal = 100000;
    var hq0 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
    var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
    hq1.MaxHp = hq1.Hp = 10000000;   // (it never falls: this is about getting there)
    foreach (var t in w.Entities.FindAll(e => e.Kind == Kind.Tower && e.Owner == 1)) t.Hp = 0;
    var tanks = new List<Entity>();
    for (int i = 0; i < n; i++)   // round the HQ, as they'd come out of it
    {
        var ang = i * 2.399;   // (a spiral)
        var r = 9 + i * 0.5;
        var at = hq0.Pos + new FixVec2(Fix.FromFloat((float)(Math.Cos(ang) * r)), Fix.FromFloat((float)(Math.Sin(ang) * r)));
        tanks.Add(w.Spawn(Kind.Tank, 0, at));
    }
    var last = tanks.ConvertAll(t => t.Pos);
    int stuckTotal = 0;
    // Jitter: how often a moving tank's step swings more than 60 degrees from its last one (weaving, shoving).
    var prevStep = tanks.ConvertAll(t => FixVec2.Zero);
    var prevPos = tanks.ConvertAll(t => t.Pos);
    long swings = 0, steps = 0;
    for (int sec = 5; sec <= 150; sec += 5)
    {
        for (int t = 0; t < 50; t++)
        {
            w.Step(Array.Empty<Command>());
            for (int i = 0; i < n; i++)
            {
                var step = tanks[i].Pos - prevPos[i];
                prevPos[i] = tanks[i].Pos;
                if (step.LengthSq < Fix.Ratio(1, 400)) { prevStep[i] = FixVec2.Zero; continue; }
                if (prevStep[i].LengthSq.Raw > 0)
                {
                    steps++;
                    var cos = (step.X * prevStep[i].X + step.Z * prevStep[i].Z) / (step.Length * prevStep[i].Length);
                    if (cos < Fix.Ratio(1, 2)) swings++;
                }
                prevStep[i] = step;
            }
        }
        int arrived = 0, stuck = 0;
        float sumGap = 0;
        for (int i = 0; i < n; i++)
        {
            var e = tanks[i];
            var gap = (e.Pos - hq1.Pos).Length.ToFloat() - 3 - 1.3f;
            sumGap += gap;
            if (gap <= 8.5f) arrived++;
            else if ((e.Pos - last[i]).Length.ToFloat() < 1f) stuck++;
            last[i] = e.Pos;
        }
        stuckTotal += stuck;
        float worst = 0;   // the deepest two tanks sink into each other, as a share of their width
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                worst = Math.Max(worst, 1 - (tanks[i].Pos - tanks[j].Pos).Length.ToFloat() / 2.6f);
        Console.WriteLine($"t={sec,3}s  in range {arrived,2}/{n}  stuck {stuck,2}  mean gap {sumGap / n:0.0}  worst overlap {worst * 100:0}%");
        if (arrived == n) break;
    }
    Console.WriteLine($"stuck-samples total {stuckTotal}   sharp swings {swings} of {steps} moving steps ({100.0 * swings / Math.Max(1, steps):0.0}%)");
    return 0;
}
if (args.Contains("crowd2"))   // (1) 30 tanks sent to one rally point; (2) 30 v 30 tanks meeting head on
{
    // (1) The rally: how many are still shoving about there after they've had time to arrive?
    {
        var w = new World(9, new[] { Civ.Coalition, Civ.Coalition }) { Started = true };
        foreach (var t in w.Entities.FindAll(e => e.Kind == Kind.Tower)) t.Hp = 0;
        var tanks = new List<Entity>();
        for (int i = 0; i < 30; i++)
        {
            var e = w.Spawn(Kind.Tank, 0, new FixVec2(-80 + (i % 6) * 3, -20 + (i / 6) * 3));
            e.HasWaypoint = true; e.Waypoint = new FixVec2(-30, 0);
            tanks.Add(e);
        }
        var last = tanks.ConvertAll(t => t.Pos);
        for (int sec = 5; sec <= 60; sec += 5)
        {
            for (int t = 0; t < 50; t++) w.Step(Array.Empty<Command>());
            int moving = 0, waiting = 0;
            for (int i = 0; i < 30; i++) { if ((tanks[i].Pos - last[i]).Length.ToFloat() > 1f) moving++; if (tanks[i].HasWaypoint) waiting++; last[i] = tanks[i].Pos; }
            Console.WriteLine($"rally t={sec,2}s  still moving {moving,2}/30  not there yet {waiting,2}/30");
        }
    }
    // (2) The fight: how many of each side get to shoot, second by second? Player 0 has light tanks behind its tanks.
    {
        var w = new World(9, new[] { Civ.Coalition, Civ.Coalition }) { Started = true };
        foreach (var t in w.Entities.FindAll(e => e.Kind == Kind.Tower)) t.Hp = 0;
        for (int i = 0; i < 30; i++)
        {
            w.Spawn(i < 15 ? Kind.Tank : Kind.LightTank, 0, new FixVec2(-60 + (i / 5) * 3, -8 + (i % 5) * 3));   // (tanks in front, light tanks behind)
            w.Spawn(Kind.Tank, 1, new FixVec2(60 - (i % 6) * 3, 8 - (i / 6) * 3));
        }
        var fired = new[] { new HashSet<int>(), new HashSet<int>() };
        for (int sec = 1; sec <= 120; sec++)
        {
            fired[0].Clear(); fired[1].Clear();
            for (int t = 0; t < 10; t++) { w.Step(Array.Empty<Command>()); foreach (var sh in w.Shots) if (sh.Owner < 2) fired[sh.Owner].Add(sh.ShooterId); }
            int a = w.Entities.Count(e => World.IsArmy(e.Kind) && e.Owner == 0), b = w.Entities.Count(e => World.IsArmy(e.Kind) && e.Owner == 1);
            int InRange(int side)
            {
                int n = 0;
                foreach (var u in w.Entities)
                {
                    if (!World.IsArmy(u.Kind) || u.Owner != side) continue;
                    foreach (var o in w.Entities)
                        if (World.IsArmy(o.Kind) && o.Owner != side && (o.Pos - u.Pos).Length - u.Stats.Radius - o.Stats.Radius <= u.Stats.Range) { n++; break; }
                }
                return n;
            }
            if (sec % 5 == 0 || a == 0 || b == 0) Console.WriteLine($"fight t={sec,3}s  alive {a,2} v {b,2}   in range {InRange(0),2} v {InRange(1),2}   firing {fired[0].Count,2} v {fired[1].Count,2}");
            if (a == 0 || b == 0) break;
        }
    }
    return 0;
}
if (args.Contains("probe"))   // print the ground at a few places (for checking map shapes)
{
    foreach (var (x, z) in new[] { (-126, -100), (-126, -60), (-200, -120), (0, -120), (0, 0), (126, 100), (-224, 100) })
    {
        var p = new FixVec2(x, z);
        Console.WriteLine($"({x},{z}) shore N {Ground.NorthShore(Fix.FromInt(x)).ToFloat():0.0} S {Ground.SouthShore(Fix.FromInt(x)).ToFloat():0.0} depth {Ground.WaterDepth(p).ToFloat():0.0} height {World.Ground.HeightAt(p).ToFloat():0.0} open {Nav.OpenAt(p)}");
    }
    return 0;
}
if (args.Contains("map"))   // the open ground as text, 8 units a character: # shut, . open, D deposit, H HQ
{
    for (int z = -168; z <= 168; z += 8)
    {
        var row = new System.Text.StringBuilder();
        for (int x = -280; x <= 280; x += 6)
        {
            var at = new FixVec2(x, z);
            char c = Nav.OpenAt(at) ? '.' : '#';
            foreach (var d in World.Deposits) if ((d - at).LengthSq < Fix.FromInt(16)) c = 'D';
            if ((World.HqStart(0) - at).LengthSq < Fix.FromInt(25) || (World.HqStart(1) - at).LengthSq < Fix.FromInt(25)) c = 'H';
            if (x == 0 && z == 0) c = '+';
            row.Append(c);
        }
        Console.WriteLine($"{z,5} {row}");
    }
    return 0;
}
if (args.Contains("doors"))   // each side's doors and output over a match (AI v AI)
{
    var w = new World(Seed);
    var ais = new[] { new SimpleAi(0, Seed * 7 + 1), new SimpleAi(1, Seed * 7 + 2) };
    while (w.Tick < 3000)
    {
        var cmds = new List<Command>();
        foreach (var ai in ais) ai.Think(w, cmds.Add);
        w.Step(cmds.OrderBy(c => c.Player).ToArray());
        if (w.Tick % 150 == 0)
            foreach (var p in w.Players)
                Console.WriteLine($"t={w.Tick / 10,3} p{p.Id} made {p.Made,2} doors [{string.Join(",", p.DoorMakes.Select((k, d) => k < 0 ? "-" : $"{(Kind)k}:{p.DoorTimer[d]}"))}] queue [{string.Join(",", p.Queue.Select(k => ((Kind)k).ToString()))}] research [{string.Join(",", p.Research.Select(t => ((Tech)t).ToString()))}] {p.ResearchSpent} metal {p.Metal} army {w.ArmyCount(p.Id)}");
    }
    return 0;
}
Console.WriteLine($"Map: {Map.Name}");
if (!HqTest() || !UpgradeTest()) return 1;
return args.Contains("lan") ? LanTest() : ReplayTest();

// The HQ (the user, 2026-10-24: one pyramid, one shared queue, doors as upgrades): a queued unit is paid for when a
// door starts it and rolls out of the front face after its build time, and Repeat puts it back on the queue; the
// battle wheel can't be queued till researched; research is paid as it starts and refunded if dropped; a second door
// builds two at once; the pyramid grows a tier (and the HQ gains health) as metal comes in. Then the radar: an enemy
// tank hidden in the fog near the HQ shows on the next sweep, and becomes known.
bool HqTest()
{
    var w = new World(1, new[] { Civ.Coalition, Civ.Coalition });
    var p = w.Players[0];
    var hq0 = w.HqOf(0);
    w.Step(new Command[] { new QueueUnit(0, Kind.LightTank), new QueueUnit(1, Kind.Ranger) });
    bool started = w.Started;
    w.Step(Array.Empty<Command>());
    bool paid = p.Metal == World.StartMetal - World.UnitCost(Kind.LightTank) && p.Queue.Count == 0 && p.DoorMakes[0] == (int)Kind.LightTank;
    int t0 = w.Tick;
    Entity first = null;
    while (first == null && w.Tick - t0 < 400) { w.Step(Array.Empty<Command>()); first = w.Entities.Find(e => e.Kind == Kind.LightTank && e.Owner == 0); }
    int took = w.Tick - t0;
    bool outFront = first != null && first.Door == 0 && (first.Pos - w.DoorSpot(0, 0, Kind.LightTank)).Length < 2;
    bool repeated = p.Queue.Count == 1 && p.Queue[0] == (int)Kind.LightTank;

    bool lockedTank = !w.CanQueue(0, Kind.Tank, out var lockWhy);
    p.Metal = 100000;
    w.Step(new Command[] { new SetRepeat(0, false), new Research(0, Tech.Door2), new Research(0, Tech.Wheel) });
    for (int t = 0; t < 50; t++) w.Step(Array.Empty<Command>());
    bool payingAsItGoes = p.ResearchSpent > 0 && p.ResearchSpent < Techs.Of(Tech.Door2).Cost / 3;
    w.Step(new Command[] { new CancelResearch(0, 1) });
    bool dropped = p.Research.Count == 1 && p.Research[0] == (int)Tech.Door2;
    w.Step(new Command[] { new CancelResearch(0, 0) });
    bool refunded = p.Research.Count == 0 && p.ResearchSpent == 0 && p.ResearchLeft == 0;
    w.Step(new Command[] { new Research(0, Tech.Door2), new Research(0, Tech.Wheel) });
    for (int t = 0; t < 70 * World.TicksPerSecond && p.Research.Count > 0; t++) w.Step(Array.Empty<Command>());
    bool unlocked = p.Has[(int)Tech.Door2] && w.CanQueue(0, Kind.Tank, out _) && World.DoorsOf(p) == 2;
    while (p.DoorMakes[0] >= 0) w.Step(Array.Empty<Command>());   // (the repeat light tank done)
    w.Step(new Command[] { new QueueUnit(0, Kind.Tank), new QueueUnit(0, Kind.Ranger) });
    w.Step(Array.Empty<Command>());
    bool twoAtOnce = p.DoorMakes[0] == (int)Kind.Tank && p.DoorMakes[1] == (int)Kind.Ranger;

    int hpBefore = hq0.MaxHp, tierBefore = p.Tier;
    p.Mined += World.CollectedFor(3);
    w.Step(Array.Empty<Command>());
    bool grew = tierBefore == 1 && p.Tier == 3 && hq0.MaxHp > hpBefore && hq0.Hp == hq0.MaxHp;

    // Radar: an enemy tank 60 out from the HQ, where nothing of ours sees it.
    while (w.Tick % World.RadarTicks != World.RadarTicks - 1) w.Step(Array.Empty<Command>());   // (just before a sweep)
    var sneak = w.Spawn(Kind.Tank, 1, hq0.Pos + new FixVec2(-10, -55));
    w.Step(Array.Empty<Command>());
    bool blip = w.Players[0].Blips.Count > 0 && w.Players[0].Known.ContainsKey(sneak.Id);
    bool ok = started && paid && first != null && Math.Abs(took - World.StatsFor[(int)Kind.LightTank].BuildTicks) <= 2 && outFront && repeated
        && lockedTank && payingAsItGoes && dropped && refunded && unlocked && twoAtOnce && grew && blip;
    Console.WriteLine($"HQ test: started {started}, paid at the door {paid}, first light tank after {took / 10.0:0.0} s out front {outFront}, repeated {repeated}; " +
        $"tank locked {lockedTank} ({lockWhy}), research paid as it goes {payingAsItGoes}, dropped {dropped} refunded {refunded}, unlocked {unlocked}, two doors at once {twoAtOnce}; " +
        $"tier {tierBefore}->{p.Tier} HQ {hpBefore}->{hq0.MaxHp}; radar blip {blip}: {(ok ? "OK" : "FAILED")}");
    return ok && (!World.FeralsOn || WaveTest()) && MenderTest() && BulwarkTest();
}

// A mender catches up with a damaged tank and mends it back to full, no further.
bool MenderTest()
{
    var w = new World(4, new[] { Civ.Coalition, Civ.Coalition });
    var hq = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
    var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
    w.Started = true;
    var tank = w.Spawn(Kind.Tank, 0, hq.Pos + new FixVec2(14, 0));
    var mender = w.Spawn(Kind.Mender, 0, hq.Pos + new FixVec2(14, 12));
    tank.Hp = tank.MaxHp / 2;
    int start = w.Tick, half = tank.Hp, after10 = 0;
    while (w.Tick - start < 1200)
    {
        w.Step(Array.Empty<Command>());
        if (w.Tick - start == 100) after10 = tank.Hp;
    }
    bool ok = !tank.Dead && after10 > half + 50 && tank.Hp >= tank.MaxHp - 20 && tank.Hp <= tank.MaxHp;   // (it ends up trading shots with the enemy HQ)
    Console.WriteLine($"Mender test: tank at {half}/{tank.MaxHp}, after 10 s {after10}, after 2 min {tank.Hp}: {(ok ? "OK" : "FAILED")}");
    return ok;
}

// A tank under a bulwark's dome takes DomePercent less from the same shot; one outside takes it all.
bool BulwarkTest()
{
    int Taken(bool domed)
    {
        var w = new World(4, new[] { Civ.Coalition, Civ.Coalition });
        var hq = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
        var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
        w.Started = true;
        var mid = new FixVec2(0, 0);
        var tank = w.Spawn(Kind.Tank, 0, mid);
        if (domed) w.Spawn(Kind.Bulwark, 0, mid + new FixVec2(-3, 0));
        w.Spawn(Kind.Tank, 1, mid + new FixVec2(8, 0));
        int start = tank.Hp, t0 = w.Tick;
        while (tank.Hp == start && w.Tick - t0 < 100) w.Step(Array.Empty<Command>());
        return start - tank.Hp;
    }
    int open = Taken(false), domed = Taken(true);
    bool ok = open > 0 && domed == open * (100 - World.DomePercent) / 100;
    Console.WriteLine($"Bulwark test: first hit {open} in the open, {domed} under a dome: {(ok ? "OK" : "FAILED")}");
    return ok;
}

// The first feral wave comes in from both edges a minute after the start.
bool WaveTest()
{
    var w = new World(3, new[] { Civ.Coalition, Civ.Coalition });
    var hqs = w.Entities.FindAll(e => e.Kind == Kind.Hq);
    w.Step(new Command[] { new QueueUnit(0, Kind.LightTank), new QueueUnit(1, Kind.LightTank) });
    for (int t = 0; t < World.FirstWaveTicks + 5; t++) w.Step(Array.Empty<Command>());
    int ferals = w.Entities.Count(e => e.Owner == World.Feral && e.Stats.IsUnit && Fix.Abs(e.Pos.X) > World.MapHalfX - 20);
    bool ok = ferals >= 6;
    Console.WriteLine($"Wave test: first wave {ferals} ferals at the edges: {(ok ? "OK" : "FAILED")}");
    return ok;
}

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
        // Player 0 sets a rally at its HQ for a while (new units gather there), then lets them go.
        if (script && world.HqOf(0) is { } hq0 && (world.Tick == 400 || world.Tick == 1800))
            cmds.Add(new SetRally(0, hq0.Id, world.Tick == 1800, new FixVec2(-45, -8)));
        // It queues research (more than it can pay for: it waits) and drops one; takes a unit off its queue; turns
        // Repeat off and on again.
        if (script && world.Tick == 1000) foreach (var t in new[] { Tech.Plating1, Tech.Servos1, Tech.Door2, Tech.Dart }) cmds.Add(new Research(0, t));
        if (script && world.Tick == 1100) cmds.Add(new CancelResearch(0, 2));
        if (script && world.Tick == 1200) cmds.Add(new Unqueue(0, 0));
        if (script && (world.Tick == 1500 || world.Tick == 1600)) cmds.Add(new SetRepeat(0, world.Tick == 1600));
        // ...and later sells its newest defence.
        if (script && world.Tick == 3000 && world.Entities.FindLast(e => World.IsDefence(e.Kind) && e.Owner == 0) is { } sold)
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
                if (i == 1 && (w.Tick == 400 || w.Tick == 1800) && w.HqOf(1) is { } hq1)
                    ss.Issue(new SetRally(1, hq1.Id, w.Tick == 1800, new FixVec2(45, 8)));
                if (i == 1 && w.Tick == 1000) foreach (var t in new[] { Tech.Gunnery1, Tech.Lobber, Tech.Door2 }) ss.Issue(new Research(1, t));
                if (i == 1 && w.Tick == 1100) ss.Issue(new CancelResearch(1, 0));
                if (i == 1 && w.Tick == 1200) ss.Issue(new Unqueue(1, 0));
                if (i == 1 && w.Tick == 3000 && w.Entities.FindLast(e => World.IsDefence(e.Kind) && e.Owner == 1) is { } sold) ss.Issue(new Sell(1, sold.Id));
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
    int m0 = 0, m1 = 0, s0 = 0, s1 = 0;
    foreach (var e in world.Entities)
    {
        if (e.Owner == World.Feral) continue;
        if (e.Stats.IsUnit) { if (e.Owner == 0) m0++; else m1++; }
        if (e.Kind == Kind.Scout) { if (e.Owner == 0) s0++; else s1++; }
    }
    var hq = world.Entities.FindAll(e => e.Kind == Kind.Hq);
    var (a, b) = (world.Players[0], world.Players[1]);
    Console.WriteLine($"t={world.Tick / 10,4}s  tier {a.Tier}v{b.Tier}  doors {World.DoorsOf(a)}v{World.DoorsOf(b)}  researched {a.Has.Count(h => h)}v{b.Has.Count(h => h)}  queued {a.Queue.Count}v{b.Queue.Count}  scouts {s0}v{s1}  metal {a.Metal}/{b.Metal}  units {m0}v{m1}  defences {world.Entities.Count(e => World.IsDefence(e.Kind) && e.Owner == 0)}v{world.Entities.Count(e => World.IsDefence(e.Kind) && e.Owner == 1)}  HQ hp {string.Join("/", hq.ConvertAll(h => h.Hp))}  top rank {MaxRank(world)}");
    string Front(int p) { var xs = world.Entities.FindAll(e => e.Owner == p && World.IsArmy(e.Kind)).ConvertAll(e => e.Pos.X.ToInt()); xs.Sort(); return xs.Count == 0 ? "-" : $"{xs[0]}..{xs[xs.Count / 2]}..{xs[^1]}"; }
    Console.WriteLine($"         earned: trickle {world.Players[0].Mined}/{world.Players[1].Mined}, salvage {world.Players[0].Salvaged}/{world.Players[1].Salvaged}");
    Console.WriteLine($"         army x (min..median..max): p0 {Front(0)}   p1 {Front(1)}   army {world.ArmyCount(0)}v{world.ArmyCount(1)}");
    // Units in the water (they never should be), and how many are in the middle.
    int shut = world.Entities.Count(e => e.Stats.IsUnit && !Nav.OpenAt(e.Pos));
    // Units pressed up against a building that isn't their target (bumping into it rather than going round).
    int bumping = world.Entities.Count(u => u.Stats.IsUnit && world.Entities.Any(b => b.Stats.IsBuilding && b.Owner == u.Owner
        && (b.Pos - u.Pos).LengthSq < (b.Stats.Radius + u.Stats.Radius + Fix.Ratio(1, 4)) * (b.Stats.Radius + u.Stats.Radius + Fix.Ratio(1, 4))));
    Console.WriteLine($"         units pressed against their own buildings: {bumping}");
    int crossing = world.Entities.Count(e => e.Stats.IsUnit && Fix.Abs(e.Pos.X) < 14);
    var zs = world.Entities.Where(e => e.Stats.IsUnit && Fix.Abs(e.Pos.X) < 14).Select(e => e.Pos.Z.ToInt()).ToList();
    Console.WriteLine($"         units in water: {shut}   in the middle: {crossing}" + (zs.Count > 0 ? $" (z {zs.Min()}..{zs.Max()})" : ""));
}

// Research upgrades every unit made after it, side-wide: gunnery hits harder, a shield coil shields, the twin feed
// sends every third unit out with a twin; a light tank made before them has none of it.
bool UpgradeTest()
{
    var w = new World(2, new[] { Civ.Coalition, Civ.Coalition });
    var p = w.Players[0];
    p.Metal = 100000;
    w.Step(new Command[] { new QueueUnit(0, Kind.LightTank), new QueueUnit(1, Kind.LightTank), new SetRepeat(0, false), new SetRepeat(1, false) });
    Entity plain = null;
    for (int t = 0; t < 400 && plain == null; t++) { w.Step(Array.Empty<Command>()); plain = w.Entities.Find(e => e.Kind == Kind.LightTank && e.Owner == 0); }
    w.HqOf(1).Damage = 0;   // (the tanks drive off to the enemy HQ: it mustn't shoot them before they're counted)
    var plan = new[] { Tech.Lobber, Tech.Gunnery1, Tech.Mender, Tech.Bulwark, Tech.ShieldCoil, Tech.Tooling1, Tech.Tooling2, Tech.Twin };
    foreach (var t in plan)
    {
        while (p.Research.Count >= World.MaxResearchQueue) w.Step(Array.Empty<Command>());
        w.Step(new Command[] { new Research(0, t) });
    }
    for (int t = 0; t < 600 * World.TicksPerSecond && p.Research.Count > 0; t++) w.Step(Array.Empty<Command>());
    bool researched = plan.All(t => p.Has[(int)t]) && p.Mods[(int)Mod.Attack] == 1 && p.Mods[(int)Mod.Build] == 2;
    int before = w.Entities.Count(e => e.Kind == Kind.LightTank && e.Owner == 0);
    for (int i = 0; i < 6; i++) w.Step(new Command[] { new QueueUnit(0, Kind.LightTank) });
    int ticks = 0;
    while (ticks++ < 300 * World.TicksPerSecond && (p.Queue.Count > 0 || p.DoorMakes[0] >= 0)) w.Step(Array.Empty<Command>());
    var made = w.Entities.FindAll(e => e.Kind == Kind.LightTank && e.Owner == 0 && e.Id > plain.Id);
    bool ok = researched && made.Count >= 8 && made.All(u => u.Damage > plain.Damage && (u.Shield > 0 || u.Hp < u.MaxHp)) && plain.Shield == 0;
    Console.WriteLine($"Upgrade test: researched {researched}; 6 queued made {made.Count} (twins), damage {made.FirstOrDefault()?.Damage} vs {plain.Damage}, shield {made.Max(u => (int?)u.Shield)} vs {plain.Shield}: {(ok ? "OK" : "FAILED")}");
    return ok;
}
