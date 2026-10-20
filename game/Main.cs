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
/// Controls: 1 / 2 place a tank / artillery factory in a base ring with a free slot, 3 an outpost beside a scout, 4 an
/// extractor on a deposit (Esc or right click cancels). Q builds a scout at the HQ; E picks all scouts; click, Shift-click
/// or drag a box to pick scouts, right-click to send them. A click selects anything else to see its stats, upgrades
/// and Sell (Esc clears). WASD/arrows pan, wheel zooms,
/// P calls a tactical pause, Enter ends your pause early, R restarts once someone has won (vs AI).
///
/// Command line (after "--"): nothing = vs AI; --host = wait for a LAN opponent;
/// --join [ip] = join a host (default 127.0.0.1); --auto = an AI plays your side;
/// --shot = 60 s of AI vs AI, save screenshots/shot.png, quit (with --at N seconds, --wide for the whole map, --fight
/// to close on the busiest fight, --frames N to play on N frames first, --placing [outpost] to hold a building under the
/// centre, --hq to pick my HQ with a base rally set, --menu to show the Escape menu).
/// </summary>
public partial class Main : Node3D
{
    const double TickLength = 1.0 / World.TicksPerSecond;

    static readonly Color[] TeamColor = { new(0.25f, 0.55f, 1f), new(1f, 0.42f, 0.18f) };
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
    Kind? _placing;                // build mode: the factory a click places (keys 1-3; Esc or a placement ends it)
    int _selected = -1;            // the entity whose stats are shown (click it; Esc clears)
    Overlay _overlay;
    PanelContainer _statsPanel;
    Label _stats;
    MeshInstance3D _selRing;
    readonly Button[] _upgradeButtons = new Button[3];   // in the stats panel, for my selected factory
    Button _sellButton, _scoutButton, _autoButton, _cancelQueueButton;
    readonly HashSet<int> _scouts = new();   // my scouts picked to be sent somewhere (click, Shift-click, drag a box, or E)
    Vector2? _dragFrom;                       // where a left-button drag began, on screen
    readonly List<Node3D> _depositViews = new();
    readonly Dictionary<int, Node3D> _padViews = new();   // per deposit with one: its outpost pad
    int _sellArmed = -1;          // the building a first click on Sell asked about
    ulong _sellArmedUntil;
    Button _rallyButton;
    string _placeProblem;          // why the factory can't go under the mouse
    readonly List<(MeshInstance3D Node, float Age)> _blasts = new();   // artillery impacts, fading
    readonly Dictionary<int, Vector3> _prevPos = new();
    readonly Dictionary<int, Vector3> _facing = new();
    readonly Dictionary<int, (int Target, int Until)> _aim = new();   // per shooter: what it last fired at, and until which tick it keeps facing it
    Projectiles _proj;
    Tracks _tracks;
    bool _placeAtCentre;   // --placing: aim at the screen centre, not the mouse
    readonly Dictionary<(Kind, int), Mesh> _meshes = new();
    readonly Dictionary<(Kind, int), Material> _materials = new();

    Camera3D _camera;
    Vector3 _focus;
    float _distance = 70;
    MeshInstance3D _ghost;
    StandardMaterial3D _ghostMat;
    readonly List<Node3D> _heldGhosts = new();
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
        else StartMatch(new LocalSession((ulong)GD.Randi()));

        if (args.Contains("--shot"))
        {
            int at = args.IndexOf("--at") is int ai and >= 0 && ai + 1 < args.Count ? int.Parse(args[ai + 1]) : 150;   // seconds in
            for (int i = 0; i < at * World.TicksPerSecond; i++)
            {
                // 30 s before the shot, my first factory's units are told to rally in front of the base.
                if (i == (at - 30) * World.TicksPerSecond && W.Entities.Find(e => e.Kind == Kind.Factory && e.Owner == _me) is { } rf)
                    _session.Issue(new SetRally(_me, rf.Id, false, ToFix(new Vector3(-28, 0, 8))));
                if (args.Contains("--hq") && i == (at - 20) * World.TicksPerSecond && W.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == _me) is { } rh)
                    _session.Issue(new SetRally(_me, rh.Id, false, ToFix(new Vector3(-58, 0, -8))));   // a base rally in front of the HQ
                StepWorld();
                FadeBlasts((float)TickLength);
                _proj.Advance((float)TickLength);
            }
            _focus = Vector3.Zero;
            _distance = 60;
            _selected = W.Entities.Where(e => e.Owner == _me && e.Kind == Kind.Factory).OrderByDescending(e => e.HasRally).ThenByDescending(e => e.Levels.Sum()).FirstOrDefault()?.Id ?? -1;   // the rallying one, else the most upgraded
            var picked = W.Find(_selected);
            if (picked != null) { _focus = ToV3(picked.Pos) + new Vector3(4, 0, 3); _distance = 26; }   // close on it and its door
            if (args.Contains("--wide")) { _focus = new Vector3(0, 0, 4); _distance = 125; _noFog = true; UpdateFog(); }   // the whole map, fog lifted
            if (args.Contains("--fight"))   // close on the busiest fight, fog lifted
            {
                var hot = W.Entities.Where(e => e.Stats.IsUnit)
                    .OrderByDescending(e => W.Entities.Count(o => o.Owner != e.Owner && (o.Pos - e.Pos).LengthSq < Fix.FromInt(15 * 15)))
                    .FirstOrDefault();
                if (hot != null) { _focus = ToV3(hot.Pos); _distance = 24; _noFog = true; UpdateFog(); _selected = -1; }
            }
            if (args.IndexOf("--placing") is int pi and >= 0)   // something in hand under the screen's centre: a tank factory, or "outpost"
            {
                _placeAtCentre = true;
                _placing = pi + 1 < args.Count && args[pi + 1] == "outpost" ? Kind.Outpost : Kind.Tank;
                if (_placing == Kind.Outpost)   // look at the pad by my first extractor that has one
                    for (int d = 0; d < World.Deposits.Length; d++)
                        if (World.HasPad[d] && W.ExtractorOn(d) is { } rig && rig.Owner == _me)
                        { _focus = ToV3(World.Pads[d]); _distance = 40; _selected = -1; break; }
            }
            if (args.Contains("--hq") && W.Entities.Find(e => e.Kind == Kind.Hq && e.Owner == _me) is { } myHq)   // my HQ picked: its panel
            { _selected = myHq.Id; _focus = ToV3(myHq.Pos) + new Vector3(6, 0, 4); _distance = 34; }
            if (args.Contains("--menu")) SetMenu(true);   // the Escape menu open
            // --frames N: play on N frames first (shells in flight, tread marks laid), then take it.
            _shotFrames = args.IndexOf("--frames") is int fi and >= 0 && fi + 1 < args.Count ? int.Parse(args[fi + 1]) : 10;
        }
    }

    // ---- --lineup: the unit models side by side in both teams' colours, to judge their outlines at game distance ----

    static readonly string[] LineupUnits = { "scout", "tank", "artillery" };
    int _lineupFrame = -1;

    void BuildLineup()
    {
        for (int team = 0; team < 2; team++)
            for (int i = 0; i < LineupUnits.Length; i++)
            {
                var unit = UnitArt.Make(LineupUnits[i], TeamColor[team]);
                // Emblems at a spread of levels: scout bronze sword, tank silver sword + gold shield, artillery both gold.
                int[][] levels = { new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 3 } };
                UnitArt.AddEmblems(unit, LineupUnits[i], levels[i][0], levels[i][1]);
                AddChild(unit);
                unit.Position = new Vector3((i - 1) * 7, 0, team == 0 ? -3.5f : 3.5f);
                // Team 0 faces right (toward the enemy), team 1 left, both a little toward the camera.
                unit.Basis = Basis.LookingAt(new Vector3(team == 0 ? 1 : -1, 0, 0.35f).Normalized(), Vector3.Up);
            }
        // An outpost beside them, to judge its model.
        var post = UnitArt.Make("outpost", TeamColor[0]);
        AddChild(post);
        post.Position = new Vector3(14, 0, 3);
        post.Basis = Basis.LookingAt(Vector3.Right, Vector3.Up);
        // An extractor, and a deposit nobody mines yet.
        var rig = UnitArt.Make("extractor", TeamColor[1]);
        AddChild(rig);
        rig.Position = new Vector3(14, 0, -5);
        rig.Basis = Basis.LookingAt(Vector3.Left, Vector3.Up);
        var ore = UnitArt.Make("deposit", Neutral);
        AddChild(ore);
        ore.Position = new Vector3(21, 0, -1);
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
        string name = _lineupFrame == 10 ? "lineup_far" : _lineupFrame == 20 ? "lineup_near" : null;
        if (name == null) return;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://screenshots"));
        GetViewport().GetTexture().GetImage().SavePng($"res://screenshots/{name}.png");
        if (_lineupFrame == 10) { _distance = 40; _focus = new Vector3(3, 0, 3); }
        else GetTree().Quit();
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
        GetTree().Quit();
    }

    // ---- fog of war (the sim decides what each side sees: Entity.SeenBy; this draws it) -------------------------

    const int FogRes = 2;   // fog cells per world unit
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
        if (e.Owner == _me || e.Kind is Kind.Hq or Kind.Tower) return true;
        if (World.CanSee(_me, e)) { if (e.Stats.IsBuilding) _seenBuildings.Add(e.Id); return true; }
        return _seenBuildings.Contains(e.Id);
    }

    void StartMatch(ISession session)
    {
        foreach (var v in _views.Values) v.QueueFree();
        _views.Clear(); _prevPos.Clear(); _facing.Clear(); _aim.Clear(); _seenBuildings.Clear();
        _proj?.Clear(); _dust?.Clear(); _tracks?.Clear();
        _session = session;
        _me = session.LocalPlayer;
        _acc = 0;
        _autoThoughtAt = -1;
        _focus = ToV3(W.Players[_me].HqPos) + new Vector3(_me == 0 ? 20 : -20, 0, 0);
        if (_auto) _autoMe = new SimpleAi(_me, (ulong)GD.Randi());
        BuildRing();
        UpdateFog();
    }

    /// <summary>The build-range ring around my HQ.</summary>
    /// <summary>The build-range rings: round my HQ and each of my outposts (redrawn each frame: outposts come, go
    /// and grow their reach). They follow the ground.</summary>
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
        Circle(World.HqStart(_me), World.BuildRadius.ToFloat());
        foreach (var e in W.Entities)
        {
            if (e.Owner != _me) continue;
            if (e.Kind == Kind.Outpost) Circle(e.Pos, World.OutpostRadius(e).ToFloat());
            if (e.Kind != Kind.Scout) continue;
            // Placing an extractor: how near each scout it must go.
            if (_placing == Kind.Extractor) Circle(e.Pos, (World.DeployReach + e.Stats.Radius).ToFloat());
            // A picked scout: a ring round it, and where it's going.
            if (_scouts.Contains(e.Id))
            {
                Circle(e.Pos, 1.3f);
                if (e.HasWaypoint) { Circle(e.Waypoint, 0.5f); Line(e.Pos, e.Waypoint); }
            }
        }
        // Pads by my extractors with nothing on them yet: ringed and tied to their extractor, "an outpost goes here".
        for (int d = 0; d < World.Deposits.Length; d++)
        {
            if (!World.HasPad[d] || W.ExtractorOn(d) is not { } rig || rig.Owner != _me) continue;
            if (W.Entities.Exists(o => o.Kind == Kind.Outpost && !o.Dead && (o.Pos - World.Pads[d]).LengthSq < Fix.FromInt(4))) continue;
            Circle(World.Pads[d], 3.4f);
            Line(World.Deposits[d] + (World.Pads[d] - World.Deposits[d]).WithLength(Fix.FromInt(2)), World.Pads[d] - (World.Pads[d] - World.Deposits[d]).WithLength(Fix.FromInt(3)));
        }
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
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.07f, 0.08f, 0.1f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.7f),
            AmbientLightEnergy = 0.5f,
        };
        AddChild(new WorldEnvironment { Environment = env });

        // The house light (ai-tools docs/render_scene.md): sun from the upper left of the screen,
        // a little toward the viewer, 50 degrees up; a cool fill from the right.
        var sunFrom = new Vector3(-Mathf.Cos(Mathf.DegToRad(50)), Mathf.Sin(Mathf.DegToRad(50)), 0.35f).Normalized();
        AddChild(new DirectionalLight3D
        {
            Transform = new Transform3D(Basis.LookingAt(-sunFrom, Vector3.Up), Vector3.Zero),
            LightEnergy = 1.2f,
            ShadowEnabled = true,
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
        AddChild(_terrain.BuildGround(groundMat));
        var waterMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/water_fog.gdshader") };
        waterMat.SetShaderParameter("fog", _fogTexture);
        waterMat.SetShaderParameter("half_size", new Vector2(World.MapHalfX.ToFloat(), World.MapHalfZ.ToFloat()));
        AddChild(_terrain.BuildDecor(waterMat));
        // The metal deposits: an outcrop of ore on each, hidden while an extractor stands on it.
        for (int i = 0; i < World.Deposits.Length; i++)
        {
            var ore = UnitArt.Make("deposit", Neutral);
            ore.Position = ToV3(World.Deposits[i]);
            ore.Basis = new Basis(Vector3.Up, i * 1.7f).Scaled(Vector3.One * 1.4f);   // (bigger, to read from afar)
            AddChild(ore);
            _depositViews.Add(ore);
            if (!World.HasPad[i]) continue;
            // Its outpost pad: a hexagon of concrete with a lit rim, so it reads as "build here".
            var pad = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 2.7f, BottomRadius = 2.9f, Height = 0.5f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.43f, 0.45f), Roughness = 0.9f },
                Position = ToV3(World.Pads[i]),
            };
            pad.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 2.75f, BottomRadius = 2.75f, Height = 0.06f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.1f, 0.1f, 0.1f), EmissionEnabled = true, Emission = new Color(0.55f, 0.85f, 1f), EmissionEnergyMultiplier = 0.6f,
                },
                Position = new Vector3(0, 0.26f, 0), Scale = new Vector3(1, 1, 1),
            });
            pad.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 2.45f, BottomRadius = 2.45f, Height = 0.08f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.37f, 0.39f), Roughness = 0.9f },
                Position = new Vector3(0, 0.27f, 0),
            });
            AddChild(pad);
            _padViews[i] = pad;
        }

        _ring = new MeshInstance3D();
        AddChild(_ring);

        _tracerMat = Unshaded(Colors.White);
        _tracerMat.VertexColorUseAsAlbedo = true;
        _tracks = new Tracks { HeightAt = _terrain.SurfaceAt };
        AddChild(_tracks);
        _dust = new Dust();
        AddChild(_dust);
        _proj = new Projectiles { Dust = _dust };
        _proj.OnShellLands = at => { if (SeenAt(at)) Blast(at, World.StatsFor[(int)Kind.Artillery].Splash.ToFloat()); };
        AddChild(_proj);
        _rallyLine = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _rallyLine });

        _ghostMat = Unshaded(new Color(0.3f, 1f, 0.4f, 0.4f));
        _ghost = new MeshInstance3D { Mesh = MeshFor(Kind.Factory), MaterialOverride = _ghostMat, Visible = false };
        AddChild(_ghost);

        _camera = new Camera3D { Fov = 40, Far = 500 };
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
        for (int i = 0; i < _upgradeButtons.Length; i++)
        {
            var which = (Upgrade)i;
            var b = new Button { FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
            b.AddThemeFontSizeOverride("font_size", 17);
            b.Pressed += () => _session?.Issue(new UpgradeFactory(_me, _selected, which));
            column.AddChild(b);
            _upgradeButtons[i] = b;
        }
        _cancelQueueButton = new Button { FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        _cancelQueueButton.AddThemeFontSizeOverride("font_size", 17);
        _cancelQueueButton.Pressed += () => _session?.Issue(new CancelUpgrades(_me, _selected));
        column.AddChild(_cancelQueueButton);
        _rallyButton = new Button { Text = "Clear rally point", FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        _rallyButton.AddThemeFontSizeOverride("font_size", 17);
        _rallyButton.Pressed += () => _session?.Issue(new SetRally(_me, _selected, true, FixVec2.Zero));
        column.AddChild(_rallyButton);
        _sellButton = new Button { FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        _sellButton.AddThemeFontSizeOverride("font_size", 17);
        _sellButton.Pressed += () =>
        {
            // Two clicks: the first asks, the second (within a few seconds, same building) sells.
            if (_sellArmed == _selected && Time.GetTicksMsec() < _sellArmedUntil)
            {
                _session?.Issue(new Sell(_me, _selected));
                _sellArmed = -1;
                _selected = -1;
            }
            else { _sellArmed = _selected; _sellArmedUntil = Time.GetTicksMsec() + 3000; }
        };
        column.AddChild(_sellButton);
        // The HQ builds scouts: one at a time on a button (or Q), or on its own with auto on.
        _scoutButton = new Button { FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        _scoutButton.AddThemeFontSizeOverride("font_size", 17);
        _scoutButton.Pressed += () => _session?.Issue(new QueueScout(_me));
        column.AddChild(_scoutButton);
        _autoButton = new Button { FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left, ToggleMode = true };
        _autoButton.AddThemeFontSizeOverride("font_size", 17);
        _autoButton.Toggled += on => { if (W.Find(_selected) is { Kind: Kind.Hq } hq && hq.AutoScout != on) _session?.Issue(new SetAutoScout(_me, on)); };
        column.AddChild(_autoButton);
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
        BuildMenu(ui);
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
            Kind.Factory => new BoxMesh { Size = new Vector3(r * 1.7f, 2, r * 1.7f) },
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

    static string ModelOf(Kind k) => k.ToString().ToLowerInvariant();   // art/units/<name>.glb

    /// <summary>A unit is its model; a building is a box for now, a factory with a small model of what it makes on its roof.</summary>
    Node3D MakeView(Entity e)
    {
        if (e.Stats.IsUnit)
        {
            // A unit wears its upgrades as emblems on its deck (its levels are fixed when it's made).
            var unit = UnitArt.Make(ModelOf(e.Kind), TeamColor[e.Owner]);
            UnitArt.AddEmblems(unit, ModelOf(e.Kind), e.Level(Upgrade.Damage), e.Level(Upgrade.Armour));
            return unit;
        }
        // Buildings (tools/art/model_buildings.py): each factory kind its own building; all face the enemy's side,
        // so units roll out of the door.
        var name = e.Kind == Kind.Factory ? "factory_" + ModelOf(e.Makes) : ModelOf(e.Kind);
        var building = UnitArt.Make(name, TeamColor[e.Owner]);
        building.Basis = Basis.LookingAt(new Vector3(e.Owner == 0 ? 1 : -1, 0, 0), Vector3.Up);
        return building;
    }

    // ---- production cogs: one brass cog over a factory per production upgrade, spinning ------------------------

    readonly Dictionary<int, List<Node3D>> _cogs = new();
    StandardMaterial3D _brass;
    float _cogTurn;   // seconds the cogs have been turning (paused with the game)
    float _frameDt;

    static Node3D MakeCog(Material brass)
    {
        var cog = new Node3D();
        cog.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.16f, RadialSegments = 12 }, MaterialOverride = brass });
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            cog.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.16f, 0.2f) },
                MaterialOverride = brass,
                Position = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f,
                Rotation = new Vector3(0, -a, 0),
            });
        }
        cog.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.22f, RadialSegments = 6 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.18f, 0.15f) } });
        return cog;
    }

    void UpdateCogs(Entity f, Node3D view, float dt)
    {
        int level = f.Level(Upgrade.Production);
        if (!_cogs.TryGetValue(f.Id, out var cogs)) _cogs[f.Id] = cogs = new List<Node3D>();
        _brass ??= new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.64f, 0.26f), Metallic = 0.6f, Roughness = 0.4f };
        while (cogs.Count < level)
        {
            var cog = MakeCog(_brass);
            view.AddChild(cog);
            cogs.Add(cog);
        }
        // Face the camera (it looks down at 53 degrees from the south), side by side over the roof, neighbours
        // turning opposite ways like meshed gears; quicker the more the factory's been upgraded.
        var facing = new Basis(Vector3.Right, Mathf.DegToRad(53));
        float speed = 1.2f + 0.9f * level;
        var undo = view.Basis.Inverse();   // the building is turned to face the enemy; the cogs face the camera
        for (int i = 0; i < cogs.Count; i++)
        {
            float spin = (i % 2 == 0 ? 1 : -1) * _cogTurn * speed + (i % 2) * Mathf.Pi / 8;
            cogs[i].Basis = undo * facing * new Basis(Vector3.Up, spin);
            cogs[i].Position = undo * new Vector3((i - (cogs.Count - 1) / 2f) * 0.98f, 3.6f, 0);
        }
    }

    // ---- rally flags: where my factories' units gather -----------------------------------------------------------

    // ---- the unit each factory is building, digitising up at its door ------------------------------------------

    readonly Dictionary<int, Node3D> _ghosts = new();

    void UpdateBuildGhost(Entity f)
    {
        if (!_ghosts.TryGetValue(f.Id, out var ghost))
        {
            ghost = UnitArt.MakeBuilding(ModelOf(f.Makes), TeamColor[f.Owner]);
            AddChild(ghost);
            _ghosts[f.Id] = ghost;
            // Where the sim rolls the unit out (World.Produce), without its little sideways spread, facing the enemy.
            var toward = (W.Players[1 - f.Owner].HqPos - f.Pos).WithLength(f.Stats.Radius + World.StatsFor[(int)f.Makes].Radius + 1);
            var at = ToV3(f.Pos + toward);
            ghost.Position = at;
            ghost.Basis = Basis.LookingAt(new Vector3(toward.X.ToFloat(), 0, toward.Z.ToFloat()).Normalized(), Vector3.Up);
        }
        ghost.Visible = Shown(f) && W.Started && W.Winner < 0;
        UnitArt.SetBuilt(ghost, 1f - f.Timer / (float)World.BuildTicksOf(f));
    }

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

    /// <summary>My factory's flag where its units rally (none for the enemy's: you don't see their orders).</summary>
    void UpdateRallyFlag(Entity f)
    {
        _flags.TryGetValue(f.Id, out var flag);
        if (f.Owner != _me || !f.HasRally) { if (flag != null) flag.Visible = false; return; }
        if (flag == null) { flag = MakeFlag(TeamColor[_me]); AddChild(flag); _flags[f.Id] = flag; }
        flag.Visible = true;
        flag.Position = ToV3(f.Rally);
    }

    /// <summary>A line from the selected factory of mine to its flag.</summary>
    void DrawRallyLine(Entity selected)
    {
        _rallyLine.ClearSurfaces();
        if (selected is not { Kind: Kind.Factory or Kind.Hq, HasRally: true } || selected.Owner != _me) return;
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
    static float HeightOf(Kind k) => 0f;   // every model stands on its origin

    /// <summary>The height shots leave from: about each one's gun.</summary>
    static float MuzzleOf(Kind k) => k switch { Kind.Hq => 2.5f, Kind.Tower => 4.0f, Kind.Outpost => 1.2f, Kind.Scout => 0.8f, Kind.Tank => 1.35f, Kind.Artillery => 1.6f, _ => 1f };

    /// <summary>How far in front of its centre a gun's muzzle is (units face their target, so it is along that line).</summary>
    static float BarrelOf(Kind k) => k switch { Kind.Tank => 1.9f, Kind.Artillery => 2.2f, Kind.Scout => 0.7f, Kind.Tower => 1.2f, Kind.Outpost => 1.6f, _ => 0f };

    /// <summary>How quickly each kind swings round, in radians a second.</summary>
    static float TurnRateOf(Kind k) => k switch { Kind.Scout => 9f, Kind.Tank => 4f, Kind.Artillery => 2.5f, _ => 4f };

    // ---- loop --------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        MoveCamera((float)delta);

        if (_lineupFrame >= 0) { StepLineup(); return; }
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
        if (!W.Paused && W.Started) _cogTurn += _frameDt;
        Draw((float)(_acc / TickLength));
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
            var target = W.Find(s.TargetId);
            var aimAt = to + Vector3.Up * (target != null ? Mathf.Min(TopOf(target.Kind) * 0.4f, 2f) : 0.6f);
            if (s.By == Kind.Artillery) aimAt = to;   // shells burst on the ground
            _proj.Fire(from, aimAt, s.By, TeamColor[s.Owner], s.Flight / (float)World.TicksPerSecond);   // a shell lands when the sim lands it
        }
        return true;
    }

    /// <summary>A flash where a shell lands, as wide as its splash, fading over half a second.</summary>
    void Blast(Vector3 at, float radius)
    {
        var flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = radius, RadialSegments = 12, Rings = 4 },
            MaterialOverride = Unshaded(new Color(1f, 0.6f, 0.2f, 0.45f)),
            Position = at,
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
            node.Scale = Vector3.One * (0.6f + age);
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
            if (!_views.TryGetValue(e.Id, out var view))
            {
                view = MakeView(e);
                AddChild(view);
                _views[e.Id] = view;
            }
            var cur = ToV3(e.Pos);
            var prev = _prevPos.TryGetValue(e.Id, out var p) ? p : cur;
            var pos = prev.Lerp(cur, alpha);
            if (e.Stats.IsUnit)
            {
                var step = cur - prev;
                // Dust off the tracks while it moves (step is one tick's move).
                var flat = new Vector3(step.X, 0, step.Z);
                bool moving = flat.LengthSquared() > 1e-4f;
                if (!W.Paused && moving && Shown(e))
                {
                    float r = e.Stats.Radius.ToFloat();
                    _dust.Drive(e.Id, pos, flat.Normalized(), r * 1.1f, r * 0.7f, flat.Length() * World.TicksPerSecond, _frameDt);
                    // Tread marks under each track (the scout's wheels leave thinner, fainter ones).
                    bool wheels = e.Kind == Kind.Scout;
                    _tracks.Drive(e.Id, pos, flat.Normalized(), r * 0.55f, r * (wheels ? 0.3f : 0.48f), wheels ? 0.7f : 1f);
                }
                // Face where it's going; standing still, face what it's shooting at. It swings round, not snaps.
                var face = _facing.TryGetValue(e.Id, out var f) ? f : new Vector3(e.Owner == 0 ? 1 : -1, 0, 0);
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
            }
            view.Position = pos + Vector3.Up * HeightOf(e.Kind);
            view.Visible = Shown(e);
            if (e.Kind == Kind.Factory) UpdateCogs(e, view, _frameDt);
            if (e.Kind is Kind.Factory or Kind.Hq) UpdateRallyFlag(e);
            if (e.Kind == Kind.Factory) UpdateBuildGhost(e);
        }
        foreach (var id in _views.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _views[id].QueueFree();
            _views.Remove(id);
            _prevPos.Remove(id);
            _facing.Remove(id);
            _aim.Remove(id);
            _cogs.Remove(id);
            _dust.Forget(id);
            _tracks.Forget(id);
            if (_flags.Remove(id, out var gone)) gone.QueueFree();
            if (_ghosts.Remove(id, out var ghost)) ghost.QueueFree();
        }

        for (int i = 0; i < _depositViews.Count; i++)   // an outcrop shows until a (seen) extractor stands on it
            _depositViews[i].Visible = W.ExtractorOn(i) is not { } rig || !Shown(rig);
        BuildRing();
        DrawPlacement();
        DrawBarsAndSelection(alpha);
        DrawHud();
    }

    /// <summary>Roughly how high each thing stands, for putting its health bar just above it.</summary>
    static float TopOf(Kind k) => k switch
    {
        Kind.Hq => 5.4f, Kind.Factory => 4.4f, Kind.Tower => 5.6f, Kind.Outpost => 2.9f, Kind.Extractor => 4.2f,   // (a factory's bar clears its cogs)
        Kind.Scout => 2.6f, Kind.Tank => 2.1f, Kind.Artillery => 2.2f, _ => 2f,
    };

    void DrawBarsAndSelection(float alpha)
    {
        _overlay.Bars.Clear();
        _overlay.Badges.Clear();
        _overlay.Tags.Clear();
        // Over each of my bases: its factory slots in use, "2/3" (amber when full).
        foreach (var b in W.Entities)
        {
            if (b.Owner != _me || !World.IsBase(b)) continue;
            var over = ToV3(b.Pos) + Vector3.Up * (TopOf(b.Kind) + 1.6f);
            if (_camera.IsPositionBehind(over)) continue;
            int used = W.SlotsUsed(b), slots = World.SlotsOf(b);
            _overlay.Tags.Add((_camera.UnprojectPosition(over) - new Vector2(0, 30), $"{used}/{slots}",
                               used >= slots ? new Color(1f, 0.75f, 0.3f) : new Color(0.75f, 0.92f, 1f)));
        }
        var right = _camera.GlobalBasis.X;
        Entity selected = null;
        foreach (var e in W.Entities)
        {
            var cur = ToV3(e.Pos);
            var pos = _prevPos.TryGetValue(e.Id, out var p) && !W.Paused ? p.Lerp(cur, alpha) : cur;
            var top = pos + Vector3.Up * TopOf(e.Kind);
            if (!Shown(e)) continue;
            if (e.Id == _selected) { selected = e; _selRing.Position = pos + Vector3.Up * 0.08f; }
            // Factories float a sword and shield; units wear theirs on the deck (UnitArt.AddEmblems), so only rank floats.
            int sword = e.Stats.IsUnit ? 0 : e.Level(Upgrade.Damage), shield = e.Stats.IsUnit ? 0 : e.Level(Upgrade.Armour);
            if ((sword > 0 || shield > 0 || e.Rank > 0) && !_camera.IsPositionBehind(top))
                _overlay.Badges.Add((_camera.UnprojectPosition(top) - new Vector2(0, 14), sword, shield, e.Rank));
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
        ShowUpgrades(selected);
        ShowScoutButtons(selected);
        _rallyButton.Visible = selected.Kind is Kind.Factory or Kind.Hq && selected.Owner == _me && selected.HasRally;
        ShowSell(selected);
        _statsPanel.OffsetTop = -_statsPanel.GetCombinedMinimumSize().Y - 16;
        _statsPanel.Size = _statsPanel.GetCombinedMinimumSize();
    }

    static readonly string[] UpgradeEffect = { "15% quicker to build", "+25% damage", "+25% health" };
    // An outpost's three tracks: its Production track is reach (World.OutpostRadius), the others its own gun and armour.
    static readonly string[] OutpostTrack = { "Reach", "Guns", "Armour" };
    static readonly string[] OutpostEffect = { "+4 build reach and sight", "+25% gun damage", "+25% health" };

    /// <summary>The Sell button, for my factories and outposts: 75% back of all it cost, upgrades included.</summary>
    void ShowSell(Entity e)
    {
        _sellButton.Visible = W.CanSell(_me, e.Id, out _);
        if (!_sellButton.Visible) return;
        int value = World.SellValue(e);
        bool armed = _sellArmed == e.Id && Time.GetTicksMsec() < _sellArmedUntil;
        _sellButton.Text = armed ? $"Click again to sell for {value}" : $"Sell for {value}  ({World.SellBackPercent}% of {World.SpentOn(e)} spent)";
        _sellButton.Modulate = armed ? new Color(1f, 0.6f, 0.5f) : Colors.White;
        _sellButton.Disabled = !W.Started || W.Winner >= 0;
    }

    /// <summary>My HQ's scout buttons: build one now, and auto-build.</summary>
    void ShowScoutButtons(Entity e)
    {
        bool hq = e.Kind == Kind.Hq && e.Owner == _me;
        _scoutButton.Visible = _autoButton.Visible = hq;
        if (!hq) return;
        int have = W.ScoutCount(_me);
        _scoutButton.Text = $"Build a scout for {World.ScoutCost}  [Q]   ({have}/{World.MaxScouts}" + (e.ScoutQueue > 0 ? $", {e.ScoutQueue} coming)" : ")");
        _scoutButton.Disabled = !W.CanQueueScout(_me, out _) || !W.Started || W.Winner >= 0;
        _autoButton.SetPressedNoSignal(e.AutoScout);
        _autoButton.Text = e.AutoScout ? "Auto-build scouts: ON  (keeps you at " + World.MaxScouts + ")" : "Auto-build scouts: off";
        _autoButton.Disabled = W.Winner >= 0;
    }

    /// <summary>The three upgrade buttons, for one of my factories only.</summary>
    void ShowUpgrades(Entity e)
    {
        bool mine = e.Kind is Kind.Factory or Kind.Outpost && e.Owner == _me;
        for (int i = 0; i < _upgradeButtons.Length; i++)
        {
            var b = _upgradeButtons[i];
            b.Visible = mine;
            if (!mine) continue;
            var which = (Upgrade)i;
            int level = e.Level(which);
            bool post = e.Kind == Kind.Outpost;
            var name = post ? OutpostTrack[i] : which.ToString();
            var effect = post ? OutpostEffect[i] : UpgradeEffect[i];
            // Clicking buys it now, or queues it if the metal isn't there (or others are queued first).
            int queued = World.Queued(e, which), next = level + queued;
            var waiting = queued > 0 ? $"   (+{queued} queued)" : "";
            b.Text = next >= World.MaxUpgradeLevel
                ? $"{name}  {level}/{World.MaxUpgradeLevel}{waiting}" + (queued == 0 ? "   (max)" : "")
                : $"{name}  {level}/{World.MaxUpgradeLevel}{waiting}   "
                  + (W.CanUpgrade(_me, e.Id, which, out _) ? "buy" : "queue") + $" for {World.UpgradeCost(next + 1)}:  {effect}";
            b.Disabled = !W.CanQueueUpgrade(_me, e.Id, which, out _) || W.Winner >= 0;
        }
        _cancelQueueButton.Visible = mine && e.UpgradeQueue.Count > 0;
        if (_cancelQueueButton.Visible) _cancelQueueButton.Text = $"Cancel {e.UpgradeQueue.Count} queued upgrade" + (e.UpgradeQueue.Count > 1 ? "s" : "");
    }

    string Describe(Entity e)
    {
        var s = e.Stats;
        var lines = new List<string>
        {
            $"{e.Kind}  ({(e.Owner == _me ? "yours" : "enemy")})",
            e.Kind switch
            {
                Kind.Scout => e.Owner == _me
                    ? "Unarmed eyes. Finds deposits: an extractor can go\non a deposit beside it. Right-click to send it."
                    : "Unarmed eyes: it's scouting you.",
                Kind.Extractor => $"Mines its deposit: +{World.ExtractorIncome * World.TicksPerSecond} metal a second."
                    + (World.HasPad[e.Deposit] ? "\nThe hexagon pad beside it takes an outpost (3)." : ""),
                Kind.Tank => "Tough all-rounder.",
                Kind.Artillery => "Outranges everything and hits an area, but can't fire\nat anything close. Its shells take a while to land:\nanything moving gets out from under them.",
                Kind.Hq => "Lose it and you lose the match.",
                Kind.Tower => "Guards the base: long range and sight.",
                Kind.Outpost => "A sub-base: build factories inside its ring.",
                _ => $"Makes: {e.Makes}, one every {World.BuildTicksOf(e) / (float)World.TicksPerSecond:0.#} s",
            },
            $"Health  {Mathf.Max(e.Hp, 0)} / {e.MaxHp}",
        };
        if (s.Reload > 0)
        {
            float every = s.Reload / (float)World.TicksPerSecond;
            lines.Add($"Damage  {e.Damage} every {every:0.#} s  ({e.Damage / every:0.#} per s)");
            lines.Add($"Range  {s.Range}" + (s.MinRange.Raw > 0 ? $"   (none within {s.MinRange})" : ""));
            if (s.Splash.Raw > 0) lines.Add($"Splash  {s.Splash} wide");
        }
        if (s.IsUnit) lines.Add($"Speed  {s.Speed.ToFloat() * World.TicksPerSecond:0.#} per s");
        if (e.Height.ToFloat() > 0.5f)
            lines.Add($"High ground  +{Mathf.Min(e.Height.ToFloat(), 5f):0.#} sight, more range firing down");
        if (s.IsUnit && (e.Level(Upgrade.Damage) > 0 || e.Level(Upgrade.Armour) > 0))
            lines.Add($"Upgraded  damage {e.Level(Upgrade.Damage)}/3, armour {e.Level(Upgrade.Armour)}/3");
        if (World.IsBase(e) && e.Owner == _me)
            lines.Add($"Factory slots  {W.SlotsUsed(e)} / {World.SlotsOf(e)}");
        if (e.Kind == Kind.Hq && e.Owner == _me && e.ScoutQueue > 0)
            lines.Add($"Next scout in  {Mathf.CeilToInt(e.Timer / (float)World.TicksPerSecond)} s");
        if (e.Kind == Kind.Factory) lines.Add($"Next in  {Mathf.CeilToInt(e.Timer / (float)World.TicksPerSecond)} s");
        if (e.Kind == Kind.Factory && e.Owner == _me)
            lines.Add(e.HasRally ? "New units go to its flag first, then attack"
                : W.Entities.Exists(h => h.Kind == Kind.Hq && h.Owner == _me && h.HasRally) ? "New units go to the base flag first  (right-click for its own)"
                : "Units attack  (right-click the ground to set a rally point)");
        if (e.Kind == Kind.Hq && e.Owner == _me)
            lines.Add(e.HasRally ? "Base rally: new scouts, and units from factories\nwithout their own flag, gather at the flag"
                                 : "Right-click the ground to set a base rally point\n(for scouts, and factories without their own)");
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
        _ghost.Visible = _placing != null && _mouseGround.HasValue;
        _placeProblem = null;
        if (_ghost.Visible)
        {
            var at = _mouseGround.Value;
            bool ok;
            if (_placing == Kind.Extractor)
            {
                // It snaps onto the deposit under the mouse.
                _placeDeposit = NearestOf(World.Deposits, at);
                ok = W.CanPlaceExtractor(_me, _placeDeposit, out _placeProblem);
                if (_placeDeposit >= 0) at = ToV3(World.Deposits[_placeDeposit]);
            }
            else if (_placing == Kind.Outpost)
            {
                // It snaps onto the pad under the mouse (the pad beside a deposit).
                _placeDeposit = NearestOf(World.Pads, at, World.HasPad);
                ok = W.CanPlaceOutpost(_me, _placeDeposit, out _placeProblem);
                if (_placeDeposit >= 0) at = ToV3(World.Pads[_placeDeposit]);
            }
            else ok = W.CanPlaceFactory(_me, ToFix(at), _placing.Value, out _placeProblem);
            // The hologram of the building itself (the user, 2026-10-19: "it should give the outline of the model of
            // what it's building"), green where it can go and red where it can't.
            var name = _placing is Kind.Outpost or Kind.Extractor ? ModelOf(_placing.Value) : "factory_" + ModelOf(_placing.Value);
            if (_placeModel == null || _placeModelKey != (name, ok))
            {
                _placeModel?.QueueFree();
                _placeModel = PlacementModel(name, ok ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.25f, 0.2f));
                _placeModelKey = (name, ok);
            }
            _placeModel.Position = at;
        }
        if (_placeModel != null) _placeModel.Visible = _ghost.Visible;
        _ghost.Visible = false;   // (the plain box is only the fallback now)

        // Factories and outposts ordered during a pause, waiting for it to end.
        var held = W.HeldDuringPause.Where(c => c.Player == _me && c is PlaceFactory or PlaceOutpost or PlaceExtractor)
            .Select(c => c switch
            {
                PlaceFactory pf => ("factory_" + ModelOf(pf.Makes), pf.Pos),
                PlaceExtractor pe => ("extractor", World.Deposits[pe.Deposit]),
                _ => ("outpost", World.Pads[((PlaceOutpost)c).Deposit]),
            }).ToList();
        if (!held.Select(h => h.Item1).SequenceEqual(_heldGhostNames))
        {
            foreach (var g in _heldGhosts) g.QueueFree();
            _heldGhosts.Clear();
            _heldGhostNames.Clear();
            foreach (var (name, _) in held) { _heldGhosts.Add(PlacementModel(name, TeamColor[_me])); _heldGhostNames.Add(name); }
        }
        for (int i = 0; i < held.Count; i++) _heldGhosts[i].Position = ToV3(held[i].Item2);
    }

    Node3D _placeModel;
    (string, bool) _placeModelKey;
    int _placeDeposit = -1;   // placing an extractor or outpost: the deposit it would go on (or by)

    /// <summary>The index of the spot (a deposit, or a pad) nearest a point on the ground, if within a few paces; else -1.</summary>
    static int NearestOf(FixVec2[] spots, Vector3 at, bool[] only = null)
    {
        int best = -1;
        float bestD = 6f;
        for (int i = 0; i < spots.Length; i++)
        {
            if (only != null && !only[i]) continue;
            var d = new Vector2(spots[i].X.ToFloat() - at.X, spots[i].Z.ToFloat() - at.Z).Length();
            if (d < bestD) { best = i; bestD = d; }
        }
        return best;
    }
    readonly List<string> _heldGhostNames = new();

    /// <summary>A building's model as an unbuilt hologram, facing the enemy like the real thing.</summary>
    Node3D PlacementModel(string name, Color tint)
    {
        var model = UnitArt.MakeBuilding(name, tint);
        UnitArt.SetBuilt(model, -0.05f);   // (below zero, so even its foot is hologram, not solid)
        model.Basis = Basis.LookingAt(new Vector3(_me == 0 ? 1 : -1, 0, 0), Vector3.Up);
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
            if (e.Stats.IsUnit && Shown(e)) { if (e.Owner == _me) myUnits++; else theirUnits++; }   // (only enemies we can see)
            if (e.Kind == Kind.Hq) { if (e.Owner == _me) myHq = e.Hp; else theirHq = e.Hp; }
        }
        var menu = string.Join("   ", World.FactoryKinds.Select((k, i) => $"[{i + 1}] {k}")) + $" factory {World.FactoryCost}"
                   + $"   [3] Outpost {World.OutpostCost}   [4] Extractor {World.ExtractorCost}   [Q] Scout {World.ScoutCost}   [E] Pick scouts"
                   + "\nExpand: send a scout to a deposit, build an extractor on it, then an outpost on the pad beside it";
        var placingWhat = _placing switch
        {
            Kind.Outpost => "an outpost (on the pad beside one of your extractors)",
            Kind.Extractor => "an extractor (on a deposit in a ring or beside a scout)",
            _ => _placing + " factory",
        };
        var building = _placing == null
            ? menu
            : $"Placing {placingWhat}: click to build, Esc to cancel" + (_placeProblem != null ? $"   ({_placeProblem})" : "");
        _hud.Text =
            $"Metal {me.Metal}   +{W.IncomeOf(_me) * World.TicksPerSecond}/s\n" +
            building + "\n" +
            $"Army {W.ArmyCount(_me)}/{World.MaxArmy}   units seen {myUnits} vs {theirUnits}\n" +
            $"HQ {myHq} vs {theirHq}\n" +
            $"Pauses left {me.PausesLeft}  [P]\n" +
            $"t {W.Tick / World.TicksPerSecond}s   sum {W.Checksum() & 0xFFFF:X4}";
        if (_session is LockstepSession ls)
            _hud.Text += $"\nLAN {(_me == 0 ? "host" : "client")}   checks passed {ls.VerifiedTicks}" + (_waiting ? "   waiting..." : "");

        if (_session.Problem != null)
            _banner.Text = _session.Problem;
        else if (W.Winner >= 0)
            _banner.Text = (W.Winner == _me ? "Victory" : "Defeat") + (_session is LocalSession ? "  -  R to play again" : "");
        else if (!W.Started)
            _banner.Text = W.Entities.Exists(e => e.Kind == Kind.Factory && e.Owner == _me)
                ? "Waiting for the enemy to place their first factory..."
                : "Place your first factory: press 1 (tank) or 2 (artillery), then click near your HQ\nThe battle starts once both sides have one";
        else if (W.Paused)
        {
            var secs = Mathf.CeilToInt(W.PauseTicksLeft / (float)World.TicksPerSecond);
            var who = W.PausedBy == _me ? "You" : "Enemy";
            _banner.Text = $"PAUSED by {who}  {secs}s\nOrders apply when play resumes" + (W.PausedBy == _me ? "  -  Enter to resume" : "");
        }
        else _banner.Text = "";
    }

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
        _restartButton = MenuButton("Restart match", () => { SetMenu(false); StartMatch(new LocalSession((ulong)GD.Randi())); });
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
        foreach (Node n in new Node[] { _dust, _proj, _tracks }) if (n != null) n.ProcessMode = effects;
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
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(25, _distance * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(160, _distance * 1.1f);
            else if (mb.ButtonIndex == MouseButton.Left && _mouseGround.HasValue)
            {
                if (_placing != null)
                {
                    // One building per build: placing it ends build mode. A bad spot keeps it, to try again.
                    if (_placeProblem == null)
                    {
                        var at = ToFix(_mouseGround.Value);
                        _session.Issue(_placing switch
                        {
                            Kind.Outpost => new PlaceOutpost(_me, _placeDeposit),
                            Kind.Extractor => new PlaceExtractor(_me, _placeDeposit),
                            _ => new PlaceFactory(_me, at, _placing.Value),
                        });
                        _placing = null;
                    }
                }
                else _dragFrom = mb.Position;   // a click or a box: decided when the button comes up
            }
            else if (mb.ButtonIndex == MouseButton.Right && _mouseGround.HasValue)
            {
                // Cancels build mode; sends the picked scouts; or, with one of my factories selected, sets its rally.
                if (_placing != null) _placing = null;
                else if (_scouts.Count > 0 && W.Winner < 0)
                    _session.Issue(new MoveUnits(_me, _scouts.OrderBy(id => id).ToArray(), ToFix(_mouseGround.Value)));
                else if (W.Find(_selected) is { Kind: Kind.Factory or Kind.Hq } f && f.Owner == _me && W.Winner < 0)
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
                    if (e.Kind == Kind.Scout && e.Owner == _me && !_camera.IsPositionBehind(ToV3(e.Pos)) && box.HasPoint(_camera.UnprojectPosition(ToV3(e.Pos))))
                        _scouts.Add(e.Id);
                _selected = _scouts.Count > 0 ? _scouts.Min() : -1;
            }
            else if (_mouseGround.HasValue)
            {
                var hit = EntityAt(_mouseGround.Value);
                if (hit is { Kind: Kind.Scout } s && s.Owner == _me)
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
        else if (ev is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode >= Key.Key1 && k.Keycode < Key.Key1 + World.FactoryKinds.Length && W.Winner < 0)
                _placing = World.FactoryKinds[k.Keycode - Key.Key1];
            else if (k.Keycode == Key.Key3 && W.Winner < 0 && W.Started) _placing = Kind.Outpost;
            else if (k.Keycode == Key.Key4 && W.Winner < 0 && W.Started) _placing = Kind.Extractor;
            else if (k.Keycode == Key.Q && W.Winner < 0 && W.Started) _session.Issue(new QueueScout(_me));
            else if (k.Keycode == Key.E)   // pick all my scouts
            {
                _scouts.Clear();
                foreach (var e in W.Entities) if (e.Kind == Kind.Scout && e.Owner == _me) _scouts.Add(e.Id);
                _selected = _scouts.Count > 0 ? _scouts.Min() : -1;
            }
            else if (k.Keycode == Key.Escape)
            {
                // One thing per press: stop placing, else let go of what's picked, else open the menu.
                if (_placing != null) _placing = null;
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
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) pan.Z -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) pan.Z += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) pan.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) pan.X += 1;
        _focus += pan * _distance * 0.8f * dt;
        _focus.X = Mathf.Clamp(_focus.X, -World.MapHalfX.ToFloat(), World.MapHalfX.ToFloat());
        _focus.Z = Mathf.Clamp(_focus.Z, -World.MapHalfZ.ToFloat(), World.MapHalfZ.ToFloat());

        // Looks down at 53 degrees, like the house render camera.
        var pitch = Mathf.DegToRad(53);
        var offset = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch)) * _distance;
        _camera.Transform = new Transform3D(Basis.LookingAt(-offset, Vector3.Up), _focus + offset);
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
