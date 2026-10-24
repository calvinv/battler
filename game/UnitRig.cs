using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// The moving parts of a unit whose model has a rig (art/units/&lt;name&gt;_rig.glb, from tools/art unit_parts part()):
/// wheels roll with the ground covered, a turret swings to its target while the hull drives, the barrel kicks back
/// when it fires, a flywheel spins with the engine, the body leans as it speeds up and brakes and shudders at idle,
/// and the exhaust smokes; a walker's legs step in time with the ground it covers. Parts are found by name: wheel_*,
/// turret, barrel, flywheel, body, thigh_L/R + shin_L/R + foot_L/R, tleg_N + tshin_N (a tripod), rotor_N (props, spun flat), drone_N (a mender's repair drones, flown out by SendDrone); marks muzzle, exhaust.
/// </summary>
public sealed class UnitRig
{
    readonly List<(Node3D Node, float Radius)> _wheels = new();
    readonly List<Node3D> _rotors = new();
    // A mender's repair drones: docked on its deck until sent out to hover over a unit it's mending, then back.
    sealed class Drone { public Node3D Node; public Transform3D Rest; public float T = -1; public Vector3 From, To; }
    readonly List<Drone> _drones = new();
    const float DroneOut = 0.7f, DroneStay = 0.9f, DroneBack = 0.7f;
    Node3D _body, _turret, _barrel, _flywheel;
    Vector3 _barrelRest, _bodyRest;
    public Node3D Muzzle { get; private set; }
    Node3D _exhaust;
    CpuParticles3D _smoke;
    float _roll, _spin, _recoil, _yaw, _lean, _speed, _time;
    readonly float _phase;
    bool _hover;
    readonly Node3D[,] _legs = new Node3D[2, 3];      // [left/right, thigh/shin/foot]
    bool _walker;
    float _gait, _stride;
    // A tripod (the Long Leg): three legs round the body, stepping one at a time.
    readonly Node3D[,] _tri = new Node3D[3, 2];       // [leg, thigh/shin]
    readonly Vector3[] _triOut = new Vector3[3];      // each leg's outward direction, flat
    bool _tripod;
    float _kick = Kick;
    // A walker's rifle: carried across the body while walking, swung up to aim once it stops with a target. The
    // upper arms turn at the shoulders to keep reaching the elbows (which ride with the rifle).
    readonly Node3D[] _arms = new Node3D[2], _elbows = new Node3D[2];
    readonly Basis[] _armRest = new Basis[2];
    readonly Vector3[] _armReach = new Vector3[2];
    float _aim;
    Node3D _hull;                    // a body that pitches on its axle (the battle wheel): cannon up driving, level to aim
    const float HullCarry = -0.6f;   // radians: nose up
    Basis _barrelBasis = Basis.Identity;
    static readonly Basis Carry = new Basis(Vector3.Up, 0.95f) * new Basis(Vector3.Right, -0.45f);   // across the chest, muzzle up and to its left
    static readonly Vector3 CarryShift = new(0.15f, 0.55f, -0.7f);                                     // drawn in and up, model units

    const float TurretTurn = 4f;      // radians a second
    const float Kick = 0.9f;            // how far the barrel slides back, model units

    UnitRig(float phase) => _phase = phase;

    /// <summary>The rig in a view from <see cref="UnitArt.Make"/>, or null if its model has none.</summary>
    public static UnitRig Find(Node3D view, int seed)
    {
        var rig = new UnitRig(seed * 1.7f);
        Walk(view, rig);
        rig._walker = rig._legs[0, 0] != null && rig._legs[1, 0] != null;
        rig._tripod = rig._tri[0, 0] != null && rig._tri[1, 0] != null && rig._tri[2, 0] != null;
        if (rig._tripod)
            for (int i = 0; i < 3; i++)
            {
                var p = rig._tri[i, 0].Position;
                rig._triOut[i] = new Vector3(p.X, 0, p.Z).Normalized();
            }
        if (rig._wheels.Count == 0 && rig._turret == null && !rig._walker && !rig._tripod && rig._rotors.Count == 0) return null;
        foreach (var d in rig._drones) d.Rest = d.Node.Transform;
        rig._hover = rig._wheels.Count == 0 && !rig._walker && !rig._tripod;      // no wheels or legs: it floats
        if (rig._walker) rig._kick = 0.4f;
        if (rig._hover && rig._rotors.Count == 0) rig.AddHoverGlow(view);   // (a flier is too high for a glow on the ground)
        if (rig._barrel != null) { rig._barrelRest = rig._barrel.Position; rig._barrelBasis = rig._barrel.Basis; }
        for (int i = 0; i < 2; i++)
            if (rig._arms[i] != null && rig._elbows[i] != null)
            {
                rig._armRest[i] = rig._arms[i].Basis;
                rig._armReach[i] = rig.ElbowFromShoulder(i);
            }
        if (rig._body != null) rig._bodyRest = rig._body.Position;
        if (rig._exhaust != null) rig.AddSmoke(view);
        return rig;
    }

    static void Walk(Node node, UnitRig rig)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n)
            {
                string name = n.Name.ToString();
                if (name.StartsWith("wheel")) rig._wheels.Add((n, RadiusOf(n)));
                else if (name.StartsWith("rotor")) rig._rotors.Add(n);
                else if (name.StartsWith("drone_")) rig._drones.Add(new Drone { Node = n });
                else if (name == "turret") rig._turret = n;
                else if (name == "barrel") rig._barrel = n;
                else if (name == "flywheel") rig._flywheel = n;
                else if (name == "body") rig._body = n;
                else if (name == "muzzle") rig.Muzzle = n;
                else if (name == "exhaust") rig._exhaust = n;
                else if (name == "hull") rig._hull = n;
                else if (name is "arm_L" or "arm_R") rig._arms[name[^1] == 'L' ? 0 : 1] = n;
                else if (name is "elbow_L" or "elbow_R") rig._elbows[name[^1] == 'L' ? 0 : 1] = n;
                else if ((name.StartsWith("tleg_") || name.StartsWith("tshin_")) && int.TryParse(name[(name.IndexOf('_') + 1)..], out int leg) && leg is >= 0 and < 3)
                    rig._tri[leg, name[1] == 'l' ? 0 : 1] = n;
                else if (name.Length > 2 && name[^2] == '_' && (name[^1] == 'L' || name[^1] == 'R'))
                {
                    int side = name[^1] == 'L' ? 0 : 1;
                    int joint = name.StartsWith("thigh") ? 0 : name.StartsWith("shin") ? 1 : name.StartsWith("foot") ? 2 : -1;
                    if (joint >= 0) rig._legs[side, joint] = n;
                }
            }
            Walk(child, rig);
        }
    }

    /// <summary>A wheel's radius in world units: half its mesh's height, at the model's scale.</summary>
    static float RadiusOf(Node3D wheel)
    {
        var mesh = wheel as MeshInstance3D ?? FirstMesh(wheel);
        float r = mesh?.Mesh != null ? mesh.Mesh.GetAabb().Size.Y * 0.5f : 1f;
        return Mathf.Max(0.05f, r * UnitArt.Scale);
    }

    static MeshInstance3D FirstMesh(Node node)
    {
        foreach (var c in node.GetChildren())
            if (c is MeshInstance3D m) return m;
            else if (FirstMesh(c) is { } found) return found;
        return null;
    }

    /// <summary>Exhaust smoke: soft grey puffs that rise and spread, puffing harder when the engine works.</summary>
    void AddSmoke(Node3D view)
    {
        var puff = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = SoftDot(),
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(0.32f, 0.31f, 0.3f, 0.55f));
        fade.SetColor(1, new Color(0.5f, 0.49f, 0.47f, 0f));
        var grow = new Curve();
        grow.AddPoint(new Vector2(0, 0.35f));
        grow.AddPoint(new Vector2(1, 1.6f));
        _smoke = new CpuParticles3D
        {
            Amount = 28,
            Lifetime = 1.8f,
            LocalCoords = false,
            Mesh = new QuadMesh { Size = new Vector2(0.32f, 0.32f), Material = puff },
            Direction = Vector3.Up,
            Spread = 18,
            InitialVelocityMin = 0.5f,
            InitialVelocityMax = 0.9f,
            Gravity = new Vector3(0, 0.25f, 0),
            DampingMin = 0.6f,
            DampingMax = 1.0f,
            ScaleAmountCurve = grow,
            ColorRamp = fade,
        };
        view.AddChild(_smoke);
    }

    static Texture2D _dot;
    static Texture2D SoftDot()
    {
        if (_dot != null) return _dot;
        const int n = 32;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).Length() / (n / 2f);
                img.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(1 - d, 0, 1) * Mathf.Clamp(1 - d, 0, 1)));
            }
        return _dot = ImageTexture.CreateFromImage(img);
    }

    MeshInstance3D _glow;

    /// <summary>A soft cyan pool of light on the ground under a hover unit, pulsing a little with its bob.</summary>
    void AddHoverGlow(Node3D view)
    {
        var mat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.35f, 0.9f, 1f, 0.55f),
            AlbedoTexture = SoftDot(),
        };
        _glow = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(2.2f, 2.8f) },
            MaterialOverride = mat,
            Position = new Vector3(0, 0.04f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        view.AddChild(_glow);
    }

    /// <summary>Where an elbow is from its shoulder, in the shoulder's parent's space (model units).</summary>
    Vector3 ElbowFromShoulder(int i)
    {
        var parent = _arms[i].GetParentNode3D();
        var elbow = parent.GlobalTransform.AffineInverse() * _elbows[i].GlobalPosition;
        return elbow - _arms[i].Position;
    }

    /// <summary>It fired: the barrel kicks back and the body rocks.</summary>
    /// <summary>The gun fires: the barrel kicks, and a turret that hasn't swung round yet snaps onto `aimYaw` (radians
    /// from straight ahead) so the shot leaves along its barrel.</summary>
    public void Fire(float? aimYaw = null)
    {
        _recoil = 1f;
        if (_turret != null && aimYaw is float y)
        {
            _yaw = y;
            _turret.Rotation = new Vector3(0, _yaw, 0);
        }
    }

    /// <summary>A mender mends: a docked drone takes off and flies to hover over `over` (world), then comes home.
    /// (All out already: the one sent longest ago turns back for this.)</summary>
    public void SendDrone(Vector3 over)
    {
        Drone pick = null;
        foreach (var d in _drones)
            if (d.T < 0) { pick = d; break; }
        if (pick == null) return;
        pick.T = 0;
        pick.From = pick.Node.GlobalPosition;
        pick.To = over + Vector3.Up * 1.1f;
    }

    void FlyDrones(float dt)
    {
        foreach (var d in _drones)
        {
            if (d.T < 0) continue;
            d.T += dt;
            var parent = d.Node.GetParentNode3D();
            var dock = parent.GlobalTransform * d.Rest.Origin;
            Vector3 at;
            if (d.T < DroneOut)
            {
                float u = Ease(d.T / DroneOut);
                at = d.From.Lerp(d.To, u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * 1.2f;
            }
            else if (d.T < DroneOut + DroneStay)
            {
                float h = d.T - DroneOut;   // circling a little over it, its green light on the work
                at = d.To + new Vector3(Mathf.Cos(h * 5f) * 0.35f, Mathf.Sin(h * 11f) * 0.08f, Mathf.Sin(h * 5f) * 0.35f);
            }
            else if (d.T < DroneOut + DroneStay + DroneBack)
            {
                float u = Ease((d.T - DroneOut - DroneStay) / DroneBack);
                at = d.To.Lerp(dock, u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * 1.2f;
            }
            else
            {
                d.Node.Transform = d.Rest;
                d.T = -1;
                continue;
            }
            d.Node.GlobalPosition = at;
            d.Node.Basis = d.Rest.Basis * new Basis(Vector3.Up, d.T * 3f);
        }
    }

    static float Ease(float t) => t * t * (3 - 2 * t);

    /// <summary>One frame. `moved`: world distance driven since the last frame. `aimYaw`: the turret's wanted turn
    /// from straight ahead (radians, about up), or null to bring it home.</summary>
    public void Update(float dt, float moved, float? aimYaw)
    {
        if (dt <= 0) return;
        _time += dt;
        float speed = moved / dt;
        float accel = (speed - _speed) / dt;
        _speed = Mathf.Lerp(_speed, speed, Mathf.Min(1, dt * 12));
        float work = Mathf.Clamp(_speed / 2.5f, 0, 1);

        // Wheels roll without slipping; the flywheel idles and races with the speed.
        _roll += moved;
        foreach (var (node, radius) in _wheels) node.Rotation = new Vector3(_roll / radius, 0, 0);
        _spin += dt * (5f + 22f * work);
        if (_flywheel != null) _flywheel.Rotation = new Vector3(0, 0, _spin);
        for (int i = 0; i < _rotors.Count; i++) _rotors[i].Rotation = new Vector3(0, (i % 2 == 0 ? 1 : -1) * (_time * 31f + i), 0);   // (pairs turn opposite ways)

        if (_drones.Count > 0) FlyDrones(dt);

        // The turret swings at its own pace.
        if (_turret != null)
        {
            float want = aimYaw ?? 0f;
            float turn = Mathf.Wrap(want - _yaw, -Mathf.Pi, Mathf.Pi);
            _yaw += Mathf.Clamp(turn, -TurretTurn * dt, TurretTurn * dt);
            _turret.Rotation = new Vector3(0, _yaw, 0);
        }

        // The barrel snaps back and runs out again. A walker's rifle swings between carried and aimed: aimed when
        // it has stopped with something to shoot (or has just fired), carried across the body while it walks.
        if (_barrel != null)
        {
            _recoil = Mathf.Max(0, _recoil - dt * 3.2f);
            float k = _recoil * _recoil;
            if (_walker && _arms[0] != null)
            {
                bool aiming = (aimYaw != null || _recoil > 0) && _speed < 0.35f;
                _aim = Mathf.MoveToward(_aim, aiming ? 1 : 0, dt * (aiming ? 3.5f : 2.5f));
                float t = _aim * _aim * (3 - 2 * _aim);                   // eased
                var pose = new Basis(Carry.GetRotationQuaternion().Slerp(Quaternion.Identity, t));
                _barrel.Basis = pose * _barrelBasis;
                _barrel.Position = _barrelRest + CarryShift * (1 - t) - pose * new Vector3(0, 0, _kick * k);
                for (int i = 0; i < 2; i++)
                {
                    if (_arms[i] == null || _elbows[i] == null) continue;
                    _arms[i].Basis = _armRest[i];
                    var reach = ElbowFromShoulder(i);
                    if (reach.LengthSquared() < 1e-6f) continue;
                    _arms[i].Basis = new Basis(new Quaternion(_armReach[i].Normalized(), reach.Normalized())) * _armRest[i];
                }
            }
            else _barrel.Position = _barrelRest - new Vector3(0, 0, _kick * k);
        }

        // A drum on an axle: the cannon points up while it drives and tips down level to aim once it stops with a
        // target (or fires); it rocks back with each shot and wobbles a little as it rolls.
        if (_hull != null)
        {
            bool aiming = (aimYaw != null || _recoil > 0) && _speed < 0.35f;
            _aim = Mathf.MoveToward(_aim, aiming ? 1 : 0, dt * (aiming ? 2.2f : 1.6f));
            float t = _aim * _aim * (3 - 2 * _aim);
            float wobble = Mathf.Sin(_time * 6.5f + _phase) * 0.04f * Mathf.Clamp(_speed / 2f, 0, 1);
            _hull.Rotation = new Vector3(HullCarry * (1 - t) - _recoil * _recoil * 0.18f + wobble, 0, 0);
        }

        // A walker steps: the gait advances with the ground covered (one stride a cycle, so feet don't skate), each
        // leg swinging back on the ground and folding up as it swings forward, the feet kept flat; the body dips at
        // each footfall and rolls side to side. Standing, the legs settle and it breathes.
        if (_walker)
        {
            const float Stride = 1.0f;     // world units a full cycle (two steps)
            _gait += moved / Stride * Mathf.Tau;
            _stride = Mathf.Lerp(_stride, Mathf.Clamp(_speed / 1.2f, 0, 1), Mathf.Min(1, dt * 8));
            for (int side = 0; side < 2; side++)
            {
                float ph = _gait + side * Mathf.Pi;
                float hip = 0.45f * Mathf.Sin(ph) * _stride;
                float lift = Mathf.Max(0, -Mathf.Cos(ph)) * _stride;
                float knee = 0.75f * lift;
                if (_legs[side, 0] != null) _legs[side, 0].Rotation = new Vector3(hip, 0, 0);
                if (_legs[side, 1] != null) _legs[side, 1].Rotation = new Vector3(knee, 0, 0);
                if (_legs[side, 2] != null) _legs[side, 2].Rotation = new Vector3(-(hip + knee) - 0.25f * lift, 0, 0);
            }
            if (_body != null)
            {
                float lean = Mathf.Clamp(-accel * 0.015f, -0.08f, 0.08f) + 0.06f * _stride - _recoil * _recoil * 0.08f;
                _lean = Mathf.Lerp(_lean, lean, Mathf.Min(1, dt * 6));
                float bob = 0.16f * _stride * Mathf.Cos(2 * _gait) + 0.05f * (1 - _stride) * Mathf.Sin(_time * 2.2f + _phase);
                _body.Position = _bodyRest + new Vector3(0, bob, 0);
                _body.Rotation = new Vector3(_lean, 0, 0.05f * _stride * Mathf.Sin(_gait));
            }
        }
        // A tripod steps one leg at a time: each in turn lifts its knee out, swings its foot forward and plants it,
        // then slides back under the body as the others take their steps; the body dips as each foot lifts.
        else if (_tripod)
        {
            const float Stride = 2.4f;     // world units a full cycle (each leg one step)
            _gait += moved / Stride;
            _stride = Mathf.Lerp(_stride, Mathf.Clamp(_speed / 1.0f, 0, 1), Mathf.Min(1, dt * 6));
            float dip = 0;
            for (int i = 0; i < 3; i++)
            {
                float p = Mathf.PosMod(_gait + i / 3f, 1f);
                float lift = p < 1 / 3f ? Mathf.Sin(p * 3 * Mathf.Pi) : 0;
                float swing = p < 1 / 3f ? -Mathf.Cos(p * 3 * Mathf.Pi) : 1 - 2 * (p - 1 / 3f) * 1.5f;
                dip = Mathf.Max(dip, lift);
                var side = Vector3.Up.Cross(_triOut[i]).Normalized();
                _tri[i, 0].Basis = new Basis(Vector3.Up, swing * 0.16f * _stride) * new Basis(side, -0.32f * lift * _stride);
                if (_tri[i, 1] != null) _tri[i, 1].Basis = new Basis(side, 0.5f * lift * _stride);
            }
            if (_body != null)
            {
                float lean = Mathf.Clamp(-accel * 0.01f, -0.05f, 0.05f) - _recoil * _recoil * 0.1f;
                _lean = Mathf.Lerp(_lean, lean, Mathf.Min(1, dt * 5));
                float bob = -0.25f * dip * _stride + 0.06f * (1 - _stride) * Mathf.Sin(_time * 1.8f + _phase);
                _body.Position = _bodyRest + new Vector3(0, bob, 0);
                _body.Rotation = new Vector3(_lean, 0, 0);
            }
        }
        // A hover unit floats: a slow bob, its nose lifting as it pulls away and dipping as it stops, a sway.
        else if (_body != null && _hover)
        {
            float lean = Mathf.Clamp(-accel * 0.02f, -0.1f, 0.1f) - _recoil * _recoil * 0.06f;
            _lean = Mathf.Lerp(_lean, lean, Mathf.Min(1, dt * 4));
            float bob = Mathf.Sin(_time * 2.1f + _phase) * 0.18f + Mathf.Sin(_time * 3.7f + _phase * 2) * 0.06f;
            _body.Position = _bodyRest + new Vector3(0, bob, 0);
            _body.Rotation = new Vector3(_lean, 0, Mathf.Sin(_time * 1.6f + _phase) * 0.025f);
            if (_glow != null) _glow.Scale = Vector3.One * (1f - bob * 0.4f);
        }
        // The body on its springs: leans back when it pulls away, dips when it brakes, rocks when it fires,
        // shudders with the engine and bumps over the ground while driving.
        else if (_body != null)
        {
            float lean = Mathf.Clamp(-accel * 0.012f, -0.06f, 0.06f) - _recoil * _recoil * 0.05f;
            _lean = Mathf.Lerp(_lean, lean, Mathf.Min(1, dt * 6));
            float shudder = Mathf.Sin(_time * 55f + _phase) * (0.012f + 0.01f * work);
            float bump = (Mathf.Sin(_time * 9.1f + _phase) * 0.6f + Mathf.Sin(_time * 14.3f + _phase * 2) * 0.4f) * 0.05f * work;
            _body.Position = _bodyRest + new Vector3(0, shudder + bump, 0);
            _body.Rotation = new Vector3(_lean, 0, Mathf.Sin(_time * 7.3f + _phase) * 0.012f * work);
        }

        // Smoke from the stack, thicker under load, following it about.
        if (_smoke != null && _exhaust != null)
        {
            _smoke.GlobalPosition = _exhaust.GlobalPosition;
            _smoke.InitialVelocityMax = 0.9f + 1.4f * work;
            _smoke.SpeedScale = 0.8f + 0.6f * work;
        }
    }
}
