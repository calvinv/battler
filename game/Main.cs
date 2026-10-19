using System.Collections.Generic;
using System.Linq;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// Runs a match against the AI: steps the simulation at a fixed rate, draws it (interpolating
/// between ticks), and turns the player's clicks and keys into commands.
///
/// Controls: left click places a factory near your HQ, WASD/arrows pan, wheel zooms,
/// P calls a tactical pause, Enter ends your pause early, R restarts once someone has won.
/// </summary>
public partial class Main : Node3D
{
    const int Me = 0, Ai = 1;
    const double TickLength = 1.0 / World.TicksPerSecond;

    static readonly Color[] TeamColor = { new(0.25f, 0.55f, 1f), new(1f, 0.42f, 0.18f) };

    World _world;
    SimpleAi _ai;
    readonly List<Command> _myCommands = new();
    double _acc;

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

    // Debug: "-- --shot" plays both sides by AI for 60 s of game time, saves screenshots/shot.png and quits.
    SimpleAi _autoMe;
    int _shotFrames = -1;

    public override void _Ready()
    {
        BuildScene();
        NewMatch();
        if (OS.GetCmdlineUserArgs().Contains("--shot"))
        {
            _autoMe = new SimpleAi(Me, 7);
            for (int i = 0; i < 60 * World.TicksPerSecond; i++) StepWorld();
            _focus = Vector3.Zero;
            _distance = 110;
            _shotFrames = 10;
        }
    }

    void TakeShotWhenReady()
    {
        if (_shotFrames < 0 || --_shotFrames > 0) return;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://screenshots"));
        GetViewport().GetTexture().GetImage().SavePng("res://screenshots/shot.png");
        GetTree().Quit();
    }

    void NewMatch()
    {
        foreach (var v in _views.Values) v.QueueFree();
        _views.Clear(); _prevPos.Clear(); _facing.Clear();
        var seed = (ulong)GD.Randi();
        _world = new World(seed);
        _ai = new SimpleAi(Ai, seed ^ 0xA1);
        _myCommands.Clear();
        _acc = 0;
        _focus = ToV3(_world.Players[Me].HqPos) + new Vector3(20, 0, 0);
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
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.24f, 0.2f) },
        };
        AddChild(ground);

        // Build-range ring around my HQ.
        var ring = new ImmediateMesh();
        ring.SurfaceBegin(Mesh.PrimitiveType.LineStrip, Unshaded(new Color(TeamColor[Me], 0.5f)));
        var c = World.HqStart(Me);
        for (int i = 0; i <= 64; i++)
        {
            var a = Mathf.Tau * i / 64;
            ring.SurfaceAddVertex(ToV3(c) + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * World.BuildRadius.ToFloat());
        }
        ring.SurfaceEnd();
        AddChild(new MeshInstance3D { Mesh = ring });

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

        _acc += delta;
        int steps = 0;
        while (_acc >= TickLength && steps++ < 5)
        {
            _acc -= TickLength;
            StepWorld();
        }
        if (_acc > TickLength) _acc = 0;   // fell far behind; don't spiral

        Draw((float)(_acc / TickLength));
        TakeShotWhenReady();
    }

    void StepWorld()
    {
        foreach (var e in _world.Entities) _prevPos[e.Id] = ToV3(e.Pos);

        var cmds = new List<Command>(_myCommands);
        _myCommands.Clear();
        _ai.Think(_world, cmds.Add);
        _autoMe?.Think(_world, cmds.Add);
        // Stable order by player: every machine in a match must apply commands identically.
        _world.Step(cmds.OrderBy(c => c.Player).ToList());

        _tracers.ClearSurfaces();
        if (_world.Shots.Count > 0)
        {
            _tracers.SurfaceBegin(Mesh.PrimitiveType.Lines, _tracerMat);
            foreach (var s in _world.Shots)
            {
                var col = TeamColor[s.Owner].Lightened(0.5f);
                _tracers.SurfaceSetColor(col);
                _tracers.SurfaceAddVertex(ToV3(s.From) + Vector3.Up * 1.2f);
                _tracers.SurfaceSetColor(col);
                _tracers.SurfaceAddVertex(ToV3(s.To) + Vector3.Up * 0.8f);
            }
            _tracers.SurfaceEnd();
        }
    }

    void Draw(float alpha)
    {
        if (_world.Paused) alpha = 1;
        var alive = new HashSet<int>();
        foreach (var e in _world.Entities)
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
        var canPlay = _world.Winner < 0;
        _ghost.Visible = canPlay && _mouseGround.HasValue;
        if (_ghost.Visible)
        {
            var ok = _world.CanPlaceFactory(Me, ToFix(_mouseGround.Value), out _);
            _ghostMat.AlbedoColor = ok ? new Color(0.3f, 1f, 0.4f, 0.4f) : new Color(1f, 0.25f, 0.2f, 0.4f);
            _ghost.Position = _mouseGround.Value + Vector3.Up * HeightOf(Kind.Factory);
        }

        // Factories ordered during a pause, waiting for it to end.
        var held = _world.HeldDuringPause.OfType<PlaceFactory>().Where(c => c.Player == Me).ToList();
        while (_heldGhosts.Count < held.Count)
        {
            var g = new MeshInstance3D { Mesh = MeshFor(Kind.Factory), MaterialOverride = Unshaded(new Color(TeamColor[Me], 0.35f)) };
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
        var me = _world.Players[Me];
        int myMechs = 0, theirMechs = 0;
        int myHq = 0, theirHq = 0;
        foreach (var e in _world.Entities)
        {
            if (e.Kind == Kind.Mech) { if (e.Owner == Me) myMechs++; else theirMechs++; }
            if (e.Kind == Kind.Hq) { if (e.Owner == Me) myHq = e.Hp; else theirHq = e.Hp; }
        }
        _hud.Text =
            $"Metal {me.Metal}   (factory {World.StatsFor[(int)Kind.Factory].Cost})\n" +
            $"Mechs {myMechs} vs {theirMechs}\n" +
            $"HQ {myHq} vs {theirHq}\n" +
            $"Pauses left {me.PausesLeft}  [P]\n" +
            $"t {_world.Tick / World.TicksPerSecond}s   sum {_world.Checksum() & 0xFFFF:X4}";

        if (_world.Winner >= 0)
            _banner.Text = (_world.Winner == Me ? "Victory" : "Defeat") + "  -  R to play again";
        else if (_world.Paused)
        {
            var secs = Mathf.CeilToInt(_world.PauseTicksLeft / (float)World.TicksPerSecond);
            var who = _world.PausedBy == Me ? "You" : "Enemy";
            _banner.Text = $"PAUSED by {who}  {secs}s\nOrders apply when play resumes" + (_world.PausedBy == Me ? "  -  Enter to resume" : "");
        }
        else _banner.Text = "";
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(25, _distance * 0.9f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(160, _distance * 1.1f);
            else if (mb.ButtonIndex == MouseButton.Left && _mouseGround.HasValue && _world.Winner < 0)
                _myCommands.Add(new PlaceFactory(Me, ToFix(_mouseGround.Value)));
        }
        else if (ev is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.P) _myCommands.Add(new Pause(Me));
            else if (k.Keycode == Key.Enter) _myCommands.Add(new Resume(Me));
            else if (k.Keycode == Key.R && _world.Winner >= 0) NewMatch();
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
