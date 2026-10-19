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
/// Controls: left click places a factory near your HQ, WASD/arrows pan, wheel zooms,
/// P calls a tactical pause, Enter ends your pause early, R restarts once someone has won (vs AI).
///
/// Command line (after "--"): nothing = vs AI; --host = wait for a LAN opponent;
/// --join [ip] = join a host (default 127.0.0.1); --auto = an AI plays your side;
/// --shot = 60 s of AI vs AI, save screenshots/shot.png, quit.
/// </summary>
public partial class Main : Node3D
{
    const double TickLength = 1.0 / World.TicksPerSecond;

    static readonly Color[] TeamColor = { new(0.25f, 0.55f, 1f), new(1f, 0.42f, 0.18f) };

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

    readonly Dictionary<int, MeshInstance3D> _views = new();
    readonly Dictionary<int, Vector3> _prevPos = new();
    readonly Dictionary<int, Vector3> _facing = new();
    readonly Dictionary<(Kind, int), Mesh> _meshes = new();
    readonly Dictionary<(Kind, int), Material> _materials = new();

    Camera3D _camera;
    Vector3 _focus;
    float _distance = 90;
    MeshInstance3D _ghost;
    StandardMaterial3D _ghostMat;
    readonly List<MeshInstance3D> _heldGhosts = new();
    ImmediateMesh _tracers;
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
    MeshInstance3D _ring;

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
            for (int i = 0; i < 60 * World.TicksPerSecond; i++) StepWorld();
            _focus = Vector3.Zero;
            _distance = 110;
            _shotFrames = 10;
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
                AddChild(unit);
                unit.Position = new Vector3((i - 1) * 7, 0, team == 0 ? -3.5f : 3.5f);
                // Team 0 faces right (toward the enemy), team 1 left, both a little toward the camera.
                unit.Basis = Basis.LookingAt(new Vector3(team == 0 ? 1 : -1, 0, 0.35f).Normalized(), Vector3.Up);
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
        if (_lineupFrame == 10) _distance = 28;
        else GetTree().Quit();
    }

    public override void _ExitTree()
    {
        _listener?.Stop();
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

    void StartMatch(ISession session)
    {
        foreach (var v in _views.Values) v.QueueFree();
        _views.Clear(); _prevPos.Clear(); _facing.Clear();
        _session = session;
        _me = session.LocalPlayer;
        _acc = 0;
        _autoThoughtAt = -1;
        _focus = ToV3(W.Players[_me].HqPos) + new Vector3(_me == 0 ? 20 : -20, 0, 0);
        if (_auto) _autoMe = new SimpleAi(_me, (ulong)GD.Randi());
        BuildRing();
    }

    /// <summary>The build-range ring around my HQ.</summary>
    void BuildRing()
    {
        var ring = new ImmediateMesh();
        ring.SurfaceBegin(Mesh.PrimitiveType.LineStrip, Unshaded(new Color(TeamColor[_me], 0.5f)));
        var c = ToV3(World.HqStart(_me));
        for (int i = 0; i <= 64; i++)
        {
            var a = Mathf.Tau * i / 64;
            ring.SurfaceAddVertex(c + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * World.BuildRadius.ToFloat());
        }
        ring.SurfaceEnd();
        _ring.Mesh = ring;
    }

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

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(World.MapHalfX.ToFloat() * 2, World.MapHalfZ.ToFloat() * 2) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.46f, 0.41f, 0.33f) },   // dusty wasteland: olive machines stand out on it
        };
        AddChild(ground);

        _ring = new MeshInstance3D();
        AddChild(_ring);

        _tracers = new ImmediateMesh();
        _tracerMat = Unshaded(Colors.White);
        _tracerMat.VertexColorUseAsAlbedo = true;
        AddChild(new MeshInstance3D { Mesh = _tracers });

        _ghostMat = Unshaded(new Color(0.3f, 1f, 0.4f, 0.4f));
        _ghost = new MeshInstance3D { Mesh = MeshFor(Kind.Factory), MaterialOverride = _ghostMat, Visible = false };
        AddChild(_ghost);

        _camera = new Camera3D { Fov = 40, Far = 500 };
        AddChild(_camera);

        var ui = new CanvasLayer();
        AddChild(ui);
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
            Kind.Factory => new BoxMesh { Size = new Vector3(r * 1.7f, 2, r * 1.7f) },
            _ => new BoxMesh { Size = new Vector3(r * 1.6f, 1.6f, r * 1.2f) },
        };
        _meshes[(k, 0)] = m;
        return m;
    }

    Material MaterialFor(Kind k, int owner)
    {
        if (_materials.TryGetValue((k, owner), out var m)) return m;
        var col = TeamColor[owner];
        if (k != Kind.Mech) col = col.Darkened(0.35f);
        m = new StandardMaterial3D { AlbedoColor = col, Roughness = 0.8f };
        _materials[(k, owner)] = m;
        return m;
    }

    static Vector3 ToV3(FixVec2 p) => new(p.X.ToFloat(), 0, p.Z.ToFloat());

    static float HeightOf(Kind k) => k switch { Kind.Hq => 1.5f, Kind.Factory => 1f, _ => 0.8f };

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

        _acc += delta;
        int steps = 0;
        _waiting = false;
        while (_acc >= TickLength && steps++ < 5)
        {
            if (!StepWorld()) { _waiting = true; _acc = TickLength; break; }   // try again next frame
            _acc -= TickLength;
        }
        if (_acc > TickLength) _acc = 0;   // fell far behind; don't spiral

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

        _tracers.ClearSurfaces();
        if (W.Shots.Count > 0)
        {
            _tracers.SurfaceBegin(Mesh.PrimitiveType.Lines, _tracerMat);
            foreach (var s in W.Shots)
            {
                var col = TeamColor[s.Owner].Lightened(0.5f);
                _tracers.SurfaceSetColor(col);
                _tracers.SurfaceAddVertex(ToV3(s.From) + Vector3.Up * 1.2f);
                _tracers.SurfaceSetColor(col);
                _tracers.SurfaceAddVertex(ToV3(s.To) + Vector3.Up * 0.8f);
            }
            _tracers.SurfaceEnd();
        }
        return true;
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
                view = new MeshInstance3D { Mesh = MeshFor(e.Kind), MaterialOverride = MaterialFor(e.Kind, e.Owner) };
                AddChild(view);
                _views[e.Id] = view;
            }
            var cur = ToV3(e.Pos);
            var prev = _prevPos.TryGetValue(e.Id, out var p) ? p : cur;
            var pos = prev.Lerp(cur, alpha);
            if (e.Kind == Kind.Mech)
            {
                var step = cur - prev;
                if (step.LengthSquared() > 1e-4f) _facing[e.Id] = step.Normalized();
                var face = _facing.TryGetValue(e.Id, out var f) ? f : new Vector3(e.Owner == 0 ? 1 : -1, 0, 0);
                view.Basis = Basis.LookingAt(face, Vector3.Up);
            }
            view.Position = pos + Vector3.Up * HeightOf(e.Kind);
        }
        foreach (var id in _views.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _views[id].QueueFree();
            _views.Remove(id);
            _prevPos.Remove(id);
            _facing.Remove(id);
        }

        DrawPlacement();
        DrawHud();
    }

    void DrawPlacement()
    {
        _mouseGround = GroundUnderMouse();
        var canPlay = W.Winner < 0;
        _ghost.Visible = canPlay && _mouseGround.HasValue;
        if (_ghost.Visible)
        {
            var ok = W.CanPlaceFactory(_me, ToFix(_mouseGround.Value), out _);
            _ghostMat.AlbedoColor = ok ? new Color(0.3f, 1f, 0.4f, 0.4f) : new Color(1f, 0.25f, 0.2f, 0.4f);
            _ghost.Position = _mouseGround.Value + Vector3.Up * HeightOf(Kind.Factory);
        }

        // Factories ordered during a pause, waiting for it to end.
        var held = W.HeldDuringPause.OfType<PlaceFactory>().Where(c => c.Player == _me).ToList();
        while (_heldGhosts.Count < held.Count)
        {
            var g = new MeshInstance3D { Mesh = MeshFor(Kind.Factory), MaterialOverride = Unshaded(new Color(TeamColor[_me], 0.35f)) };
            AddChild(g);
            _heldGhosts.Add(g);
        }
        for (int i = 0; i < _heldGhosts.Count; i++)
        {
            _heldGhosts[i].Visible = i < held.Count;
            if (i < held.Count) _heldGhosts[i].Position = ToV3(held[i].Pos) + Vector3.Up * HeightOf(Kind.Factory);
        }
    }

    void DrawHud()
    {
        var me = W.Players[_me];
        int myMechs = 0, theirMechs = 0;
        int myHq = 0, theirHq = 0;
        foreach (var e in W.Entities)
        {
            if (e.Kind == Kind.Mech) { if (e.Owner == _me) myMechs++; else theirMechs++; }
            if (e.Kind == Kind.Hq) { if (e.Owner == _me) myHq = e.Hp; else theirHq = e.Hp; }
        }
        _hud.Text =
            $"Metal {me.Metal}   (factory {World.StatsFor[(int)Kind.Factory].Cost})\n" +
            $"Mechs {myMechs} vs {theirMechs}\n" +
            $"HQ {myHq} vs {theirHq}\n" +
            $"Pauses left {me.PausesLeft}  [P]\n" +
            $"t {W.Tick / World.TicksPerSecond}s   sum {W.Checksum() & 0xFFFF:X4}";
        if (_session is LockstepSession ls)
            _hud.Text += $"\nLAN {(_me == 0 ? "host" : "client")}   checks passed {ls.VerifiedTicks}" + (_waiting ? "   waiting..." : "");

        if (_session.Problem != null)
            _banner.Text = _session.Problem;
        else if (W.Winner >= 0)
            _banner.Text = (W.Winner == _me ? "Victory" : "Defeat") + (_session is LocalSession ? "  -  R to play again" : "");
        else if (W.Paused)
        {
            var secs = Mathf.CeilToInt(W.PauseTicksLeft / (float)World.TicksPerSecond);
            var who = W.PausedBy == _me ? "You" : "Enemy";
            _banner.Text = $"PAUSED by {who}  {secs}s\nOrders apply when play resumes" + (W.PausedBy == _me ? "  -  Enter to resume" : "");
        }
        else _banner.Text = "";
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent ev)
    {
        if (_session == null) return;
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(25, _distance * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(160, _distance * 1.1f);
            else if (mb.ButtonIndex == MouseButton.Left && _mouseGround.HasValue && W.Winner < 0)
                _session.Issue(new PlaceFactory(_me, ToFix(_mouseGround.Value)));
        }
        else if (ev is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.P) _session.Issue(new Pause(_me));
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
        var mouse = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return null;
        var t = -from.Y / dir.Y;
        if (t < 0) return null;
        var hit = from + dir * t;
        if (Mathf.Abs(hit.X) > World.MapHalfX.ToFloat() || Mathf.Abs(hit.Z) > World.MapHalfZ.ToFloat()) return null;
        return hit;
    }
}
