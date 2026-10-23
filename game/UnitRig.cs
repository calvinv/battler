using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// The moving parts of a unit whose model has a rig (art/units/&lt;name&gt;_rig.glb, from tools/art unit_parts part()):
/// wheels roll with the ground covered, a turret swings to its target while the hull drives, the barrel kicks back
/// when it fires, a flywheel spins with the engine, the body leans as it speeds up and brakes and shudders at idle,
/// and the exhaust smokes. Parts are found by name: wheel_*, turret, barrel, flywheel, body; marks muzzle, exhaust.
/// </summary>
public sealed class UnitRig
{
    readonly List<(Node3D Node, float Radius)> _wheels = new();
    Node3D _body, _turret, _barrel, _flywheel;
    Vector3 _barrelRest, _bodyRest;
    public Node3D Muzzle { get; private set; }
    Node3D _exhaust;
    CpuParticles3D _smoke;
    float _roll, _spin, _recoil, _yaw, _lean, _speed, _time;
    readonly float _phase;

    const float TurretTurn = 2.2f;      // radians a second
    const float Kick = 0.9f;            // how far the barrel slides back, model units

    UnitRig(float phase) => _phase = phase;

    /// <summary>The rig in a view from <see cref="UnitArt.Make"/>, or null if its model has none.</summary>
    public static UnitRig Find(Node3D view, int seed)
    {
        var rig = new UnitRig(seed * 1.7f);
        Walk(view, rig);
        if (rig._wheels.Count == 0 && rig._turret == null) return null;
        if (rig._barrel != null) rig._barrelRest = rig._barrel.Position;
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
                else if (name == "turret") rig._turret = n;
                else if (name == "barrel") rig._barrel = n;
                else if (name == "flywheel") rig._flywheel = n;
                else if (name == "body") rig._body = n;
                else if (name == "muzzle") rig.Muzzle = n;
                else if (name == "exhaust") rig._exhaust = n;
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

    /// <summary>It fired: the barrel kicks back and the body rocks.</summary>
    public void Fire() => _recoil = 1f;

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

        // The turret swings at its own pace.
        if (_turret != null)
        {
            float want = aimYaw ?? 0f;
            float turn = Mathf.Wrap(want - _yaw, -Mathf.Pi, Mathf.Pi);
            _yaw += Mathf.Clamp(turn, -TurretTurn * dt, TurretTurn * dt);
            _turret.Rotation = new Vector3(0, _yaw, 0);
        }

        // The barrel snaps back and runs out again.
        if (_barrel != null)
        {
            _recoil = Mathf.Max(0, _recoil - dt * 3.2f);
            float k = _recoil * _recoil;
            _barrel.Position = _barrelRest - new Vector3(0, 0, Kick * k);
        }

        // The body on its springs: leans back when it pulls away, dips when it brakes, rocks when it fires,
        // shudders with the engine and bumps over the ground while driving.
        if (_body != null)
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
