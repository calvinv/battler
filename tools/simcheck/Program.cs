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
    for (int i = 0; i < 5; i++) w.Step(new Command[] { new AddWing(0, hq0.Id, i % 2 == 0 ? Kind.Tank : Kind.LightTank) });
    foreach (var f in w.Entities.FindAll(e => e.Kind == Kind.Factory)) { f.Building = 0; f.Timer = 1000000; }
    var tanks = new List<Entity>();
    for (int i = 0; i < n; i++)   // round the base, among the wings, as they'd come out of them
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
Console.WriteLine($"Map: {Map.Name}");
if (!BaseTest() || !ModuleTest()) return 1;
return args.Contains("lan") ? LanTest() : ReplayTest();

// Bases and wings: a utility wing on the HQ orders a mobile base, which drives out and sets up; an extractor can then
// go on the deposit there, and a wing on the new base. When that base dies its wing goes with it. Then the radar: an
// enemy tank hidden in the fog near the HQ shows on the next sweep, and becomes known.
bool BaseTest()
{
    var w = new World(1, new[] { Civ.Coalition, Civ.Coalition });
    foreach (var p in w.Players) p.Metal = 100000;
    var hq0 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
    var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
    w.Step(new Command[] { new AddWing(0, hq0.Id, Kind.Tank), new AddWing(1, hq1.Id, Kind.Tank) });
    w.Step(new Command[] { new AddWing(0, hq0.Id, Kind.MobileBase) });
    var util = w.Entities.Find(e => World.IsUtility(e) && e.Owner == 0);
    while (util.Building > 0) w.Step(Array.Empty<Command>());
    w.Step(new Command[] { new Order(0, util.Id, Kind.MobileBase) });
    Entity truck = null;
    for (int t = 0; t < 1000 && truck == null; t++) { w.Step(Array.Empty<Command>()); truck = w.Entities.Find(e => e.Kind == Kind.MobileBase); }
    // The nearest metal deposit out from home on player 0's side (guarded, as every one away from home is).
    int metalAt = -1;
    for (int d = 0; d < World.Deposits.Length; d++)
    {
        var dist = (World.Deposits[d] - hq0.Pos).Length.ToInt();
        if (World.DepositResource[d] != Resource.Metal || dist < 40 || World.Deposits[d].X.Raw > 0) continue;
        if (metalAt < 0 || dist < (World.Deposits[metalAt] - hq0.Pos).Length.ToInt()) metalAt = d;
    }
    var spot = World.Deposits[metalAt] + (World.Deposits[metalAt] - hq0.Pos).WithLength(Fix.FromInt(8));   // (just past it, clear of the HQ)
    w.Step(new Command[] { new MoveUnits(0, new[] { truck.Id }, spot) });
    for (int t = 0; t < 4000 && truck.HasWaypoint; t++) w.Step(Array.Empty<Command>());
    bool guarded = !World.FeralsOn || (!w.CanDeploy(0, truck.Id, out _) && w.GuardNear(World.Deposits[metalAt]));   // (no guards with ferals off)
    foreach (var g in w.Entities) if (g.Owner == World.Feral && (g.Pos - World.Deposits[metalAt]).LengthSq < Fix.FromInt(30 * 30)) g.Hp = 0;   // cleared
    w.Step(Array.Empty<Command>());
    bool canDeploy = w.CanDeploy(0, truck.Id, out var why);
    w.Step(new Command[] { new Deploy(0, truck.Id) });
    var post = w.Entities.Find(e => e.Kind == Kind.Outpost && e.Owner == 0);
    while (post != null && post.Building > 0) w.Step(Array.Empty<Command>());
    bool canMine = w.CanPlaceExtractor(0, metalAt, out _);
    // Wings go on the extractor there (and the set-up base starts lines too); when the extractor falls its wing goes with it.
    w.Step(new Command[] { new PlaceExtractor(0, metalAt) });
    var rig = w.Entities.Find(e => e.Kind == Kind.Extractor && e.Owner == 0 && e.Deposit == metalAt);
    if (rig != null) rig.Building = 0;
    bool baseTakesNone = post != null && w.CanAddWing(0, post.Id, Kind.LightTank, out _);   // (now: the base takes them)
    w.Step(new Command[] { new AddWing(0, rig?.Id ?? 0, Kind.LightTank) });
    int wingsThere = rig == null || !baseTakesNone ? -1 : w.SlotsUsed(rig);
    if (rig != null) rig.Hp = 0;
    w.Step(Array.Empty<Command>());
    w.Step(Array.Empty<Command>());
    int wingsLeft = w.Entities.Count(e => e.Kind == Kind.Factory && e.Owner == 0);

    // Radar: an enemy tank 60 out from the HQ, where nothing of ours sees it.
    while (w.Tick % World.RadarTicks != World.RadarTicks - 1) w.Step(Array.Empty<Command>());   // (just before a sweep)
    var sneak = w.Spawn(Kind.Tank, 1, hq0.Pos + new FixVec2(-10, -55));
    w.Step(Array.Empty<Command>());
    bool blip = w.Players[0].Blips.Count > 0 && w.Players[0].Known.ContainsKey(sneak.Id);
    bool ok = truck != null && guarded && canDeploy && post != null && canMine && baseTakesNone && wingsThere == 1 && wingsLeft == 2 && blip;
    Console.WriteLine($"Base test: truck {(truck != null)}, guarded till cleared {guarded}, could set up {canDeploy} ({why}), set up {post != null}, extractor in reach {canMine}, wings there {wingsThere}, wings left after it fell {wingsLeft}/2; radar blip {blip}: {(ok ? "OK" : "FAILED")}");
    return ok && (!World.FeralsOn || WaveTest()) && PowerTest() && HostTest();
}

// Resource hosts: a tank wing on a sulfur extractor makes enhanced tanks (harder hits, fires); one on a power plant
// draws half the power.
bool HostTest()
{
    var w = new World(5, new[] { Civ.Coalition, Civ.Coalition });
    foreach (var p in w.Players) p.Metal = 100000;
    var hq = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
    var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
    w.Step(new Command[] { new AddWing(0, hq.Id, Kind.LightTank), new AddWing(1, hq1.Id, Kind.LightTank) });
    int sulfurAt = Array.FindIndex(World.DepositResource, r => r == Resource.Sulfur);
    var pit = w.Spawn(Kind.Extractor, 0, World.Deposits[sulfurAt]);
    pit.Deposit = sulfurAt; pit.Building = 0;
    var plant = w.Spawn(Kind.PowerPlant, 0, World.Vents[0]);
    plant.Vent = 0; plant.Building = 0;
    w.Step(new Command[] { new AddWing(0, pit.Id, Kind.Tank), new AddWing(0, plant.Id, Kind.Artillery) });
    var onSulfur = w.Entities.Find(e => e.Kind == Kind.Factory && e.BaseId == pit.Id);
    var onPlant = w.Entities.Find(e => e.Kind == Kind.Factory && e.BaseId == plant.Id);
    foreach (var f in w.Entities.FindAll(e => e.Kind == Kind.Factory)) f.Building = 0;
    Entity tank = null;
    for (int t = 0; t < 1200 && tank == null; t++) { w.Step(Array.Empty<Command>()); tank = w.Entities.Find(e => e.Kind == Kind.Tank && e.FactoryId == onSulfur?.Id); }
    bool ok = onSulfur != null && onPlant != null && tank != null && tank.Enhanced && w.WingDraw(onPlant) == World.PowerDraw(Kind.Artillery) / 2;
    Console.WriteLine($"Host test: sulfur wing {(onSulfur != null)}, its tank enhanced {tank?.Enhanced}, plant wing draws {(onPlant == null ? -1 : w.WingDraw(onPlant))} of {World.PowerDraw(Kind.Artillery)}: {(ok ? "OK" : "FAILED")}");
    return ok;
}

// Power: three tank wings (drawing 6) on the HQ's 4 make their first tank in half as long again (45 s becomes
// 67.5 s); with a power plant up as well, in the usual 45 s.
bool PowerTest()
{
    int FirstTank(bool plant)
    {
        var w = new World(4, new[] { Civ.Coalition, Civ.Coalition });
        foreach (var p in w.Players) p.Metal = 100000;
        var hq = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
        var hq1 = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1);
        w.Step(new Command[] { new AddWing(0, hq.Id, Kind.Tank), new AddWing(0, hq.Id, Kind.Tank), new AddWing(0, hq.Id, Kind.Tank), new AddWing(1, hq1.Id, Kind.LightTank) });
        if (plant)
        {
            int v = Array.FindIndex(World.Vents, at => w.BaseNear(0, at, World.BaseReach) != null);
            w.Spawn(Kind.PowerPlant, 0, World.Vents[v]).Vent = v;
            w.Entities.Find(e => e.Kind == Kind.PowerPlant).Building = 0;
        }
        foreach (var f in w.Entities.FindAll(e => e.Kind == Kind.Factory)) f.Building = 0;
        int start = w.Tick;
        while (!w.Entities.Exists(e => e.Kind == Kind.Tank) && w.Tick - start < 2000) w.Step(Array.Empty<Command>());
        return w.Tick - start;
    }
    int shortOf = FirstTank(false), powered = FirstTank(true);
    bool ok = Math.Abs(shortOf - 675) <= 5 && Math.Abs(powered - 450) <= 5;
    Console.WriteLine($"Power test: first tank with 6 drawn on 4 supplied after {shortOf / 10.0:0.0} s, with a plant {powered / 10.0:0.0} s: {(ok ? "OK" : "FAILED")}");
    return ok;
}

// The first feral wave comes in from both edges a minute after the start.
bool WaveTest()
{
    var w = new World(3, new[] { Civ.Coalition, Civ.Coalition });
    var hqs = w.Entities.FindAll(e => e.Kind == Kind.Hq);
    w.Step(new Command[] { new AddWing(0, hqs[0].Id, Kind.Tank), new AddWing(1, hqs[1].Id, Kind.Tank) });
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
        // Player 0 holds its first factory's units near home for a while, then lets them go.
        var first0 = world.Entities.Find(e => e.Kind == Kind.Factory && e.Owner == 0);
        if (script && first0 != null && world.Tick == 300) cmds.Add(new SetRally(0, first0.Id, false, new FixVec2(-30, 5)));
        // ...and sets a base rally at its HQ for a while (scouts and unrallied factories go there).
        if (script && world.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0) is { } hq0 && (world.Tick == 400 || world.Tick == 1800))
            cmds.Add(new SetRally(0, hq0.Id, world.Tick == 1800, new FixVec2(-45, -8)));
        if (script && first0 != null && world.Tick == 1500) cmds.Add(new SetRally(0, first0.Id, true, FixVec2.Zero));
        // ...and later sells its newest factory or outpost.
        // It snaps modules onto its HQ's lines (more than it can pay for: the rest are refused), then a wing after them.
        if (script && (world.Tick == 1000 || world.Tick == 1100) && world.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0) is { } mh0)
            foreach (var m in new[] { Mod.Attack, Mod.Shield, Mod.Twin, Mod.Speed }) cmds.Add(new AddModule(0, mh0.Id, world.Tick == 1000 ? 1 : 2, m));
        if (script && world.Tick == 1050 && world.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0) is { } wh0) cmds.Add(new AddWingAt(0, wh0.Id, 1, Kind.LightTank));
        if (script && world.Tick == 2000 && world.Entities.FindLast(e => e.Kind == Kind.Factory && e.Owner == 0) is { } sold)
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
                if (i == 1 && (w.Tick == 400 || w.Tick == 1800) && w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1) is { } hq1)
                    ss.Issue(new SetRally(1, hq1.Id, w.Tick == 1800, new FixVec2(45, 8)));
                if (i == 1 && first1 != null && w.Tick == 1500) ss.Issue(new SetRally(1, first1.Id, true, FixVec2.Zero));
                if (i == 1 && (w.Tick == 1000 || w.Tick == 1100) && w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1) is { } mh1)
                    foreach (var m in new[] { Mod.Armour, Mod.Attack, Mod.Build }) ss.Issue(new AddModule(1, mh1.Id, 1, m));
                if (i == 1 && w.Tick == 1050 && w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 1) is { } wh1) ss.Issue(new AddWingAt(1, wh1.Id, 1, Kind.Tank));
                if (i == 1 && w.Tick == 2000 && w.Entities.FindLast(e => e.Kind == Kind.Factory && e.Owner == 1) is { } sold) ss.Issue(new Sell(1, sold.Id));
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
    int m0 = 0, m1 = 0, f0 = 0, f1 = 0, o0 = 0, o1 = 0, x0 = 0, x1 = 0, s0 = 0, s1 = 0;
    foreach (var e in world.Entities)
    {
        if (e.Owner == World.Feral) continue;
        if (e.Stats.IsUnit) { if (e.Owner == 0) m0++; else m1++; }
        if (e.Kind == Kind.Factory) { if (e.Owner == 0) f0++; else f1++; }
        if (e.Kind == Kind.Outpost) { if (e.Owner == 0) o0++; else o1++; }
        if (e.Kind == Kind.Extractor) { if (e.Owner == 0) x0++; else x1++; }
        if (e.Kind == Kind.Scout) { if (e.Owner == 0) s0++; else s1++; }
    }
    var hq = world.Entities.FindAll(e => e.Kind == Kind.Hq);
    Console.WriteLine($"t={world.Tick / 10,4}s  wings {f0}v{f1}  bases {o0}v{o1}  sulfur {world.SulfurOf(0)}v{world.SulfurOf(1)}  extractors {x0}v{x1}  scouts {s0}v{s1}  metal {world.Players[0].Metal}/{world.Players[1].Metal}  units {m0}v{m1}  ferals {world.Entities.Count(e => e.Owner == World.Feral)} (wave {world.WaveNumber})  defences {world.Entities.Count(e => World.IsDefence(e.Kind) && e.Owner == 0)}v{world.Entities.Count(e => World.IsDefence(e.Kind) && e.Owner == 1)}  HQ hp {string.Join("/", hq.ConvertAll(h => h.Hp))}  top rank {MaxRank(world)}");
    string Front(int p) { var xs = world.Entities.FindAll(e => e.Owner == p && World.IsArmy(e.Kind)).ConvertAll(e => e.Pos.X.ToInt()); xs.Sort(); return xs.Count == 0 ? "-" : $"{xs[0]}..{xs[xs.Count / 2]}..{xs[^1]}"; }
    Console.WriteLine($"         power: {world.PowerDrawOf(0)}/{world.PowerSupply(0)} v {world.PowerDrawOf(1)}/{world.PowerSupply(1)} (draw/supply), plants {world.Entities.Count(e => e.Kind == Kind.PowerPlant && e.Owner == 0)}v{world.Entities.Count(e => e.Kind == Kind.PowerPlant && e.Owner == 1)}");
    Console.WriteLine($"         earned: mined {world.Players[0].Mined}/{world.Players[1].Mined}, salvage {world.Players[0].Salvaged}/{world.Players[1].Salvaged}");
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

// Modules feed the wings after them on their line (the user, 2026-10-23: "armoury upgrade, then whatever comes after
// has more firepower"): attack then a shield coil then a twin feed, a tank wing after them; a wing on another line gets
// none; the tanks come out hitting harder, shielded, and every third one with a twin.
bool ModuleTest()
{
    var w = new World(2, new[] { Civ.Coalition, Civ.Coalition });
    var hq = w.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == 0);
    w.Players[0].Metal = 5000;
    w.Step(new Command[] { new AddModule(0, hq.Id, 0, Mod.Attack), new AddModule(0, hq.Id, 0, Mod.Shield), new AddModule(0, hq.Id, 0, Mod.Twin) });
    w.Step(new Command[] { new AddWingAt(0, hq.Id, 0, Kind.Tank), new AddWingAt(0, hq.Id, 1, Kind.Tank) });
    var fed = w.Entities.Find(e => e.Kind == Kind.Factory && e.Slot == 0);
    var plain = w.Entities.Find(e => e.Kind == Kind.Factory && e.Slot == 1);
    w.Started = true;
    int ticks = 0;
    while (ticks++ < 260 * World.TicksPerSecond && w.Entities.Count(e => e.FactoryId == fed.Id) < 3) w.Step(Array.Empty<Command>());
    var made = w.Entities.FindAll(e => e.FactoryId == fed.Id);
    var other = w.Entities.Find(e => e.FactoryId == plain.Id);
    bool ok = fed.Link == 3 && fed.Mods[(int)Mod.Attack] == 1 && fed.Mods[(int)Mod.Twin] == 1 && plain.Mods.Sum() == 0
        && made.Count >= 3 && made.All(u => (u.Shield > 0 || u.Hp < u.MaxHp) && u.Damage > (other?.Damage ?? 0)) && other != null && other.Shield == 0;
    Console.WriteLine($"Module test: wing at place {fed.Link} with attack {fed.Mods[(int)Mod.Attack]} shield {fed.Mods[(int)Mod.Shield]} twin {fed.Mods[(int)Mod.Twin]}; " +
        $"{made.Count} made in {ticks / World.TicksPerSecond} s (damage {made.FirstOrDefault()?.Damage} vs {other?.Damage}, shield {made.Max(u => u.Shield)}): {(ok ? "OK" : "FAILED")}");
    return ok;
}
