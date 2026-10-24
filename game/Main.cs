using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Battler.Net;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// Runs a match: steps the session at a fixed rate, draws the world (interpolating between
/// ticks), and turns the player's clicks and keys into commands.
///
/// Controls: click your HQ (or B) for its card: units to queue, the tech tree (Tab), defences. E picks all scouts;
/// click, Shift-click or drag a box to pick scouts, right-click to send them. Right-click the ground with the HQ picked
/// to set its rally point. A click selects anything else to see its stats (Esc clears). WASD/arrows pan, wheel zooms,
/// middle-drag turns and tilts the view, V resets it, P calls a tactical pause, Enter ends your pause early, R restarts
/// once someone has won (vs AI).
///
/// Command line (after "--"): nothing = vs AI; --host = wait for a LAN opponent;
/// --join [ip] = join a host (default 127.0.0.1); --auto = an AI plays your side;
/// --shot = 60 s of AI vs AI, save screenshots/shot.png, quit (with --at N seconds, --wide for the whole map, --fight
/// to close on the busiest fight, --frames N to play on N frames first, --hq to pick my HQ with a rally set, --menu to
/// show the Escape menu, --napalm to light fires in view, --build to bring up the HQ's card, --tech for the tech tree,
/// --hover N to show card button N's popup).
/// </summary>
public partial class Main : Node3D
{
    const double TickLength = 1.0 / World.TicksPerSecond;

    static readonly Color[] TeamColor = { new(0.25f, 0.55f, 1f), new(1f, 0.42f, 0.18f), new(0.52f, 0.58f, 0.3f) };   // (the third: feral machines)
    static readonly Color Neutral = new(0.5f, 0.5f, 0.5f);   // (deposits: their model has no team paint)

    ISession _session;
    World W => _session.World;
    int _me;
    double _acc;
    bool _waiting;   // the last step was held up by the network

    // LAN setup, until the session exists.
    TcpListener _listener;
    Task<TcpTransport> _connecting;
    TcpTransport _net;
    string _joinAddress;
    string _connectStatus;

    readonly Dictionary<int, Node3D> _views = new();
    Kind? _placing;                // build mode: the defence a click places (Esc or a placement ends it)
    int _selected = -1;            // the entity whose stats are shown (click it; Esc clears)
    Overlay _overlay;
    PanelContainer _statsPanel;
    Label _stats;
    MeshInstance3D _selRing;
    readonly HashSet<int> _scouts = new();   // my scouts picked to be sent somewhere (click, Shift-click, drag a box, or E)
    Vector2? _dragFrom;                       // where a left-button drag began, on screen
    int _sellArmed = -1;          // the building a first click on Sell asked about
    ulong _sellArmedUntil;
    string _placeProblem;          // why the defence can't go under the mouse
    readonly List<(MeshInstance3D Node, float Age)> _blasts = new();   // artillery impacts, fading
    readonly Dictionary<int, Vector3> _prevPos = new();
    readonly Dictionary<int, Vector3> _facing = new();
    readonly Dictionary<int, (int Target, int Until)> _aim = new();   // per shooter: what it last fired at, and until which tick it keeps facing it
    Projectiles _proj;
    Explosions _booms;
    Tracks _tracks;
    Forest _forest;
    bool _watchFelled, _felledFocused;
    bool _placeAtCentre;   // --placing: aim at the screen centre, not the mouse
    readonly Dictionary<(Kind, int), Mesh> _meshes = new();
    readonly Dictionary<(Kind, int), Material> _materials = new();

    Camera3D _camera;
    Vector3 _focus;
    float _distance = 70;
    StandardMaterial3D _tracerMat;
    Label _hud;
    Label _banner;
    Vector3? _mouseGround;

    bool _auto;                // --auto: an AI issues my commands
    SimpleAi _autoMe;
    int _autoThoughtAt = -1;
    int _shotFrames = -1;
    int _checkTick = -1;       // --check N: at tick N print the checksum, save a screenshot, quit
    string _shotName = "shot";
    bool _frozen;
    MeshInstance3D _ring;   // build-range rings (BuildRing)

    public override void _Ready()
    {
        BuildScene();
        var args = OS.GetCmdlineUserArgs().ToList();
        _auto = args.Contains("--auto") || args.Contains("--shot");
        int check = args.IndexOf("--check");
        if (check >= 0 && check + 1 < args.Count) _checkTick = int.Parse(args[check + 1]);
        int join = args.IndexOf("--join");
        if (args.Contains("--host"))
        {
            _listener = TcpTransport.Listen();
            _connectStatus = $"Hosting on port {TcpTransport.DefaultPort}, waiting for an opponent...";
            DisplayServer.WindowSetTitle("Battler - host");
        }
        else if (join >= 0)
        {
            _joinAddress = join + 1 < args.Count && !args[join + 1].StartsWith("--") ? args[join + 1] : "127.0.0.1";
            _connecting = TcpTransport.ConnectAsync(_joinAddress);
            _connectStatus = $"Connecting to {_joinAddress}...";
            DisplayServer.WindowSetTitle("Battler - client");
        }
        else if (args.Contains("--lineup")) { BuildLineup(); return; }
        else if (args.Contains("--reel")) { BuildReel(); return; }
        else if (args.Contains("--base")) StartShowcase();
        else if (args.IndexOf("--skirmish") is int sk and >= 0) StartSkirmish(sk + 1 < args.Count && !args[sk + 1].StartsWith("-") ? args[sk + 1] : "tanks");
        else StartMatch(new LocalSession((ulong)GD.Randi()));

        if (args.Contains("--shot"))
        {
            int at = args.IndexOf("--at") is int ai and >= 0 && ai + 1 < args.Count ? int.Parse(args[ai + 1]) : 150;   // seconds in
            for (int i = 0; i < at * World.TicksPerSecond; i++)
            {
                if (args.Contains("--hq") && i == (at - 20) * World.TicksPerSecond && W.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == _me) is { } rh)
                    _session.Issue(new SetRally(_me, rh.Id, false, ToFix(ToV3(W.Players[_me].HqPos) + TowardEnemy(_me) * 16)));   // a base rally in front of the HQ
                StepWorld();
                FadeBlasts((float)TickLength);
                _proj.Advance((float)TickLength);
                _booms?.Advance((float)TickLength);
            }
            _focus = Vector3.Zero;
            _distance = 60;
            _selected = W.HqOf(_me)?.Id ?? -1;
            if (W.Find(_selected) is { } picked) { _focus = ToV3(picked.Pos) + TowardEnemy(_me) * 4; _distance = 34; }   // close on my HQ
            if (args.Contains("--wide")) { _focus = new Vector3(0, 0, 4); _distance = 400; _noFog = true; UpdateFog(); }   // the whole map, fog lifted
            if (args.Contains("--fight"))   // close on the busiest fight, fog lifted
            {
                var hot = W.Entities.Where(e => e.Stats.IsUnit)
                    .OrderByDescending(e => W.Entities.Count(o => o.Owner != e.Owner && (o.Pos - e.Pos).LengthSq < Fix.FromInt(15 * 15)))
                    .FirstOrDefault();
                if (hot != null) { _focus = ToV3(hot.Pos); _distance = 24; _noFog = true; UpdateFog(); _selected = -1; }
            }
            if (args.Contains("--hq") && W.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == _me) is { } myHq)   // my HQ picked: its panel
            { _selected = myHq.Id; _focus = ToV3(myHq.Pos) + new Vector3(6, 0, 4); _distance = 34; }
            if (args.Contains("--menu")) SetMenu(true);   // the Escape menu open
            if (args.Contains("--build")) OpenBuildCard();   // the HQ's build card up
            if (args.Contains("--tech")) SetTechTree(true);   // the tech tree up
            if (args.IndexOf("--hover") is int hv and >= 0 && hv + 1 < args.Count) _hover = int.Parse(args[hv + 1]);   // a card button's popup showing
            if (args.IndexOf("--view") is int vi and >= 0 && vi + 2 < args.Count)   // --view yawDegrees pitchDegrees: a turned view
            {
                _yaw = Mathf.DegToRad(float.Parse(args[vi + 1]));
                _pitch = float.Parse(args[vi + 2]);
                _viewHintUntil = Time.GetTicksMsec() + 20000;
            }
            if (args.Contains("--napalm") && W.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == _me) is { } burner)   // fires in view
            {
                _focus = ToV3(burner.Pos) + TowardEnemy(_me) * 30; _distance = 30; _selected = -1;
                for (int i = 0; i < 4; i++) W.Ignite(ToFix(_focus + new Vector3(i * 4 - 6, 0, (i % 2) * 3)), Fix.Ratio(20 + i * 5, 10), burner);
            }
            _watchFelled = args.Contains("--felled");      // close on the first tree knocked down, once it's down
            if (args.IndexOf("--look") is int li and >= 0 && li + 3 < args.Count)   // --look x z distance: the camera on a spot, fog lifted
            { _focus = new Vector3(float.Parse(args[li + 1]), 0, float.Parse(args[li + 2])); _distance = float.Parse(args[li + 3]); _noFog = true; UpdateFog(); _selected = -1; }
            // --frames N: play on N frames first (shells in flight, tread marks laid), then take it.
            _shotFrames = args.IndexOf("--frames") is int fi and >= 0 && fi + 1 < args.Count ? int.Parse(args[fi + 1]) : 10;
        }
    }

    // ---- --lineup: the unit models side by side in both teams' colours, to judge their outlines at game distance ----

    static readonly string[] LineupUnits = { "scout", "lighttank", "tank", "mortar", "artillery", "crawler", "spider", "piercer", "drone" };
    int _lineupFrame = -1;

    void BuildLineup()
    {
        int before = GetChildCount();
        BuildLineupModels();
        // Stood on the ground (the middle of the map is a hill now).
        for (int i = before; i < GetChildCount(); i++)
            if (GetChild(i) is Node3D n) n.Position += Vector3.Up * _terrain.HeightAt(n.Position.X, n.Position.Z);
    }

    void BuildLineupModels()
    {
        for (int team = 0; team < 2; team++)
            for (int i = 0; i < LineupUnits.Length; i++)
            {
                var unit = UnitArt.Make(LineupUnits[i], TeamColor[team]);
                // Emblems at a spread of levels, none to both gold.
                int[][] levels = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 3 }, new[] { 1, 1 }, new[] { 3, 3 }, new[] { 0, 0 }, new[] { 2, 0 }, new[] { 1, 2 }, new[] { 0, 0 } };
                UnitArt.AddEmblems(unit, LineupUnits[i], levels[i][0], levels[i][1]);
                AddChild(unit);
                unit.Position = new Vector3((i - 2) * 6, 0, team == 0 ? -3.5f : 3.5f);
                // Team 0 faces right (toward the enemy), team 1 left, both a little toward the camera.
                unit.Basis = Basis.LookingAt(new Vector3(team == 0 ? 1 : -1, 0, 0.35f).Normalized(), Vector3.Up);
            }
        // A row of tanks being built, a fifth, half and four fifths done (the digitising effect).
        for (int i = 0; i < 3; i++)
        {
            var ghost = UnitArt.MakeBuilding("tank", TeamColor[0]);
            AddChild(ghost);
            ghost.Position = new Vector3((i - 1) * 7, 0, 10);
            ghost.Basis = Basis.LookingAt(new Vector3(1, 0, 0.35f).Normalized(), Vector3.Up);
            UnitArt.SetBuilt(ghost, new[] { 0.2f, 0.5f, 0.8f }[i]);
        }
        _banner.Text = "";
        _focus = Vector3.Zero;
        _distance = 90;   // the game's starting zoom
        _lineupFrame = 0;
    }

    void StepLineup()
    {
        MoveCamera(0);
        _lineupFrame++;
        string name = _lineupFrame switch { 10 => "lineup_far", 20 => "lineup_near", 30 => "lineup_close", _ => null };
        if (name == null) return;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://screenshots"));
        GetViewport().GetTexture().GetImage().SavePng($"res://screenshots/{name}.png");
        if (_lineupFrame == 10) { _distance = 40; _focus = new Vector3(3, 0, 3); }
        // Then right up close on one unit (LINEUP_CLOSE: its index in LineupUnits, the light tank by default).
        else if (_lineupFrame == 20)
        {
            int i = int.TryParse(OS.GetEnvironment("LINEUP_CLOSE"), out var n) ? n : 1;
            _focus = new Vector3((i - 2) * 6, 0, -3.5f);
            _distance = 11;
        }
        else GetTree().Quit();
    }

    // ---- --reel: one rigged unit driving past a target, its turret tracking, stopping, firing and pulling away,
    // to judge its animation. Record it: godot --write-movie screenshots/reel/f.png --fixed-fps 30 --quit-after 300 -- --reel

    Node3D _reelUnit, _reelTarget;
    UnitRig _reelRig;
    float _reelT = -1, _reelSpeed, _reelHeading;
    Vector3 _reelPos;
    int _reelShots;
    bool _reelHovers;
    Kind _reelKind;
    static readonly float[] ReelFireAt = { 1.9f, 4.9f, 6.0f, 8.6f };

    void BuildReel()
    {
        _reelTarget = UnitArt.Make("tank", TeamColor[1]);
        AddChild(_reelTarget);
        _reelTarget.Position = new Vector3(0, _terrain.HeightAt(0, 0), 0);
        _reelTarget.Basis = Basis.LookingAt(new Vector3(-1, 0, 0.6f).Normalized(), Vector3.Up);
        string name = OS.GetEnvironment("REEL_UNIT") is { Length: > 0 } u ? u : "lighttank";
        _reelUnit = UnitArt.Make(name, TeamColor[0]);
        AddChild(_reelUnit);
        _reelRig = UnitRig.Find(_reelUnit, 1);
        if (name == "bulwark") _reelUnit.AddChild(Dome(TeamColor[0]));
        _reelHovers = name == "hovertank";
        _reelKind = name switch { "mech" => Kind.Ranger, "battlewheel" or "tank" => Kind.Tank, "longleg" => Kind.Artillery, "lobber" => Kind.Mortar, "skitter" => Kind.Scout, "dart" => Kind.Flier, "blimp" => Kind.Bomber, "mender" => Kind.Mender, "bulwark" => Kind.Bulwark, "hive" => Kind.Hive, _ => Kind.LightTank };
        _reelPos = new Vector3(-13, 0, 7);
        _reelHeading = 0;
        _banner.Text = "";
        _focus = _reelPos;
        _distance = float.TryParse(OS.GetEnvironment("REEL_DIST"), out var rd) ? rd : 24;
        _reelT = 0;
    }

    void StepReel(float dt)
    {
        float t = _reelT += dt;
        // Speed: pull away, cruise, brake to a stop, stand and shoot, pull away again turning.
        float want = t < 3.5f ? 3f : t < 6.6f ? 0f : 3f;
        float rate = want > _reelSpeed ? 5f : 4f;
        _reelSpeed = Mathf.MoveToward(_reelSpeed, want, rate * dt);
        if (t > 7.2f) _reelHeading += Mathf.DegToRad(28) * dt;
        var dir = new Vector3(Mathf.Cos(_reelHeading), 0, -Mathf.Sin(_reelHeading));
        float moved = _reelSpeed * dt;
        _reelPos += dir * moved;
        _reelPos.Y = _terrain.HeightAt(_reelPos.X, _reelPos.Z) + HeightOf(_reelKind);
        _reelUnit.Position = _reelPos;
        _reelUnit.Basis = Basis.LookingAt(dir, Vector3.Up);
        var toTarget = _reelTarget.Position - _reelPos;
        toTarget.Y = 0;
        float yaw = dir.SignedAngleTo(toTarget.Normalized(), Vector3.Up);
        _reelRig?.Update(dt, moved, yaw);
        float r = World.StatsFor[(int)Kind.LightTank].Radius.ToFloat();
        if (_reelSpeed > 0.05f && HeightOf(_reelKind) == 0)
        {
            _dust.Drive(1, _reelPos, dir, r * 1.1f, r * 0.7f, _reelSpeed, dt);
            if (!_reelHovers && _reelKind is not (Kind.Ranger or Kind.Artillery)) _tracks.Drive(1, _reelPos, dir, r * 0.55f, r * 0.3f, 0.7f);
        }
        if (_reelShots < ReelFireAt.Length && t >= ReelFireAt[_reelShots] && _reelKind is Kind.Ranger or Kind.Artillery && _reelSpeed > 0.1f) _reelShots++;   // (a walker stops to shoot)
        else if (_reelShots < ReelFireAt.Length && t >= ReelFireAt[_reelShots] && World.StatsFor[(int)_reelKind].Reload > 0)
        {
            _reelShots++;
            if (_reelKind is Kind.Mender or Kind.Hive) _reelRig?.SendDrone(_reelTarget.Position + Vector3.Up * 0.6f); else _reelRig?.Fire();
            var from = _reelRig?.Muzzle?.GlobalPosition ?? _reelPos + Vector3.Up;
            _proj.Fire(from, _reelTarget.Position + Vector3.Up * 0.6f, _reelKind, TeamColor[0]);
        }
        float follow = _distance < 18 ? 0.9f : 0.7f;   // (close up: keep the unit in the middle)
        _focus = _focus.Lerp(_reelPos * follow + _reelTarget.Position * (1 - follow), Mathf.Min(1, dt * 2));
    }

    public override void _ExitTree()
    {
        _listener?.Stop();
        UnitArt.Release();
        _terrain?.Dispose();
        _net?.Dispose();
    }

    /// <summary>Until the session exists: accept or finish the LAN connection, then start.</summary>
    void PollConnection()
    {
        if (_listener != null)
        {
            _net = TcpTransport.TryAccept(_listener);
            if (_net == null) return;
            _listener.Stop();
            _listener = null;
            var seed = (ulong)GD.Randi();
            _net.Send(Wire.Start(seed));
            StartMatch(new LockstepSession(_net, seed, 0));
        }
        else if (_connecting != null)
        {
            if (!_connecting.IsCompleted) return;
            if (_connecting.IsFaulted)
            {
                _connectStatus = $"Couldn't reach {_joinAddress}, retrying...";
                _connecting = TcpTransport.ConnectAsync(_joinAddress);
                return;
            }
            _net = _connecting.Result;
            _connecting = null;
        }
        else if (_net != null && _net.TryReceive(out var msg) && Wire.Type(msg) == Wire.MsgStart)
        {
            StartMatch(new LockstepSession(_net, Wire.ReadStart(msg), 1));
        }
    }

    void TakeShotWhenReady()
    {
        if (_checkTick > 0 && W.Tick >= _checkTick && _shotFrames < 0)
        {
            var problem = _session.Problem ?? "none";
            var verified = _session is LockstepSession ls ? ls.VerifiedTicks : 0;
            GD.Print($"CHECK player={_me} tick={W.Tick} sum={W.Checksum():X16} verified={verified} problem={problem}");
            _shotName = $"shot_p{_me}";
            _checkTick = -1;
            _shotFrames = 3;
            _frozen = true;   // hold this tick for the screenshot
        }
        if (_shotFrames < 0 || --_shotFrames > 0) return;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://screenshots"));
        GetViewport().GetTexture().GetImage().SavePng($"res://screenshots/{_shotName}.png");
        GD.Print($"trees knocked down: {_forest?.Knocked}");
        GetTree().Quit();
    }

    // ---- fog of war (the sim decides what each side sees: Entity.SeenBy; this draws it) -------------------------

    const int FogRes = 1;   // fog cells per world unit (one: the map is big, and the fog is redrawn every tick)
    static readonly int FogW = World.MapHalfX.ToInt() * 2 * FogRes, FogH = World.MapHalfZ.ToInt() * 2 * FogRes;
    bool _noFog;            // --wide: everything shown, to look at the landscape
    byte[] _fogData;        // what's seen, hard edged: for SeenAt
    byte[] _fogSoft;        // the same blurred a little: what the ground shows
    Image _fogImage;
    ImageTexture _fogTexture;
    readonly HashSet<int> _seenBuildings = new();   // enemy buildings stay on the map once spotted

    /// <summary>Repaints the fog texture from my side's vision circles.</summary>
    void UpdateFog()
    {
        System.Array.Clear(_fogData);
        if (_noFog) System.Array.Fill(_fogData, (byte)255);
        foreach (var e in W.Entities)
        {
            if (e.Owner != _me) continue;
            float r = (e.Stats.Vision.ToFloat() + e.Stats.Radius.ToFloat() + 0.5f) * FogRes;
            var c = ToV3(e.Pos) * FogRes;
            int x0 = Mathf.Max(0, (int)(c.X - r + FogW / 2)), x1 = Mathf.Min(FogW - 1, (int)(c.X + r + FogW / 2));
            int z0 = Mathf.Max(0, (int)(c.Z - r + FogH / 2)), z1 = Mathf.Min(FogH - 1, (int)(c.Z + r + FogH / 2));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - FogW / 2 - c.X, dz = z + 0.5f - FogH / 2 - c.Z;
                    if (dx * dx + dz * dz <= r * r) _fogData[z * FogW + x] = 255;
                }
        }
        // A 5x5 box blur softens the stair-stepped edge of the cells into a short fade.
        for (int z = 0; z < FogH; z++)
            for (int x = 0; x < FogW; x++)
            {
                int sum = 0, n = 0;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= FogW || zz >= FogH) continue;
                        sum += _fogData[zz * FogW + xx];
                        n++;
                    }
                _fogSoft[z * FogW + x] = (byte)(sum / n);
            }
        _fogImage.SetData(FogW, FogH, false, Image.Format.L8, _fogSoft);
        _fogTexture.Update(_fogImage);
    }

    /// <summary>Whether my side sees a place on the ground (for tracers and blasts).</summary>
    bool SeenAt(Vector3 p)
    {
        int x = (int)(p.X * FogRes + FogW / 2), z = (int)(p.Z * FogRes + FogH / 2);
        return x >= 0 && z >= 0 && x < FogW && z < FogH && _fogData[z * FogW + x] > 0;
    }

    /// <summary>Whether to draw something: mine; anything my side sees now; enemy buildings once spotted; and the
    /// the enemy HQ, whose place everyone knows.</summary>
    bool Shown(Entity e)
    {
        if (e.Owner == _me || e.Kind is Kind.Hq or Kind.Tower || _noFog) return true;   // (_noFog: a debug view, everything shown)
        if (World.CanSee(_me, e)) { if (e.Stats.IsBuilding) _seenBuildings.Add(e.Id); return true; }
        return _seenBuildings.Contains(e.Id);
    }

    void StartMatch(ISession session)
    {
        foreach (var v in _views.Values) v.QueueFree();
        foreach (var id in _pyramids.Keys.ToList()) ForgetPyramid(id);
        _buildingRigs.Clear(); _rigs.Clear();
        _views.Clear(); _prevPos.Clear(); _facing.Clear(); _aim.Clear(); _seenBuildings.Clear(); _rising.Clear(); _rollOut.Clear();
        _proj?.Clear(); _booms?.Clear(); _dust?.Clear(); _tracks?.Clear(); _forest?.Reset(); _salvageTexts.Clear();
        ClearFieldViews();
        _session = session;
        _me = session.LocalPlayer;
        _acc = 0;
        _autoThoughtAt = -1;
        _focus = ToV3(W.Players[_me].HqPos) + TowardEnemy(_me) * 20;
        if (_auto) _autoMe = new SimpleAi(_me, (ulong)GD.Randi());
        BuildRing();
        UpdateFog();
    }

    /// <summary>The base on show (--base): my pyramid at a tier (BASE_TIER, 3 by default) with research done
    /// (BASE_TECH: "all", "none", or names like Wheel,Dart; by default one step down every branch) and a few units
    /// queued so its doors open in turn (BASE_MAKES=LightTank,Ranger,Tank); no opponent moving.</summary>
    void StartShowcase()
    {
        StartMatch(new SkirmishSession((ulong)GD.Randi(), System.Array.Empty<(Kind, int)>()));
        var hq = W.HqOf(0);
        if (hq == null) return;
        var p = W.Players[0];
        p.Metal = 100000;
        p.Mined = World.CollectedFor(int.TryParse(OS.GetEnvironment("BASE_TIER"), out var bt) ? bt : 3);
        var techs = OS.GetEnvironment("BASE_TECH") switch
        {
            "all" => Techs.All.Select(t => t.Id),
            "none" => Enumerable.Empty<Tech>(),
            { Length: > 0 } list => list.Split(',').Select(n => System.Enum.Parse<Tech>(n.Trim(), true)),
            _ => Techs.All.Where(t => t.Row == 0 && t.Lane == 0).Select(t => t.Id),
        };
        foreach (var t in techs) { p.Has[(int)t] = true; if (Techs.Of(t).Gives is { } m) p.Mods[(int)m]++; }
        var makes = OS.GetEnvironment("BASE_MAKES") is { Length: > 0 } names
            ? names.Split(',').Select(n => System.Enum.Parse<Kind>(n.Trim(), true)).ToArray()
            : new[] { Kind.LightTank, Kind.Ranger, Kind.Tank };
        foreach (var k in makes) p.Queue.Add((int)k);
        p.Repeat = false;
        W.Players[1].Ordered = true;
        _noFog = true;
        UpdateFog();
        _focus = ToV3(hq.Pos) + TowardEnemy(0) * 3;
        _distance = float.TryParse(OS.GetEnvironment("BASE_DIST"), out var bd) ? bd : 34;
        if (float.TryParse(OS.GetEnvironment("BASE_YAW"), out var turn)) _yaw = Mathf.DegToRad(turn);   // (a turned view)
    }

    void StartSkirmish(string lineup = "tanks")
    {
        var groups = lineup switch
        {
            "mechs" => new[] { (Kind.Ranger, 5) },
            // Every unit, front to back: the fighters, the shield and swarm among them, menders behind, the guns at
            // the back, fliers and the scout last.
            "mixed" => new[] { (Kind.Tank, 3), (Kind.LightTank, 4), (Kind.Ranger, 4), (Kind.Bulwark, 1), (Kind.Hive, 2), (Kind.Mender, 2),
                               (Kind.Mortar, 2), (Kind.Artillery, 2), (Kind.Flier, 2), (Kind.Bomber, 1), (Kind.Scout, 1) },
            "wheels" => new[] { (Kind.Tank, 5) },
            _ => new[] { (Kind.LightTank, 5) },
        };
        StartMatch(new SkirmishSession((ulong)GD.Randi(), groups, rows: lineup == "mixed" ? 4 : 5));
        _noFog = true;
        UpdateFog();
        _focus = Vector3.Zero;
        _distance = 42;
    }

    /// <summary>Rings on the ground (redrawn each frame, following it): placing a defence, how far round the HQ it can
    /// go and how far its gun would reach; round picked scouts and where they're going; the radar sweep.</summary>
    void BuildRing()
    {
        if (_ring.Mesh is not ImmediateMesh ring) { ring = new ImmediateMesh(); _ring.Mesh = ring; _ringMat = Unshaded(new Color(TeamColor[_me], 0.5f)); }
        ring.ClearSurfaces();
        void Circle(FixVec2 at, float r)
        {
            ring.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _ringMat);
            var c = ToV3(at);
            for (int i = 0; i <= 64; i++)
            {
                var a = Mathf.Tau * i / 64;
                var p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                ring.SurfaceAddVertex(new Vector3(p.X, _terrain.HeightAt(p.X, p.Z) + 0.08f, p.Z));
            }
            ring.SurfaceEnd();
        }
        foreach (var e in W.Entities)
        {
            if (_placing != null && e.Owner == _me && World.IsBase(e)) Circle(e.Pos, World.BaseReach.ToFloat());
            if (e.Owner != _me || !World.IsSteered(e.Kind)) continue;
            // A picked scout: a ring round it, and where it's going.
            if (_scouts.Contains(e.Id))
            {
                Circle(e.Pos, 1.3f);
                if (e.HasWaypoint) { Circle(e.Waypoint, 0.5f); Line(e.Pos, e.Waypoint); }
            }
        }
        SweepRings(Circle);   // the radar, just after it pings
        // Placing a gun: how far it will reach from where it would stand.
        if (_placing is { } gun && World.StatsFor[(int)gun].Range.Raw > 0 && _mouseGround is { } mg)
            Circle(ToFix(mg), World.StatsFor[(int)gun].Range.ToFloat());
        void Line(FixVec2 from, FixVec2 to)
        {
            ring.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _ringMat);
            var a = ToV3(from); var b = ToV3(to);
            for (int i = 0; i <= 16; i++)
            {
                var p = a.Lerp(b, i / 16f);
                ring.SurfaceAddVertex(new Vector3(p.X, _terrain.HeightAt(p.X, p.Z) + 0.15f, p.Z));
            }
            ring.SurfaceEnd();
        }
    }

    StandardMaterial3D _ringMat;

    // ---- scene -------------------------------------------------------------------------------

    void BuildScene()
    {
        // Lit like a clear, slightly hazy day (the user, 2026-10-20: "higher definition battlefields"): light from a sky
        // rather than one flat colour, filmic tone mapping, the lit strips blooming, contact shadows in creases (SSAO),
        // a little haze far off, and smooth edges (MSAA).
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.32f, 0.45f, 0.62f), SkyHorizonColor = new Color(0.66f, 0.7f, 0.72f),
                    GroundBottomColor = new Color(0.5f, 0.52f, 0.52f), GroundHorizonColor = new Color(0.62f, 0.65f, 0.67f),   // (haze past the map's edge)
                    SunAngleMax = 20f,
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.55f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.05f,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowIntensity = 0.7f,
            GlowBloom = 0.03f,
            GlowHdrThreshold = 0.9f,
            SsaoEnabled = true,
            SsaoRadius = 1.6f,
            SsaoIntensity = 2.2f,
            SsaoPower = 1.4f,
            FogEnabled = true,
            FogLightColor = new Color(0.62f, 0.66f, 0.7f),
            FogDensity = 0.0007f,
            FogSkyAffect = 0f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.08f,
            AdjustmentContrast = 1.05f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;

        // The house light (ai-tools docs/render_scene.md): sun from the upper left of the screen,
        // a little toward the viewer, 50 degrees up; a cool fill from the right.
        var sunFrom = new Vector3(-Mathf.Cos(Mathf.DegToRad(50)), Mathf.Sin(Mathf.DegToRad(50)), 0.35f).Normalized();
        AddChild(new DirectionalLight3D
        {
            Transform = new Transform3D(Basis.LookingAt(-sunFrom, Vector3.Up), Vector3.Zero),
            LightEnergy = 1.6f,
            LightColor = new Color(1f, 0.95f, 0.86f),   // a warm sun against the cool sky
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 420f,        // the whole view at the widest zoom
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            // Crisp shadow edges, smoothed by filtering only: a sun with size (angular distance) samples its soft
            // edges with noise, which speckled the flat cel shaded hover tank (the user, 2026-10-23: "dark spots").
            ShadowBlur = 1.0f,
            ShadowNormalBias = 1.5f,
        });
        var fillFrom = new Vector3(1, 0.4f, 0.2f).Normalized();
        AddChild(new DirectionalLight3D
        {
            Transform = new Transform3D(Basis.LookingAt(-fillFrom, Vector3.Up), Vector3.Zero),
            LightColor = new Color(0.6f, 0.75f, 1f),
            LightEnergy = 0.3f,
        });

        // Dusty wasteland hills (olive machines stand out on it), darkened where we can't see.
        _terrain = new Terrain(World.MapHalfX.ToFloat(), World.MapHalfZ.ToFloat());
        _fogData = new byte[FogW * FogH];
        _fogSoft = new byte[FogW * FogH];
        System.Array.Fill(_fogData, (byte)255);
        System.Array.Fill(_fogSoft, (byte)255);
        _fogImage = Image.CreateFromData(FogW, FogH, false, Image.Format.L8, _fogData);
        _fogTexture = ImageTexture.CreateFromImage(_fogImage);
        var groundMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/ground_fog.gdshader") };
        groundMat.SetShaderParameter("fog", _fogTexture);
        groundMat.SetShaderParameter("half_size", new Vector2(World.MapHalfX.ToFloat(), World.MapHalfZ.ToFloat()));
        // The ground's noise, made once: seamless fractal value noise, 32 px a unit of the shader's p (it tiles every 16).
        groundMat.SetShaderParameter("noise_tex", new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Value, Frequency = 1f / 32f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5, Seed = 7 },
        });
        foreach (var soil in new[] { "dirt", "mud", "gravel", "scorched" })
            groundMat.SetShaderParameter(soil + "_tex", GD.Load<Texture2D>($"res://art/ground/{soil}.png"));
        AddChild(_terrain.BuildGround(groundMat));
        var waterMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/water_fog.gdshader") };
        waterMat.SetShaderParameter("fog", _fogTexture);
        waterMat.SetShaderParameter("half_size", new Vector2(World.MapHalfX.ToFloat(), World.MapHalfZ.ToFloat()));
        AddChild(_terrain.BuildDecor(waterMat));
        _forest = new Forest();
        AddChild(_forest);
        if (OS.GetEnvironment("NOTREES") != "1") _forest.Plant(_terrain.TreeSpots);   // (NOTREES=1: none, to measure their cost)
        // (No deposits or vents on show: resources come from battle for now; the user, 2026-10-24: "later Im thinking we
        // have a commander unit that can go collect resources".)

        _ring = new MeshInstance3D();
        AddChild(_ring);

        _tracerMat = Unshaded(Colors.White);
        _tracerMat.VertexColorUseAsAlbedo = true;
        _tracks = new Tracks { HeightAt = _terrain.SurfaceAt };
        AddChild(_tracks);
        _dust = new Dust();
        AddChild(_dust);
        _booms = new Explosions { Dust = _dust, GroundAt = (x, z) => _terrain.HeightAt(x, z) };
        AddChild(_booms);
        _proj = new Projectiles { Dust = _dust, Booms = _booms };
        _proj.OnShellLands = (at, by) => { if (SeenAt(at)) Blast(at, World.StatsFor[(int)by].Splash.ToFloat()); };
        AddChild(_proj);
        _rallyLine = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _rallyLine });

        _camera = new Camera3D { Fov = 40, Far = 1500, Near = 0.3f };
        AddChild(_camera);

        _selRing = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 32, RingSegments = 4 },
            MaterialOverride = Unshaded(new Color(1f, 1f, 1f, 0.85f)),
            Visible = false,
        };
        AddChild(_selRing);

        var ui = new CanvasLayer();
        AddChild(ui);
        _overlay = new Overlay { AnchorRight = 1, AnchorBottom = 1 };
        ui.AddChild(_overlay);
        _statsPanel = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 16, OffsetTop = -230, Visible = false };
        _statsPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.07f, 0.85f), BorderColor = new Color(0.5f, 0.45f, 0.35f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        _stats = new Label();
        _stats.AddThemeFontSizeOverride("font_size", 18);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(_stats);
        _statsPanel.AddChild(column);
        ui.AddChild(_statsPanel);
        _hud = new Label { Position = new Vector2(16, 12) };
        _hud.AddThemeFontSizeOverride("font_size", 20);
        ui.AddChild(_hud);
        _banner = new Label
        {
            AnchorLeft = 0, AnchorRight = 1, OffsetTop = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _banner.AddThemeFontSizeOverride("font_size", 32);
        ui.AddChild(_banner);
        _viewHint = new Label
        {
            Text = "[V] Reset the view", AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -44,
            HorizontalAlignment = HorizontalAlignment.Center, Visible = false, Modulate = new Color(1, 1, 1, 0.85f),
        };
        _viewHint.AddThemeFontSizeOverride("font_size", 20);
        _viewHint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _viewHint.AddThemeConstantOverride("outline_size", 6);
        ui.AddChild(_viewHint);
        BuildMenu(ui);
        BuildQueueBar(ui);
        BuildCommandCard(ui);
        BuildTechTree(ui);
    }

    static StandardMaterial3D Unshaded(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = c.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };

    Mesh MeshFor(Kind k)
    {
        if (_meshes.TryGetValue((k, 0), out var m)) return m;
        var r = World.StatsFor[(int)k].Radius.ToFloat();
        m = k switch
        {
            Kind.Hq => new BoxMesh { Size = new Vector3(r * 1.6f, 3, r * 1.6f) },
            Kind.Tower => new CylinderMesh { TopRadius = r * 0.55f, BottomRadius = r * 0.9f, Height = 4.5f, RadialSegments = 8 },
            _ => new BoxMesh { Size = new Vector3(r * 1.6f, 1.6f, r * 1.2f) },
        };
        _meshes[(k, 0)] = m;
        return m;
    }

    Material MaterialFor(Kind k, int owner)
    {
        if (_materials.TryGetValue((k, owner), out var m)) return m;
        m = new StandardMaterial3D { AlbedoColor = TeamColor[owner].Darkened(0.35f), Roughness = 0.8f };
        _materials[(k, owner)] = m;
        return m;
    }

    /// <summary>The flat direction from a player's HQ to the other's: which way its buildings face, and so on.</summary>
    static Vector3 TowardEnemy(int player)
    {
        if (player >= 2) return Vector3.Forward;   // (ferals have no side to face)
        var d = World.HqStart(1 - player) - World.HqStart(player);
        return new Vector3(d.X.ToFloat(), 0, d.Z.ToFloat()).Normalized();
    }
    static string ModelOf(Kind k) => k.ToString().ToLowerInvariant();   // art/units/<name>.glb

    /// <summary>A unit's model for a civilisation.</summary>
    static string UnitModel(Kind k, Civ civ) => k == Kind.LightTank ? "hovertank"      // (the cel shaded hover tank, being explored: the user, 2026-10-23)
        : k == Kind.Ranger ? "mech"                                                            // (and the bird-legged rifle mech)
        : k == Kind.Tank ? "battlewheel"                                                       // (and the two-wheeled cannon)
        : k == Kind.Artillery ? "longleg"                                                      // (and the tripod gun)
        : k == Kind.Mortar ? "lobber"                                                          // (and the wheeled mortar)
        : k == Kind.Scout ? "skitter"                                                          // (and the ball scout)
        : k == Kind.Flier ? "dart"                                                             // (and the prop interceptor)
        : k == Kind.Bomber ? "blimp"                                                           // (and the armoured blimp)
        : k == Kind.Mender ? "mender"                                                          // (the repair carrier)
        : k == Kind.Bulwark ? "bulwark"                                                        // (the shield walker)
        : k == Kind.Hive ? "hive"                                                              // (the swarm carrier)
        : civ == Civ.Lattice && k is Kind.Ranger or Kind.Flier or Kind.Bomber ? "l_" + ModelOf(k) : ModelOf(k);

    /// <summary>A building's model: the HQ by its side's tier (Pyramid.cs); the Lattice's towers and the like their own
    /// ("l_tower", tools/art/model_lattice.py).</summary>
    string BuildingModel(Kind kind, int owner) =>
        kind == Kind.Hq ? $"hq_t{(owner < W.Players.Length ? W.Players[owner].Tier : 1)}"
        : World.IsFeral(kind) ? ModelOf(kind) : (CivOf(owner) == Civ.Lattice ? "l_" : "") + ModelOf(kind);

    Civ MyCiv => W.Players[_me].Civ;
    /// <summary>A side's civilisation (the ferals' models aren't any civilisation's: they're named for themselves).</summary>
    Civ CivOf(int owner) => owner < W.Players.Length ? W.Players[owner].Civ : Civ.Coalition;

    /// <summary>An upgrade as the player reads it.</summary>
    static string ModName(Mod m) => m switch
    {
        Mod.Build => "Tooling", Mod.Twin => "Twin feed", Mod.Shield => "Shield coil", Mod.Magnet => "Salvage magnet", Mod.Attack => "Gunnery",
        Mod.Armour => "Plating", Mod.Speed => "Servos", _ => m.ToString(),
    };

    static string A(string thing) => ("aeiou".Contains(thing[0]) ? "an " : "a ") + thing;

    /// <summary>A unit's upgrades, as "gunnery 2, shield coil".</summary>
    static string ModList(int[] mods)
    {
        var parts = new List<string>();
        for (int i = 0; i < World.ModCount; i++)
            if (mods[i] > 0) parts.Add(ModName((Mod)i).ToLowerInvariant() + (mods[i] > 1 ? " " + mods[i] : ""));
        return string.Join(", ", parts);
    }

    static Shader _domeShader;

    /// <summary>A bulwark's dome: a faint bubble of light the size of what it covers, brightest at its rim, with
    /// slow bands drifting up it.</summary>
    static MeshInstance3D Dome(Color team)
    {
        _domeShader ??= new Shader { Code = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never;
uniform vec4 tint : source_color;
void fragment() {
    float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    float band = 0.5 + 0.5 * sin(UV.y * 40.0 - TIME * 2.0);
    ALBEDO = tint.rgb;
    ALPHA = clamp(rim * 0.35 + band * rim * 0.12, 0.0, 1.0);
}" };
        float r = World.DomeReach.ToFloat();
        var mat = new ShaderMaterial { Shader = _domeShader };
        mat.SetShaderParameter("tint", new Color(0.55f, 0.85f, 1f).Lerp(team, 0.35f));
        return new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 1.3f, IsHemisphere = true, RadialSegments = 40, Rings = 12 },
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    static string NameOf(Kind k) => k switch
    {
        Kind.LightTank => "Hover tank", Kind.Tank => "Battle wheel", Kind.Ranger => "Rifle mech", Kind.Mortar => "Lobber", Kind.Artillery => "Long leg",
        Kind.Scout => "Skitter", Kind.Flier => "Dart", Kind.Bomber => "Blimp", Kind.Hq => "HQ",
        Kind.Feral => "Feral scrapper", Kind.FeralBrute => "Feral brute", _ => k.ToString(),
    };

    /// <summary>A unit is its model; the HQ its pyramid (Pyramid.cs); a defence its model, facing the enemy.</summary>
    readonly Dictionary<int, UnitRig> _rigs = new();
    readonly Dictionary<int, BuildingRig> _buildingRigs = new();   // buildings with moving parts: a turning radar
    readonly Dictionary<int, Vector3> _drawnAt = new();

    Node3D MakeView(Entity e)
    {
        if (e.Stats.IsUnit)
        {
            // (Damage/armour upgrades have no visual for now: the user, 2026-10-22, "they dont need a visual right
            // now". UnitArt.AddEmblems still draws deck emblems if they come back.)
            var unit = UnitArt.Make(UnitModel(e.Kind, CivOf(e.Owner)), TeamColor[e.Owner]);
            if (e.Stats.Shields) unit.AddChild(Dome(TeamColor[e.Owner]));
            return unit;
        }
        if (e.Kind == Kind.Hq) return MakePyramid(e);
        var name = BuildingModel(e.Kind, e.Owner);
        bool rising = e.Building > 0;
        var building = rising ? UnitArt.MakeBuilding(name, TeamColor[e.Owner]) : UnitArt.Make(name, TeamColor[e.Owner]);
        if (rising) _rising.Add(e.Id);
        building.Basis = Basis.LookingAt(TowardEnemy(e.Owner), Vector3.Up);
        return building;
    }

    readonly HashSet<int> _rising = new();

    static Vector3 Flat(FixVec2 d) => new Vector3(d.X.ToFloat(), 0, d.Z.ToFloat()).Normalized();

    float _frameDt;

    // ---- rally flags: where my factories' units gather -----------------------------------------------------------

    readonly Dictionary<int, (Vector3 From, Vector3 Dir, float Age)> _rollOut = new();   // units driving out of the HQ's doors
    const float RollOutTime = 1.1f;

    readonly Dictionary<int, Node3D> _flags = new();
    ImmediateMesh _rallyLine;
    Dust _dust;

    static Node3D MakeFlag(Color team)
    {
        var flag = new Node3D();
        flag.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 2.6f, RadialSegments = 6 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.18f, 0.16f) },
            Position = new Vector3(0, 1.3f, 0),
        });
        flag.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(1.0f, 0.7f, 0.05f), LeftToRight = 0f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = team, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            Position = new Vector3(0.5f, 2.2f, 0),
            RotationDegrees = new Vector3(0, 0, -90),
        });
        flag.AddChild(new MeshInstance3D   // a ring on the ground: how close they gather
        {
            Mesh = new TorusMesh { InnerRadius = 1.4f, OuterRadius = 1.55f, Rings = 24, RingSegments = 3 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(team, 0.7f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
            Position = new Vector3(0, 0.1f, 0),
        });
        return flag;
    }

    /// <summary>My HQ's flag where its units rally (none for the enemy's: you don't see their orders).</summary>
    void UpdateRallyFlag(Entity f)
    {
        _flags.TryGetValue(f.Id, out var flag);
        if (f.Owner != _me || !f.HasRally) { if (flag != null) flag.Visible = false; return; }
        if (flag == null) { flag = MakeFlag(TeamColor[_me]); AddChild(flag); _flags[f.Id] = flag; }
        flag.Visible = true;
        flag.Position = ToV3(f.Rally);
    }

    /// <summary>A line from my HQ, when it's picked, to its flag.</summary>
    void DrawRallyLine(Entity selected)
    {
        _rallyLine.ClearSurfaces();
        if (selected is not { Kind: Kind.Hq, HasRally: true } || selected.Owner != _me) return;
        var from = ToV3(selected.Pos);
        var to = ToV3(selected.Rally);
        _rallyLine.SurfaceBegin(Mesh.PrimitiveType.Lines, _tracerMat);
        int dashes = Mathf.Max(2, (int)(from.DistanceTo(to) / 1.2f));
        for (int i = 0; i < dashes; i += 2)   // dashed, following the ground
        {
            for (int k = 0; k < 2; k++)
            {
                var p = from.Lerp(to, (i + k) / (float)dashes);
                _rallyLine.SurfaceSetColor(TeamColor[_me].Lightened(0.4f));
                _rallyLine.SurfaceAddVertex(new Vector3(p.X, _terrain.HeightAt(p.X, p.Z) + 0.25f, p.Z));
            }
        }
        _rallyLine.SurfaceEnd();
    }

    /// <summary>A place in the sim, stood on its ground.</summary>
    Vector3 ToV3(FixVec2 p) { float x = p.X.ToFloat(), z = p.Z.ToFloat(); return new(x, _terrain.HeightAt(x, z), z); }

    Terrain _terrain;

    /// <summary>Where a view's origin sits above the ground: boxes are centred, unit models stand on y = 0.</summary>
    static float HeightOf(Kind k) => k switch { Kind.Drone => 3.2f, Kind.Flier => 6f, Kind.Bomber => 7.5f, _ => 0f };   // models stand on their origin; fliers fly

    /// <summary>The height shots leave from: about each one's gun.</summary>
    static float MuzzleOf(Kind k) => k switch { Kind.Hq => 2.5f, Kind.Tower => 4.0f, Kind.Scout => 0.8f, Kind.Tank => 0.95f, Kind.Artillery => 2.8f, Kind.LightTank => 1.15f, Kind.Mortar => 2.0f, Kind.Spider => 0.98f, Kind.Crawler => 0.32f, Kind.Piercer => 0.72f, _ => 1f };

    /// <summary>How far in front of its centre a gun's muzzle is (units face their target, so it is along that line).</summary>
    static float BarrelOf(Kind k) => k switch { Kind.Tank => 1.7f, Kind.Artillery => 1.8f, Kind.Scout => 0.7f, Kind.Tower => 1.2f, Kind.LightTank => 1.05f, Kind.Mortar => 0.05f, Kind.Spider => 0.95f, Kind.Crawler => 0.8f, Kind.Piercer => 1.75f, _ => 0f };

    /// <summary>How quickly each kind swings round, in radians a second.</summary>
    static float TurnRateOf(Kind k) => k switch { Kind.Scout => 9f, Kind.Tank => 4f, Kind.Artillery => 2.5f, Kind.LightTank => 7f, Kind.Mortar => 3.5f, Kind.Spider => 8f, Kind.Crawler => 10f, Kind.Piercer => 3f, Kind.Drone => 6f, _ => 4f };

    // ---- loop --------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        MoveCamera((float)delta);

        if (_lineupFrame >= 0) { StepLineup(); return; }
        if (_reelT >= 0) { StepReel((float)delta); return; }
        if (_session == null)
        {
            PollConnection();
            _banner.Text = _connectStatus;
            if (_session == null) return;
        }

        if (MenuHoldsGame) { _acc = 0; _frameDt = 0; Draw(1); TakeShotWhenReady(); return; }   // paused from the menu: nothing moves
        _acc += delta;
        int steps = 0;
        _waiting = false;
        while (_acc >= TickLength && steps++ < 5)
        {
            if (!StepWorld()) { _waiting = true; _acc = TickLength; break; }   // try again next frame
            _acc -= TickLength;
        }
        if (_acc > TickLength) _acc = 0;   // fell far behind; don't spiral

        FadeBlasts((float)delta);
        _frameDt = (float)delta;
        Draw((float)(_acc / TickLength));
        if (_watchFelled && !_felledFocused && _forest.FirstKnocked is { } felled)
        {
            _felledFocused = true; _focus = felled; _distance = 26; _noFog = true; UpdateFog(); _selected = -1; _shotFrames = 120;
        }
        TakeShotWhenReady();
    }

    /// <summary>One tick, if the session allows it (a network match may be waiting on the other player).</summary>
    bool StepWorld()
    {
        if (_frozen) return false;
        if (_autoMe != null && _autoThoughtAt != W.Tick)   // once per tick, even while waiting
        {
            _autoThoughtAt = W.Tick;
            _autoMe.Think(W, _session.Issue);
        }

        var prev = W.Entities.ToDictionary(e => e.Id, e => ToV3(e.Pos));
        if (!_session.TryStep()) return false;
        foreach (var (id, p) in prev) _prevPos[id] = p;
        UpdateFog();

        foreach (var s in W.Shots)
        {
            // Whoever fires keeps facing its target until a little after it could fire again.
            _aim[s.ShooterId] = (s.TargetId, W.Tick + World.StatsFor[(int)s.By].Reload + 10);
            var at = ToV3(s.From);
            var to = ToV3(s.To);
            if (!SeenAt(at) && !SeenAt(to)) continue;   // not from fog to fog
            var dir = new Vector3(to.X - at.X, 0, to.Z - at.Z);
            dir = dir.LengthSquared() > 1e-6f ? dir.Normalized() : Vector3.Right;
            if (World.StatsFor[(int)s.By].IsUnit) _facing.TryAdd(s.ShooterId, dir);
            var from = at + Vector3.Up * MuzzleOf(s.By) + dir * BarrelOf(s.By);
            if (s.By is not (Kind.Mender or Kind.Hive) && _rigs.TryGetValue(s.ShooterId, out var shooter))   // a rigged unit: the barrel kicks, the shot leaves its muzzle
            {
                float? yaw = null;
                if (_facing.TryGetValue(s.ShooterId, out var hull)) yaw = hull.SignedAngleTo(dir, Vector3.Up);
                shooter.Fire(yaw);
                if (shooter.Muzzle != null && shooter.Muzzle.IsInsideTree()) from = shooter.Muzzle.GlobalPosition;
            }
            var target = W.Find(s.TargetId);
            var aimAt = to + Vector3.Up * (target != null ? Mathf.Min(TopOf(target.Kind) * 0.4f, 2f) : 0.6f);
            if (s.Flight > 0) aimAt = to;   // shells burst on the ground
            // A laser stops where it meets the armour, not in the middle of the target (where the target's own beam
            // leaves from, so the two would look like one shot out of its turret).
            if (s.By == Kind.LightTank && target != null)
            {
                var back = (from - aimAt) with { Y = 0 };
                float r = Mathf.Min(World.StatsFor[(int)target.Kind].Radius.ToFloat() * 0.75f, back.Length() * 0.5f);
                if (back.LengthSquared() > 1e-4f) aimAt += back.Normalized() * r;
            }
            if (s.By is Kind.Mender or Kind.Hive && _rigs.TryGetValue(s.ShooterId, out var carrier)) carrier.SendDrone(aimAt);   // a drone flies over to it
            _proj.Fire(from, aimAt, s.By, TeamColor[s.Owner], s.Flight / (float)World.TicksPerSecond);   // a shell lands when the sim lands it
        }
        foreach (var (at, owner, amount) in W.Salvaged)   // my salvage: a "+60" rising off the wreck
            if (owner == _me) _salvageTexts.Add((ToV3(at), amount, 0f));
        return true;
    }

    /// <summary>A flash where a shell lands, as wide as its splash, fading over half a second.</summary>
    void Blast(Vector3 at, float radius)
    {
        var flash = new MeshInstance3D
        {
            // (a faint ring on the ground the size of the splash: what the shell caught; the fireball is Explosions)
            Mesh = new TorusMesh { InnerRadius = radius * 0.93f, OuterRadius = radius, Rings = 48, RingSegments = 4 },
            MaterialOverride = Unshaded(new Color(1f, 0.6f, 0.2f, 0.45f)),
            Position = at + Vector3.Up * 0.15f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(flash);
        _blasts.Add((flash, 0f));
    }

    void FadeBlasts(float dt)
    {
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var (node, age) = _blasts[i];
            age += dt;
            if (age > 0.5f) { node.QueueFree(); _blasts.RemoveAt(i); continue; }
            node.Scale = new Vector3(1, 0.05f, 1) * (0.75f + age * 0.5f);
            ((StandardMaterial3D)node.MaterialOverride).AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.45f * (1 - age / 0.5f));
            _blasts[i] = (node, age);
        }
    }

    void Draw(float alpha)
    {
        if (W.Paused) alpha = 1;
        var alive = new HashSet<int>();
        foreach (var e in W.Entities)
        {
            alive.Add(e.Id);
            // A building going up is a hologram rising with its progress; once finished it's swapped for the real thing.
            if (_views.TryGetValue(e.Id, out var old) && _rising.Contains(e.Id) && e.Building == 0)
            {
                old.QueueFree();
                _views.Remove(e.Id);
                _rising.Remove(e.Id);
            }
            if (!_views.TryGetValue(e.Id, out var view))
            {
                view = MakeView(e);
                AddChild(view);
                _views[e.Id] = view;
                if (e.Stats.IsUnit && UnitRig.Find(view, e.Id) is { } newRig) _rigs[e.Id] = newRig;
                if (!e.Stats.IsUnit && e.Kind != Kind.Hq && e.Building == 0 && BuildingRig.Find(view) is { } bRig) _buildingRigs[e.Id] = bRig;   // (the pyramid turns its own parts)
                if (e.Stats.IsUnit && MadeAtDoor(e) is { } formed && W.Find(e.FactoryId) is { } home)   // out of a door of its HQ
                {
                    var outward = (ToV3(e.Pos) - ToV3(home.Pos)) with { Y = 0 };
                    outward = outward.LengthSquared() > 1e-4f ? outward.Normalized() : Vector3.Forward;
                    _rollOut[e.Id] = (formed with { Y = ToV3(e.Pos).Y }, outward, 0f);   // from where it formed, out to where it stands
                    _facing[e.Id] = outward;
                }
            }
            if (_rising.Contains(e.Id))
                UnitArt.SetBuilt(view, 1f - e.Building / (float)World.ConstructionTicks(e.Kind));
            var cur = ToV3(e.Pos);
            var prev = _prevPos.TryGetValue(e.Id, out var p) ? p : cur;
            var pos = prev.Lerp(cur, alpha);
            if (_rollOut.TryGetValue(e.Id, out var roll))   // driving out of its bay
            {
                roll.Age += W.Paused ? 0 : _frameDt;
                float t = Mathf.Clamp(roll.Age / RollOutTime, 0, 1);
                pos = roll.From.Lerp(pos, t * t * (3 - 2 * t));
                if (t >= 1) _rollOut.Remove(e.Id); else _rollOut[e.Id] = roll;
            }
            float walkBob = 0;
            if (e.Stats.IsUnit)
            {
                var step = cur - prev;
                // Dust off the tracks while it moves (step is one tick's move).
                var flat = new Vector3(step.X, 0, step.Z);
                bool moving = flat.LengthSquared() > 1e-4f;
                if (!W.Paused && moving && Shown(e))
                {
                    float r = e.Stats.Radius.ToFloat();
                    bool wheels = e.Kind == Kind.Scout, legs = e.Kind is Kind.Spider or Kind.Crawler or Kind.Piercer or Kind.Ranger or Kind.Artillery, flies = e.Stats.Flying;
                    bool hovers = e.Kind == Kind.LightTank;   // (the hover tank: dust blown up, no tread marks, trees left standing)
                    if (!flies) _dust.Drive(e.Id, pos, flat.Normalized(), r * 1.1f, r * 0.7f, flat.Length() * World.TicksPerSecond, _frameDt);
                    // Tread marks under each track (the scout's wheels leave thinner, fainter ones; walkers and fliers none).
                    if (!legs && !flies && !hovers) _tracks.Drive(e.Id, pos, flat.Normalized(), r * 0.55f, r * (wheels ? 0.3f : 0.48f), wheels ? 0.7f : 1f);
                    // Trees in the way go over (the scout hovers, walkers step and fliers fly, so they slip between them).
                    if (!wheels && !legs && !flies && !hovers) _forest.Trample(pos, flat.Normalized(), r);
                }
                // Face where it's going; standing still, face what it's shooting at. It swings round, not snaps.
                var face = _facing.TryGetValue(e.Id, out var f) ? f : TowardEnemy(e.Owner);
                var want = face;
                if (moving) want = flat.Normalized();
                else if (_aim.TryGetValue(e.Id, out var aim) && aim.Until >= W.Tick && W.Find(aim.Target) is { Dead: false } t)
                {
                    var to = ToV3(t.Pos) - cur;
                    to.Y = 0;
                    if (to.LengthSquared() > 1e-4f) want = to.Normalized();
                }
                float turn = face.SignedAngleTo(want, Vector3.Up), most = TurnRateOf(e.Kind) * (W.Paused ? 0 : _frameDt);
                face = face.Rotated(Vector3.Up, Mathf.Clamp(turn, -most, most)).Normalized();
                _facing[e.Id] = face;
                // Tilt with the ground under it.
                float ahead = _terrain.HeightAt(pos.X + face.X, pos.Z + face.Z), behind = _terrain.HeightAt(pos.X - face.X, pos.Z - face.Z);
                view.Basis = Basis.LookingAt(new Vector3(face.X, (ahead - behind) * 0.5f, face.Z), Vector3.Up);
                // Moving parts (UnitRig): wheels roll the ground covered, the turret tracks its target.
                if (_rigs.TryGetValue(e.Id, out var unitRig))
                {
                    var lastAt = _drawnAt.TryGetValue(e.Id, out var la) ? la : pos;
                    _drawnAt[e.Id] = pos;
                    float rolled = W.Paused ? 0 : new Vector2(pos.X - lastAt.X, pos.Z - lastAt.Z).Length();
                    float? turretYaw = null;
                    if (_aim.TryGetValue(e.Id, out var held) && held.Until >= W.Tick && W.Find(held.Target) is { Dead: false } aimed)
                    {
                        var toAimed = ToV3(aimed.Pos) - pos;
                        toAimed.Y = 0;
                        if (toAimed.LengthSquared() > 1e-4f) turretYaw = face.SignedAngleTo(toAimed.Normalized(), Vector3.Up);
                    }
                    unitRig.Update(W.Paused ? 0 : _frameDt, rolled, turretYaw);
                }
                // A walker sways side to side and bobs as it steps (its legs are one piece with it).
                if (e.Kind is Kind.Spider or Kind.Crawler or Kind.Piercer or Kind.Ranger && moving && !W.Paused && !_rigs.ContainsKey(e.Id))
                {
                    float gait = (float)Time.GetTicksMsec() * 0.012f + e.Id;
                    view.Basis = view.Basis * new Basis(Vector3.Back, Mathf.Sin(gait) * 0.06f);
                    walkBob = Mathf.Abs(Mathf.Sin(gait)) * 0.12f;
                }
                // A flier hovers, drifting up and down, and stays level over hills.
                if (e.Stats.Flying)
                {
                    view.Basis = Basis.LookingAt(new Vector3(face.X, 0, face.Z), Vector3.Up);
                    walkBob = Mathf.Sin((float)Time.GetTicksMsec() * 0.003f + e.Id) * 0.15f;
                }
            }
            view.Position = pos + Vector3.Up * (HeightOf(e.Kind) + walkBob);
            view.Visible = Shown(e);
            if (e.Kind == Kind.Hq) { UpdateRallyFlag(e); UpdatePyramid(e, view, W.Paused ? 0 : _frameDt); }
            if (_buildingRigs.TryGetValue(e.Id, out var bRigNow)) bRigNow.Update(W.Paused ? 0 : _frameDt, false);
        }
        foreach (var id in _views.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _views[id].QueueFree();
            _views.Remove(id);
            _rigs.Remove(id);
            _buildingRigs.Remove(id);
            _rollOut.Remove(id);
            _drawnAt.Remove(id);
            _prevPos.Remove(id);
            _facing.Remove(id);
            _aim.Remove(id);
            _dust.Forget(id);
            _tracks.Forget(id);
            if (_flags.Remove(id, out var gone)) gone.QueueFree();
            ForgetPyramid(id);
            _rising.Remove(id);
        }

        BuildRing();
        DrawRadarBlips();
        DrawSalvage();
        DrawFires();
        DrawPlacement();
        DrawBarsAndSelection(alpha);
        DrawHud();
    }

    /// <summary>Roughly how high each thing stands, for putting its health bar just above it.</summary>
    static float TopOf(Kind k) => k switch
    {
        Kind.Hq => 8f, Kind.Tower => 5.6f,
        Kind.Turret => 2.6f, Kind.Flak => 2.6f, Kind.Bombard => 2.6f, Kind.Wall => 1.6f, Kind.Derelict => 2.8f, Kind.FeralBrute => 1.8f,
        Kind.Scout => 2.6f, Kind.Tank => 2.1f, Kind.Artillery => 2.2f, Kind.LightTank => 1.7f, Kind.Mortar => 1.8f, Kind.Spider => 1.6f, Kind.Crawler => 1.1f, Kind.Piercer => 1.3f, Kind.Drone => 4.2f, Kind.Ranger => 1.6f, Kind.Flier => 7.2f, Kind.Bomber => 8.8f, Kind.Mender => 2.3f, Kind.Bulwark => 3.2f, Kind.Hive => 2.4f, _ => 2f,
    };

    readonly List<(Vector3 At, int Amount, float Age)> _salvageTexts = new();
    const float SalvageSeconds = 2.4f;

    /// <summary>The salvage numbers: each rises off its wreck and fades out.</summary>
    void DrawSalvage()
    {
        _overlay.Floats.Clear();
        for (int i = _salvageTexts.Count - 1; i >= 0; i--)
        {
            var (at, amount, age) = _salvageTexts[i];
            age += W.Paused ? 0 : _frameDt;
            if (age > SalvageSeconds) { _salvageTexts.RemoveAt(i); continue; }
            _salvageTexts[i] = (at, amount, age);
            var p = at + Vector3.Up * (2.5f + age * 2.5f);
            if (_camera.IsPositionBehind(p)) continue;
            _overlay.Floats.Add((_camera.UnprojectPosition(p), $"+{amount}", 1 - age / SalvageSeconds));
        }
    }

    void DrawBarsAndSelection(float alpha)
    {
        _overlay.Bars.Clear();
        _overlay.Badges.Clear();
        _overlay.Tags.Clear();
        var right = _camera.GlobalBasis.X;
        Entity selected = null;
        foreach (var e in W.Entities)
        {
            var cur = ToV3(e.Pos);
            var pos = _prevPos.TryGetValue(e.Id, out var p) && !W.Paused ? p.Lerp(cur, alpha) : cur;
            var top = pos + Vector3.Up * (e.Kind == Kind.Hq ? PyramidTop(W.Players[e.Owner].Tier) : TopOf(e.Kind));
            if (!Shown(e)) continue;
            if (e.Id == _selected) { selected = e; _selRing.Position = pos + Vector3.Up * 0.08f; }
            // Only veterancy floats over things (sword/shield upgrade badges are off for now, see MakeView).
            if (e.Rank > 0 && !_camera.IsPositionBehind(top))
                _overlay.Badges.Add((_camera.UnprojectPosition(top) - new Vector2(0, 14), 0, 0, e.Rank));
            // A bar only once it's been hurt (or while it's selected).
            if (e.Hp >= e.MaxHp && e.Id != _selected) continue;
            if (_camera.IsPositionBehind(top)) continue;
            var r = e.Stats.Radius.ToFloat();
            var width = _camera.UnprojectPosition(top + right * r).X - _camera.UnprojectPosition(top - right * r).X;
            _overlay.Bars.Add((_camera.UnprojectPosition(top), width, Mathf.Clamp(e.Hp / (float)e.MaxHp, 0, 1),
                               TeamColor[e.Owner], e.Id == _selected));
        }
        _overlay.QueueRedraw();

        if (selected == null) _selected = -1;   // (it died)
        _scouts.RemoveWhere(id => W.Find(id) == null);
        if (selected == null && _scouts.Count > 0) { _selected = _scouts.Min(); selected = W.Find(_selected); }
        _selRing.Visible = selected != null;
        _statsPanel.Visible = selected != null;
        DrawRallyLine(selected);
        if (selected == null) return;
        var ring = selected.Stats.Radius.ToFloat() * (selected.Stats.IsUnit ? 1.25f : 1.15f);
        _selRing.Scale = new Vector3(ring, 1, ring);
        _stats.Text = _scouts.Count > 1
            ? $"{_scouts.Count} scouts picked\nRight-click the ground to send them\n(Shift-click adds or drops one; Esc lets go)"
            : Describe(selected);
        _statsPanel.OffsetTop = -_statsPanel.GetCombinedMinimumSize().Y - 16;
        _statsPanel.Size = _statsPanel.GetCombinedMinimumSize();
    }

    // A defence's three upgrade tracks: its Production track is range.
    static readonly string[] DefenceTrack = { "Range", "Damage", "Armour" };
    static readonly string[] DefenceEffect = { "+2 range", "+25% damage", "+25% health" };

    static string Clock(int ticks)
    {
        int secs = Mathf.Max(0, Mathf.CeilToInt(ticks / (float)World.TicksPerSecond));
        return $"{secs / 60}:{secs % 60:00}";
    }


    string Describe(Entity e)
    {
        var s = e.Stats;
        var lines = new List<string>
        {
            $"{NameOf(e.Kind)}  ({(e.Owner == _me ? "yours" : e.Owner == World.Feral ? "feral" : "enemy")})",
            e.Kind switch
            {
                Kind.Scout => e.Owner == _me
                    ? "Unarmed eyes: watches the enemy.\nRight-click to send it."
                    : "Unarmed eyes: it's scouting you.",
                Kind.Tank => "Tough all-rounder.",
                Kind.LightTank => "Quick and cheap to build, but fragile:\na fast-firing light gun. Raids and screens.",
                Kind.Spider => "A four-legged walker: quick, lightly armoured, a twin\nlaser firing three times a second, little each shot.",
                Kind.Crawler => "The swarm's body: cheap, quick and fragile,\na little claw-gun. Comes in numbers.",
                Kind.Piercer => "Anti-armour: slow, a heavy spike-gun every 3 s that\ndoes two and a half times to tanks and buildings.",
                Kind.Drone => e.Owner == _me
                    ? "A flying scout: unarmed, quick, sees far, crosses water.\nOnly HQs, towers and outposts can shoot it down."
                    : "A flying scout: it's watching you.",
                Kind.Ranger => "Small, quick and fragile, with small arms that\nreach fliers too: the cheap answer to most things.",
                Kind.Flier => "An interceptor: fast, fights other fliers only.\nWith none about, it keeps over your army.",
                Kind.Mender => "A repair carrier: unarmed. Keeps behind your army and\nsends its drones to mend the nearest damaged unit.",
                Kind.Bulwark => $"A shield walker: slow, heavy, unarmed. Everything of\nyours under its dome takes {World.DomePercent}% less damage.",
                Kind.Hive => "A swarm carrier: its drones fly out of the roof and sting\nwhatever is in reach, fliers too. Fragile itself.",
                Kind.Bomber => "Slow bombing runs: flies over a ground target, drops a\nheavy bomb with a wide blast, flies on and comes round.\nOnly anti-air can touch it.",
                Kind.Mortar => "A small artillery tank: shorter range than artillery,\na small blast, quick shells. Can't fire at anything close.",
                Kind.Artillery => "Outranges everything and hits an area, but can't fire\nat anything close. Its shells take a while to land:\nanything moving gets out from under them.",
                Kind.Hq => $"Lose it and you lose the match. Everything is built here (B):\nqueue units, research the tech tree (Tab). It grows a tier as you\nbring metal in. Its radar sweeps {World.HqRadar} round it every {World.RadarTicks / World.TicksPerSecond} s.",
                Kind.Tower => "Guards the base: long range and sight.",
                Kind.Turret => "A gun emplacement: holds the ground round a base.",
                Kind.Flak => "A quick gun that reaches fliers too; light against the ground.",
                Kind.Bombard => "An artillery emplacement: long reach and a blast, but\nnothing close, and it needs others to spot for it.",
                Kind.Wall => "A block of armour. A line of them turns attackers aside.",
                Kind.Feral => $"A feral scrapper: wild, quick, weak. Worth {World.Salvage(Kind.Feral)} metal as scrap.",
                Kind.FeralBrute => $"A feral brute: wild, slow, tough. Worth {World.Salvage(Kind.FeralBrute)} metal as scrap.",
                Kind.Derelict => $"An old turret gone feral, guarding the ground. Worth {World.Salvage(Kind.Derelict)} metal.",
                _ => "",
            },
            $"Health  {Mathf.Max(e.Hp, 0)} / {e.MaxHp}",
        };
        if (s.Reload > 0)
        {
            float every = s.Reload / (float)World.TicksPerSecond;
            lines.Add($"Damage  {e.Damage} every {every:0.#} s  ({e.Damage / every:0.#} per s)");
            lines.Add($"Range  {World.RangeOf(e)}" + (s.MinRange.Raw > 0 ? $"   (none within {s.MinRange})" : ""));
            if (s.Splash.Raw > 0) lines.Add($"Splash  {s.Splash} wide");
        }
        if (s.IsUnit) lines.Add($"Speed  {World.SpeedOf(e).ToFloat() * World.TicksPerSecond:0.#} per s");
        if (e.Height.ToFloat() > 0.5f)
            lines.Add($"High ground  +{Mathf.Min(e.Height.ToFloat(), 5f):0.#} sight, more range firing down");
        if (s.IsUnit && ModList(e.Mods) is { Length: > 0 } madeWith) lines.Add($"Made with  {madeWith}");
        if (s.IsUnit && e.Shield > 0) lines.Add($"Shield  {e.Shield}");
        if (s.IsUnit && e.Fitting == Fitting.Napalm)
            lines.Add("Napalm: hits leave the ground burning");
        if (e.Building > 0)
            lines.Add($"Under construction  {Mathf.CeilToInt(e.Building / (float)World.TicksPerSecond)} s left");
        if (e.Kind == Kind.Hq)
        {
            var p = W.Players[e.Owner];
            lines.Add($"Tier {p.Tier}" + (p.Tier < World.MaxTier && e.Owner == _me ? $"   (tier {p.Tier + 1} at {World.CollectedFor(p.Tier + 1)} brought in; {p.Collected} so far)" : ""));
        }
        if (e.Kind == Kind.Hq && e.Owner == _me)
            lines.Add($"Doors  {World.DoorsOf(W.Players[_me])}   " + (e.HasRally ? "New units gather at the flag first" : "Right-click the ground to set a rally point"));
        return string.Join("\n", lines);
    }

    /// <summary>The entity under a point on the ground, if any (the nearest whose footprint holds it).</summary>
    Entity EntityAt(Vector3 ground)
    {
        Entity best = null;
        float bestD = float.MaxValue;
        foreach (var e in W.Entities)
        {
            var d = ToV3(e.Pos).DistanceTo(ground);
            if (d <= e.Stats.Radius.ToFloat() + 0.6f && d < bestD && Shown(e)) { best = e; bestD = d; }
        }
        return best;
    }

    void DrawPlacement()
    {
        _mouseGround = GroundUnderMouse();
        if (W.Winner >= 0) _placing = null;
        bool shown = _placing != null && _mouseGround.HasValue;
        _placeProblem = null;
        if (shown)
        {
            var at = _mouseGround.Value;
            bool ok = W.CanPlaceDefence(_me, ToFix(at), _placing.Value, out _placeProblem);
            // The hologram of the building itself (the user, 2026-10-19: "it should give the outline of the model of
            // what it's building"), green where it can go and red where it can't.
            var name = BuildingModel(_placing.Value, _me);
            if (_placeModel == null || _placeModelKey != (name, ok))
            {
                _placeModel?.QueueFree();
                _placeModel = PlacementModel(name, ok ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.25f, 0.2f));
                _placeModelKey = (name, ok);
            }
            _placeModel.Position = at;
        }
        if (_placeModel != null) _placeModel.Visible = shown;
    }

    Node3D _placeModel;
    (string, bool) _placeModelKey;

    /// <summary>A building's model as an unbuilt hologram, facing the enemy like the real thing.</summary>
    Node3D PlacementModel(string name, Color tint)
    {
        var model = UnitArt.MakeBuilding(name, tint);
        UnitArt.SetBuilt(model, -0.05f);   // (below zero, so even its foot is hologram, not solid)
        model.Basis = Basis.LookingAt(TowardEnemy(_me), Vector3.Up);
        AddChild(model);
        return model;
    }

    void DrawHud()
    {
        var me = W.Players[_me];
        int myUnits = 0, theirUnits = 0;
        int myHq = 0, theirHq = 0;
        foreach (var e in W.Entities)
        {
            if (e.Stats.IsUnit && Shown(e) && e.Owner != World.Feral) { if (e.Owner == _me) myUnits++; else theirUnits++; }   // (only enemies we can see)
            if (e.Kind == Kind.Hq) { if (e.Owner == _me) myHq = e.Hp; else theirHq = e.Hp; }
        }
        UpdateCard();
        UpdateQueueBar();
        UpdateTechTree();
        var menu = "[B] HQ   [Tab] Tech tree   [Q] Scout   [E] Pick scouts   [P] Pause   [Esc] Menu";
        var placingWhat = _placing is { } pd ? $"a {NameOf(pd).ToLowerInvariant()} (within {World.BaseReach} of your HQ; Shift to place more)" : "";
        var building = _placing == null
            ? menu
            : $"Placing {placingWhat}: click to build, Esc to cancel" + (_placeProblem != null ? $"   ({_placeProblem})" : "");
        _hud.Text =
            $"Metal {me.Metal}   +{W.IncomeOf(_me)}/s   (and salvage: destroy things)\n" +
            building + "\n" +
            $"Army {W.ArmyCount(_me)}/{World.MaxArmyOf(MyCiv)}   units seen {myUnits} vs {theirUnits}\n" +
            $"Pyramid tier {me.Tier}" + (me.Tier < World.MaxTier ? $"  (next at {World.CollectedFor(me.Tier + 1)} brought in: {me.Collected})" : "") + $"   doors {World.DoorsOf(me)}\n" +
            $"HQ {myHq} vs {theirHq}\n" +
            $"Pauses left {me.PausesLeft}  [P]\n" +
            (W.Started && World.FeralsOn ? $"Feral wave {W.WaveNumber + 1} in {Clock(W.NextWaveIn)}\n" : "") +
            $"t {W.Tick / World.TicksPerSecond}s   sum {W.Checksum() & 0xFFFF:X4}";
        if (_session is LockstepSession ls)
            _hud.Text += $"\nLAN {(_me == 0 ? "host" : "client")}   checks passed {ls.VerifiedTicks}" + (_waiting ? "   waiting..." : "");

        if (_session.Problem != null)
            _banner.Text = _session.Problem;
        else if (W.Winner >= 0)
            _banner.Text = (W.Winner == _me ? "Victory" : "Defeat") + (_session is LocalSession ? "  -  R to play again" : "");
        else if (!W.Started)
            _banner.Text = me.Ordered
                ? "Waiting for the enemy to queue their first units..."
                : "Queue your first units: click your HQ (or press B) and pick some\nThe battle starts once both sides have";
        else if (W.Paused)
        {
            var secs = Mathf.CeilToInt(W.PauseTicksLeft / (float)World.TicksPerSecond);
            var who = W.PausedBy == _me ? "You" : "Enemy";
            _banner.Text = $"PAUSED by {who}  {secs}s\nOrders apply when play resumes" + (W.PausedBy == _me ? "  -  Enter to resume" : "");
        }
        else _banner.Text = "";
    }

    // ---- where builds and orders go (the command card, CommandCard.cs, uses these) ----------------------------

    /// <summary>My HQ.</summary>
    Entity MyHq() => W.HqOf(_me);

    // ---- the Escape menu (the user, 2026-10-20: "we need escape to pause") -----------------------------------------

    Control _menu;
    Label _menuNote;
    Button _restartButton;
    bool _menuOpen;

    /// <summary>Against the computer the menu stops the game; in a LAN match it can't (the other side plays on).</summary>
    bool MenuHoldsGame => _menuOpen && _session is LocalSession;

    void BuildMenu(CanvasLayer ui)
    {
        _menu = new ColorRect { Color = new Color(0, 0, 0, 0.45f), AnchorRight = 1, AnchorBottom = 1, Visible = false };
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.95f), BorderColor = new Color(0.45f, 0.75f, 0.9f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 18, ContentMarginBottom = 18,
        });
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        var title = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30);
        column.AddChild(title);
        _menuNote = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _menuNote.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(_menuNote);
        Button MenuButton(string text, System.Action pressed)
        {
            var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(260, 44) };
            b.AddThemeFontSizeOverride("font_size", 19);
            b.Pressed += pressed;
            column.AddChild(b);
            return b;
        }
        MenuButton("Resume  (Esc)", () => SetMenu(false));
        _restartButton = MenuButton("Restart match", () => { SetMenu(false); _noFog = false; StartMatch(new LocalSession((ulong)GD.Randi())); });
        MenuButton("Skirmish: 5 v 5 hover tanks", () => { SetMenu(false); StartSkirmish("tanks"); });
        MenuButton("Skirmish: 5 v 5 mechs", () => { SetMenu(false); StartSkirmish("mechs"); });
        MenuButton("Skirmish: 5 v 5 battle wheels", () => { SetMenu(false); StartSkirmish("wheels"); });
        MenuButton("Skirmish: everything", () => { SetMenu(false); StartSkirmish("mixed"); });
        MenuButton("Quit to desktop", () => GetTree().Quit());
        panel.AddChild(column);
        centre.AddChild(panel);
        _menu.AddChild(centre);
        ui.AddChild(_menu);
    }

    void SetMenu(bool open)
    {
        _menuOpen = open;
        _menu.Visible = open;
        bool local = _session is LocalSession;
        _restartButton.Visible = local;
        _menuNote.Text = local ? "The battle is stopped." : "A LAN match can't stop here: it plays on.\nP calls one of your tactical pauses.";
        // Shells, dust and tread marks hold still with the battle.
        var effects = MenuHoldsGame ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        foreach (Node n in new Node[] { _dust, _proj, _booms, _tracks }) if (n != null) n.ProcessMode = effects;
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_session == null) return;
        if (_menuOpen)   // the menu takes everything; Escape closes it
        {
            if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) SetMenu(false);
            return;
        }
        // Middle-drag turns the view round (across) and tilts it (up and down); V puts it back.
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle) { _orbiting = middle.Pressed; return; }
        if (ev is InputEventMouseMotion turn && _orbiting)
        {
            _yaw -= turn.Relative.X * 0.006f;
            _pitch = Mathf.Clamp(_pitch + turn.Relative.Y * 0.25f, 25f, 85f);
            _viewHintUntil = Time.GetTicksMsec() + 20000;
            return;
        }
        if (_techOpen && ev is InputEventKey { Pressed: true, Echo: false } treeKey && TechTreeKey(treeKey.Keycode)) return;   // the tech tree has the keys while it's up
        if (ev is InputEventKey { Pressed: true, Echo: false } cardKey && cardKey.Keycode != Key.Escape | _cardPage != "main" && CardKey(cardKey.Keycode)) return;   // the command card's letters
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.V }) { ResetView(); return; }
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(25, _distance * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(480, _distance * 1.1f);   // (the map is wide)
            else if (mb.ButtonIndex == MouseButton.Left && _mouseGround.HasValue)
            {
                if (_placing != null)
                {
                    // One building per build: placing it ends build mode. A bad spot keeps it, to try again.
                    if (_placeProblem == null)
                    {
                        _session.Issue(new PlaceDefence(_me, ToFix(_mouseGround.Value), _placing.Value));
                        // Walls (and anything with Shift held) keep going: a line of them is a wall.
                        if (_placing != Kind.Wall && !mb.ShiftPressed) _placing = null;
                    }
                }
                else _dragFrom = mb.Position;   // a click or a box: decided when the button comes up
            }
            else if (mb.ButtonIndex == MouseButton.Right && _mouseGround.HasValue)
            {
                // Cancels build mode; sends the picked scouts; or, with my HQ selected, sets its rally.
                if (_placing != null) _placing = null;
                else if (_scouts.Count > 0 && W.Winner < 0)
                    _session.Issue(new MoveUnits(_me, _scouts.OrderBy(id => id).ToArray(), ToFix(_mouseGround.Value)));
                else if (W.Find(_selected) is { Kind: Kind.Hq } f && f.Owner == _me && W.Winner < 0)
                    _session.Issue(new SetRally(_me, f.Id, false, ToFix(_mouseGround.Value)));
            }
        }
        else if (ev is InputEventMouseButton up && !up.Pressed && up.ButtonIndex == MouseButton.Left && _dragFrom is { } from)
        {
            _dragFrom = null;
            _overlay.Box = null;
            bool add = up.ShiftPressed;
            if (from.DistanceTo(up.Position) > 8)
            {
                // A box: picks my scouts inside it.
                var box = new Rect2(from, Vector2.Zero).Expand(up.Position);
                if (!add) _scouts.Clear();
                foreach (var e in W.Entities)
                    if (World.IsSteered(e.Kind) && e.Owner == _me && !_camera.IsPositionBehind(ToV3(e.Pos)) && box.HasPoint(_camera.UnprojectPosition(ToV3(e.Pos))))
                        _scouts.Add(e.Id);
                _selected = _scouts.Count > 0 ? _scouts.Min() : -1;
            }
            else if (_mouseGround.HasValue)
            {
                var hit = EntityAt(_mouseGround.Value);
                if (hit is { } s && World.IsSteered(s.Kind) && s.Owner == _me)
                {
                    // Shift-click adds or drops a scout; a plain click picks just that one.
                    if (!add) _scouts.Clear();
                    if (!_scouts.Remove(s.Id)) _scouts.Add(s.Id);
                    _selected = _scouts.Count > 0 ? (_scouts.Contains(s.Id) ? s.Id : _scouts.Min()) : -1;
                }
                else if (!add)
                {
                    _scouts.Clear();
                    _selected = hit?.Id ?? -1;
                }
            }
        }
        else if (ev is InputEventMouseMotion motion && _dragFrom is { } start)
            _overlay.Box = start.DistanceTo(motion.Position) > 8 ? new Rect2(start, Vector2.Zero).Expand(motion.Position) : null;
        else if (ev is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.Keycode == Key.B && W.Winner < 0) OpenBuildCard();
        else if (ev is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Tab) SetTechTree(!_techOpen);
            else if (k.Keycode == Key.Q && W.Winner < 0) _session.Issue(new QueueUnit(_me, World.ScoutKindOf(MyCiv)));
            else if (k.Keycode == Key.E)   // pick all my scouts
            {
                _scouts.Clear();
                foreach (var e in W.Entities) if (World.IsScout(e.Kind) && e.Owner == _me) _scouts.Add(e.Id);
                _selected = _scouts.Count > 0 ? _scouts.Min() : -1;
            }
            else if (k.Keycode == Key.Escape)
            {
                // One thing per press: stop placing, else let go of what's picked, else open the menu.
                if (_techOpen) SetTechTree(false);
                else if (_placing != null) _placing = null;
                else if (_selected >= 0 || _scouts.Count > 0) { _selected = -1; _scouts.Clear(); }
                else SetMenu(true);
            }
            else if (k.Keycode == Key.P) _session.Issue(new Pause(_me));
            else if (k.Keycode == Key.Enter) _session.Issue(new Resume(_me));
            else if (k.Keycode == Key.R && W.Winner >= 0 && _session is LocalSession) StartMatch(new LocalSession((ulong)GD.Randi()));
        }
    }

    static FixVec2 ToFix(Vector3 v) => new(Fix.FromFloat(v.X), Fix.FromFloat(v.Z));

    void MoveCamera(float dt)
    {
        var pan = Vector3.Zero;
        bool letters = true;   // (the command card's letters keep clear of WASD)
        if ((letters && Input.IsKeyPressed(Key.W)) || Input.IsKeyPressed(Key.Up)) pan.Z -= 1;
        if ((letters && Input.IsKeyPressed(Key.S)) || Input.IsKeyPressed(Key.Down)) pan.Z += 1;
        if ((letters && Input.IsKeyPressed(Key.A)) || Input.IsKeyPressed(Key.Left)) pan.X -= 1;
        if ((letters && Input.IsKeyPressed(Key.D)) || Input.IsKeyPressed(Key.Right)) pan.X += 1;
        // Panning goes the way the camera faces (it may have been turned round).
        _focus += pan.Rotated(Vector3.Up, _yaw) * _distance * 0.8f * dt;
        _focus.X = Mathf.Clamp(_focus.X, -World.MapHalfX.ToFloat(), World.MapHalfX.ToFloat());
        _focus.Z = Mathf.Clamp(_focus.Z, -World.MapHalfZ.ToFloat(), World.MapHalfZ.ToFloat());

        // Normally looks down at 53 degrees from the south, like the house render camera; middle-drag turns and tilts it.
        var pitch = Mathf.DegToRad(_pitch);
        var offset = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch)).Rotated(Vector3.Up, _yaw) * _distance;
        // Aimed at the ground under the focus (on a hill, low angles would otherwise look into the hillside).
        var look = _focus + Vector3.Up * (_terrain?.HeightAt(_focus.X, _focus.Z) ?? 0f);
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), look + offset);

        // "V resets the view" shows for 20 s after the view was last turned.
        if (_viewHint != null) _viewHint.Visible = Time.GetTicksMsec() < _viewHintUntil;
    }

    // ---- turning the camera (the user, 2026-10-20: "middle clicking to change the camera to look around ... and some
    // key to reset the view, that is shown on screen when you change the perspective for 20s") -------------------
    float _yaw;                  // radians the view is turned round from looking north
    float _pitch = DefaultPitch; // degrees down from level
    const float DefaultPitch = 53f;
    bool _orbiting;              // the middle button is held
    ulong _viewHintUntil;
    Label _viewHint;

    void ResetView()
    {
        _yaw = 0;
        _pitch = DefaultPitch;
        _viewHintUntil = 0;
    }

    Vector3? GroundUnderMouse()
    {
        var mouse = _placeAtCentre ? GetViewport().GetVisibleRect().Size / 2 : GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return null;
        // Where the ray meets the hills: start at sea level, then step to the ground's height there a few times.
        float level = 0;
        Vector3 hit = from;
        for (int i = 0; i < 5; i++)
        {
            var t = (level - from.Y) / dir.Y;
            if (t < 0) return null;
            hit = from + dir * t;
            level = _terrain.HeightAt(hit.X, hit.Z);
        }
        hit.Y = level;
        if (Mathf.Abs(hit.X) > World.MapHalfX.ToFloat() || Mathf.Abs(hit.Z) > World.MapHalfZ.ToFloat()) return null;
        return hit;
    }
}
