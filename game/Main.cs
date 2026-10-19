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
/// Controls: 1-3 pick a factory (scout, tank, artillery) and a click places it near your HQ (Esc or right click cancels);
/// otherwise a click selects a unit or building to see its stats (Esc clears). WASD/arrows pan, wheel zooms,
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

    readonly Dictionary<int, Node3D> _views = new();
    Kind? _placing;                // build mode: the factory a click places (keys 1-3; Esc or a placement ends it)
    int _selected = -1;            // the entity whose stats are shown (click it; Esc clears)
    Overlay _overlay;
    PanelContainer _statsPanel;
    Label _stats;
    MeshInstance3D _selRing;
    readonly Button[] _upgradeButtons = new Button[3];   // in the stats panel, for my selected factory
    string _placeProblem;          // why the factory can't go under the mouse
    readonly List<(MeshInstance3D Node, float Age)> _blasts = new();   // artillery impacts, fading
    readonly Dictionary<int, Vector3> _prevPos = new();
    readonly Dictionary<int, Vector3> _facing = new();
    readonly Dictionary<(Kind, int), Mesh> _meshes = new();
    readonly Dictionary<(Kind, int), Material> _materials = new();

    Camera3D _camera;
    Vector3 _focus;
    float _distance = 70;
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
            for (int i = 0; i < 150 * World.TicksPerSecond; i++) { StepWorld(); FadeBlasts((float)TickLength); }
            _focus = Vector3.Zero;
            _distance = 60;
            _selected = W.Entities.Where(e => e.Owner == _me && e.Kind == Kind.Factory).OrderByDescending(e => e.Levels.Sum()).FirstOrDefault()?.Id ?? -1;   // the most upgraded: its panel, cogs and badges
            var picked = W.Find(_selected);
            if (picked != null) { _focus = ToV3(picked.Pos) + new Vector3(4, 0, 5); _distance = 30; }   // close on it
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
                // Emblems at a spread of levels: scout bronze sword, tank silver sword + gold shield, artillery both gold.
                int[][] levels = { new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 3 } };
                UnitArt.AddEmblems(unit, LineupUnits[i], levels[i][0], levels[i][1]);
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
        UnitArt.Release();
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
    byte[] _fogData;        // what's seen, hard edged: for SeenAt
    byte[] _fogSoft;        // the same blurred a little: what the ground shows
    Image _fogImage;
    ImageTexture _fogTexture;
    readonly HashSet<int> _seenBuildings = new();   // enemy buildings stay on the map once spotted

    /// <summary>Repaints the fog texture from my side's vision circles.</summary>
    void UpdateFog()
    {
        System.Array.Clear(_fogData);
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
        _views.Clear(); _prevPos.Clear(); _facing.Clear(); _seenBuildings.Clear();
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
        };
        // Dusty wasteland (olive machines stand out on it), darkened where we can't see.
        _fogData = new byte[FogW * FogH];
        _fogSoft = new byte[FogW * FogH];
        System.Array.Fill(_fogData, (byte)255);
        System.Array.Fill(_fogSoft, (byte)255);
        _fogImage = Image.CreateFromData(FogW, FogH, false, Image.Format.L8, _fogData);
        _fogTexture = ImageTexture.CreateFromImage(_fogImage);
        var groundMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/ground_fog.gdshader") };
        groundMat.SetShaderParameter("fog", _fogTexture);
        groundMat.SetShaderParameter("half_size", new Vector2(World.MapHalfX.ToFloat(), World.MapHalfZ.ToFloat()));
        ground.MaterialOverride = groundMat;
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
        var box = new MeshInstance3D { Mesh = MeshFor(e.Kind), MaterialOverride = MaterialFor(e.Kind, e.Owner) };
        if (e.Kind == Kind.Factory)
        {
            var sign = UnitArt.Make(ModelOf(e.Makes), TeamColor[e.Owner]);
            sign.Scale = Vector3.One * 0.7f;
            sign.Position = Vector3.Up * 1f;   // on the roof (the box's middle is its origin)
            sign.Basis = Basis.LookingAt(new Vector3(e.Owner == 0 ? 1 : -1, 0, 0.4f).Normalized(), Vector3.Up).Scaled(Vector3.One * 0.7f);
            box.AddChild(sign);
        }
        return box;
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
        for (int i = 0; i < cogs.Count; i++)
        {
            float spin = (i % 2 == 0 ? 1 : -1) * _cogTurn * speed + (i % 2) * Mathf.Pi / 8;
            cogs[i].Basis = facing * new Basis(Vector3.Up, spin);
            cogs[i].Position = new Vector3((i - (cogs.Count - 1) / 2f) * 0.98f, 2.6f, 0);
        }
    }

    static Vector3 ToV3(FixVec2 p) => new(p.X.ToFloat(), 0, p.Z.ToFloat());

    /// <summary>Where a view's origin sits above the ground: boxes are centred, unit models stand on y = 0.</summary>
    static float HeightOf(Kind k) => k switch { Kind.Hq => 1.5f, Kind.Factory => 1f, Kind.Tower => 2.25f, _ => 0f };

    /// <summary>The height shots leave from: about each one's gun.</summary>
    static float MuzzleOf(Kind k) => k switch { Kind.Hq => 3f, Kind.Tower => 4.2f, Kind.Scout => 0.8f, Kind.Tank => 1.35f, Kind.Artillery => 1.6f, _ => 1f };

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

        _tracers.ClearSurfaces();
        var shots = W.Shots.Where(s => SeenAt(ToV3(s.From)) || SeenAt(ToV3(s.To))).ToList();   // not from fog to fog
        if (shots.Count > 0)
        {
            _tracers.SurfaceBegin(Mesh.PrimitiveType.Lines, _tracerMat);
            foreach (var s in shots)
            {
                var col = TeamColor[s.Owner].Lightened(0.5f);
                var from = ToV3(s.From) + Vector3.Up * MuzzleOf(s.By);
                var to = ToV3(s.To) + Vector3.Up * 0.6f;
                // Artillery lobs its shell in an arc; everything else fires straight.
                int segments = s.By == Kind.Artillery ? 10 : 1;
                float peak = s.By == Kind.Artillery ? from.DistanceTo(to) * 0.35f : 0f;
                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
                    _tracers.SurfaceSetColor(col);
                    _tracers.SurfaceAddVertex(from.Lerp(to, t0) + Vector3.Up * peak * 4 * t0 * (1 - t0));
                    _tracers.SurfaceSetColor(col);
                    _tracers.SurfaceAddVertex(from.Lerp(to, t1) + Vector3.Up * peak * 4 * t1 * (1 - t1));
                }
                if (s.By == Kind.Artillery && SeenAt(to)) Blast(to, World.StatsFor[(int)Kind.Artillery].Splash.ToFloat());
            }
            _tracers.SurfaceEnd();
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
                if (step.LengthSquared() > 1e-4f) _facing[e.Id] = step.Normalized();
                var face = _facing.TryGetValue(e.Id, out var f) ? f : new Vector3(e.Owner == 0 ? 1 : -1, 0, 0);
                view.Basis = Basis.LookingAt(face, Vector3.Up);
            }
            view.Position = pos + Vector3.Up * HeightOf(e.Kind);
            view.Visible = Shown(e);
            if (e.Kind == Kind.Factory) UpdateCogs(e, view, _frameDt);
        }
        foreach (var id in _views.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _views[id].QueueFree();
            _views.Remove(id);
            _prevPos.Remove(id);
            _facing.Remove(id);
            _cogs.Remove(id);
        }

        DrawPlacement();
        DrawBarsAndSelection(alpha);
        DrawHud();
    }

    /// <summary>Roughly how high each thing stands, for putting its health bar just above it.</summary>
    static float TopOf(Kind k) => k switch
    {
        Kind.Hq => 3.4f, Kind.Factory => 4.0f, Kind.Tower => 5.0f,   // (a factory's bar clears its cogs)
        Kind.Scout => 2.6f, Kind.Tank => 2.1f, Kind.Artillery => 2.2f, _ => 2f,
    };

    void DrawBarsAndSelection(float alpha)
    {
        _overlay.Bars.Clear();
        _overlay.Badges.Clear();
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
        _selRing.Visible = selected != null;
        _statsPanel.Visible = selected != null;
        if (selected == null) return;
        var ring = selected.Stats.Radius.ToFloat() * (selected.Stats.IsUnit ? 1.25f : 1.15f);
        _selRing.Scale = new Vector3(ring, 1, ring);
        _stats.Text = Describe(selected);
        ShowUpgrades(selected);
        _statsPanel.OffsetTop = -_statsPanel.GetCombinedMinimumSize().Y - 16;
        _statsPanel.Size = _statsPanel.GetCombinedMinimumSize();
    }

    static readonly string[] UpgradeEffect = { "15% quicker to build", "+25% damage", "+25% health" };

    /// <summary>The three upgrade buttons, for one of my factories only.</summary>
    void ShowUpgrades(Entity e)
    {
        bool mine = e.Kind == Kind.Factory && e.Owner == _me;
        for (int i = 0; i < _upgradeButtons.Length; i++)
        {
            var b = _upgradeButtons[i];
            b.Visible = mine;
            if (!mine) continue;
            var which = (Upgrade)i;
            int level = e.Level(which);
            b.Text = level >= World.MaxUpgradeLevel
                ? $"{which}  {level}/{World.MaxUpgradeLevel}   (max)"
                : $"{which}  {level}/{World.MaxUpgradeLevel}   buy for {World.UpgradeCost(level + 1)}:  {UpgradeEffect[i]}";
            b.Disabled = !W.CanUpgrade(_me, e.Id, which, out _) || W.Winner >= 0;
        }
    }

    string Describe(Entity e)
    {
        var s = e.Stats;
        var lines = new List<string>
        {
            $"{e.Kind}  ({(e.Owner == _me ? "yours" : "enemy")})",
            e.Kind switch
            {
                Kind.Scout => "Fast and fragile: first to the fight.",
                Kind.Tank => "Tough all-rounder.",
                Kind.Artillery => "Outranges everything and hits an area,\nbut can't fire at anything close.",
                Kind.Hq => "Lose it and you lose the match.",
                Kind.Tower => "Guards the base: long range and sight.",
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
        if (s.IsUnit && (e.Level(Upgrade.Damage) > 0 || e.Level(Upgrade.Armour) > 0))
            lines.Add($"Upgraded  damage {e.Level(Upgrade.Damage)}/3, armour {e.Level(Upgrade.Armour)}/3");
        if (e.Kind == Kind.Factory) lines.Add($"Next in  {Mathf.CeilToInt(e.Timer / (float)World.TicksPerSecond)} s");
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
            var ok = W.CanPlaceFactory(_me, ToFix(_mouseGround.Value), _placing.Value, out _placeProblem);
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
        int myUnits = 0, theirUnits = 0;
        int myHq = 0, theirHq = 0;
        foreach (var e in W.Entities)
        {
            if (e.Stats.IsUnit && Shown(e)) { if (e.Owner == _me) myUnits++; else theirUnits++; }   // (only enemies we can see)
            if (e.Kind == Kind.Hq) { if (e.Owner == _me) myHq = e.Hp; else theirHq = e.Hp; }
        }
        var menu = string.Join("   ", World.UnitKinds.Select((k, i) =>
            $"[{i + 1}] {k}"));
        var building = _placing == null
            ? $"Build ({World.FactoryCost} each): {menu}"
            : $"Placing {_placing} factory: click to build, Esc to cancel" + (_placeProblem != null ? $"   ({_placeProblem})" : "");
        _hud.Text =
            $"Metal {me.Metal}\n" +
            building + "\n" +
            $"Units {myUnits} vs {theirUnits} seen\n" +
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
                : "Place your first factory: press 1, 2 or 3, then click near your HQ\nThe battle starts once both sides have one";
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
            else if (mb.ButtonIndex == MouseButton.Left && _mouseGround.HasValue)
            {
                if (_placing != null)
                {
                    // One factory per build: placing it ends build mode. A bad spot keeps it, to try again.
                    if (_placeProblem == null) { _session.Issue(new PlaceFactory(_me, ToFix(_mouseGround.Value), _placing.Value)); _placing = null; }
                }
                else _selected = EntityAt(_mouseGround.Value)?.Id ?? -1;
            }
            else if (mb.ButtonIndex == MouseButton.Right) { _placing = null; }
        }
        else if (ev is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode >= Key.Key1 && k.Keycode < Key.Key1 + World.UnitKinds.Length && W.Winner < 0)
                _placing = World.UnitKinds[k.Keycode - Key.Key1];
            else if (k.Keycode == Key.Escape)
            {
                if (_placing != null) _placing = null;
                else _selected = -1;
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
