using System;
using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// Impact explosions (the user, 2026-10-23: "it needs some impact explosions"). One <see cref="Boom"/> plays:
/// a white-hot core, a fireball of puffs that swell, rise and cool from yellow to orange to dull red; a shockwave
/// ring racing out over the ground; dark debris thrown up that tumbles, bounces once and settles; sparks arcing
/// down; a flash of light on everything nearby; a ring of dust rolling out, smoke climbing; a scorch left behind
/// that fades. Looks only.
/// </summary>
public partial class Explosions : Node3D
{
    const int MaxFire = 400, MaxChunks = 300, MaxSparks = 400, MaxRings = 40, Lights = 6, Scorches = 32;
    const float Gravity = 18f;

    struct Fire { public Vector3 At, Vel; public float Age, Life, Size, Grow; }
    struct Chunk { public Vector3 At, Vel, Axis; public float Age, Life, Size, Spin, Ground; public bool Bounced; public Color Col; }
    struct Spark { public Vector3 At, Vel; public float Age, Life, Ground; }
    struct Ring { public Vector3 At; public float Age, Life, Radius; }

    readonly List<Fire> _fire = new();
    readonly List<Chunk> _chunks = new();
    readonly List<Spark> _sparks = new();
    readonly List<Ring> _rings = new();
    readonly RandomNumberGenerator _rng = new() { Seed = 4242 };
    MultiMesh _fireMesh, _chunkMesh, _sparkMesh, _ringMesh;
    readonly OmniLight3D[] _lights = new OmniLight3D[Lights];
    readonly float[] _lightAge = new float[Lights], _lightLife = new float[Lights], _lightPeak = new float[Lights];
    int _nextLight;
    readonly Decal[] _scorch = new Decal[Scorches];
    readonly float[] _scorchAge = new float[Scorches];
    int _nextScorch;

    /// <summary>Somewhere to roll dust and smoke (the vehicles' dust).</summary>
    public Dust Dust;
    /// <summary>The ground's height at (x, z), so debris lands on it.</summary>
    public Func<float, float, float> GroundAt = (_, _) => 0f;

    static readonly Color Smoke = new(0.13f, 0.12f, 0.11f), Dirt = new(0.42f, 0.35f, 0.27f);

    static GradientTexture2D Radial(Color[] cols, float[] at) => new()
    {
        Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient { Colors = cols, Offsets = at },
    };

    static StandardMaterial3D Glow(Texture2D tex, bool billboard, bool add = true) => new()
    {
        AlbedoTexture = tex, VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = add ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
        BillboardKeepScale = true,
    };

    /// <summary>A puff of flame: solid in the middle, its edge broken into billows by noise.</summary>
    static ImageTexture Billow()
    {
        const int N = 128;
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 0.06f, FractalOctaves = 3, Seed = 7 };
        var img = Image.CreateEmpty(N, N, true, Image.Format.Rgba8);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - N / 2f) / (N / 2f), dy = (y - N / 2f) / (N / 2f);
                float r = Mathf.Sqrt(dx * dx + dy * dy) + noise.GetNoise2D(x, y) * 0.35f;
                float a = Mathf.Clamp((0.82f - r) / 0.18f, 0, 1);
                float shade = Mathf.Clamp(1.1f - r * 0.5f + noise.GetNoise2D(x * 2 + 50, y * 2) * 0.25f, 0.55f, 1f);   // a little light and dark through it
                img.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    MultiMesh Layer(Mesh mesh, int count, Material mat, bool shadow = false)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = count, VisibleInstanceCount = 0, Mesh = mesh };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = mm, MaterialOverride = mat,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return mm;
    }

    public override void _Ready()
    {
        // Fire: a soft blob with a bright middle; billboards, added on.
        _fireMesh = Layer(new QuadMesh { Size = Vector2.One }, MaxFire, Glow(Billow(), true, add: false));
        // The shockwave: a thin bright band on a flat plane.
        var band = Radial(new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.7f), new Color(1, 1, 1, 0) }, new[] { 0f, 0.74f, 0.9f, 1f });
        _ringMesh = Layer(new PlaneMesh { Size = Vector2.One }, MaxRings, Glow(band, false));
        // Debris: little dark lumps, lit, casting shadows.
        _chunkMesh = Layer(new BoxMesh { Size = Vector3.One }, MaxChunks,
            new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f }, shadow: true);
        // Sparks: thin streaks along their flight.
        _sparkMesh = Layer(new CapsuleMesh { Radius = 0.5f, Height = 1f, RadialSegments = 4, Rings = 1 }, MaxSparks,
            new StandardMaterial3D { VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                                     Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add });

        for (int i = 0; i < Lights; i++)
        {
            _lights[i] = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.25f), OmniRange = 9f, LightEnergy = 0f, ShadowEnabled = false, Visible = false };
            AddChild(_lights[i]);
        }
        var burnt = Radial(new[] { new Color(0.04f, 0.03f, 0.02f, 0.9f), new Color(0.08f, 0.06f, 0.04f, 0.7f), new Color(0.1f, 0.08f, 0.06f, 0f) }, new[] { 0f, 0.45f, 1f });
        var ember = Radial(new[] { new Color(0.9f, 0.3f, 0.05f), new Color(0.3f, 0.06f, 0f), new Color(0, 0, 0) }, new[] { 0f, 0.3f, 0.7f });
        for (int i = 0; i < Scorches; i++)
        {
            _scorch[i] = new Decal { TextureAlbedo = burnt, TextureEmission = ember, Size = new Vector3(3, 4, 3), Visible = false, AlbedoMix = 1f };
            AddChild(_scorch[i]);
            _scorchAge[i] = 999;
        }
    }

    /// <summary>An explosion at `at` (the hit point; the ground is found under it), `size` 1 = a cannonball.</summary>
    public void Boom(Vector3 at, float size = 1f)
    {
        float ground = GroundAt(at.X, at.Z);
        var floor = new Vector3(at.X, ground, at.Z);

        // The white-hot core, then the fireball: puffs flung out a little, swelling, rising, cooling.
        AddFire(at, Vector3.Zero, 0.18f, 1.6f * size, 2.2f);
        for (int i = 0; i < 22; i++)
        {
            var v = RandDir() * _rng.RandfRange(2f, 6f) * size;
            v.Y = Mathf.Abs(v.Y) * 0.9f + 1.2f;
            AddFire(at + v * 0.06f, v, _rng.RandfRange(0.4f, 0.75f), _rng.RandfRange(0.6f, 1.05f) * size, _rng.RandfRange(1.3f, 1.9f));
        }
        // The shockwave over the ground.
        if (_rings.Count < MaxRings) _rings.Add(new Ring { At = floor + Vector3.Up * 0.35f, Life = 0.28f, Radius = 3.6f * size });
        // Debris: lumps of dirt and metal thrown up and out.
        for (int i = 0; i < 12; i++)
        {
            if (_chunks.Count >= MaxChunks) break;
            var v = RandDir() * _rng.RandfRange(3f, 7f) * size;
            v.Y = _rng.RandfRange(4f, 9f) * Mathf.Sqrt(size);
            bool metal = i % 3 == 0;
            _chunks.Add(new Chunk
            {
                At = at, Vel = v, Axis = RandDir(), Spin = _rng.RandfRange(6f, 16f), Life = _rng.RandfRange(1.2f, 2.0f),
                Size = _rng.RandfRange(0.12f, 0.28f) * size, Ground = ground, Col = metal ? new Color(0.04f, 0.04f, 0.045f) : new Color(0.14f, 0.1f, 0.07f),
            });
        }
        // Sparks arcing out and down.
        for (int i = 0; i < 18; i++)
        {
            if (_sparks.Count >= MaxSparks) break;
            var v = RandDir() * _rng.RandfRange(6f, 13f) * size;
            v.Y = Mathf.Abs(v.Y) + 2f;
            _sparks.Add(new Spark { At = at, Vel = v, Life = _rng.RandfRange(0.35f, 0.7f), Ground = ground });
        }
        // A flash of light on everything round it.
        int l = _nextLight++ % Lights;
        _lightAge[l] = 0; _lightLife[l] = 0.35f; _lightPeak[l] = 7f * size;
        _lights[l].Position = at + Vector3.Up * 1.2f;
        _lights[l].OmniRange = 10f * size;
        _lights[l].Visible = true;
        // Dust rolling out along the ground, and smoke climbing from the middle.
        for (int i = 0; i < 10; i++)
        {
            float t = i * Mathf.Tau / 10 + _rng.RandfRange(-0.2f, 0.2f);
            var outward = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
            Dust?.Add(floor + outward * 0.6f * size + Vector3.Up * 0.3f, outward * 3.2f * size + Vector3.Up * 0.4f, 1.0f * size, Dirt);
        }
        for (int i = 0; i < 7; i++)
            Dust?.Add(at + RandDir() * 0.5f * size + Vector3.Up * 0.4f, Vector3.Up * _rng.RandfRange(1.5f, 3f) + RandDir() * 0.5f, _rng.RandfRange(1.3f, 2.0f) * size, Smoke);
        // A scorch on the ground.
        int s = _nextScorch++ % Scorches;
        _scorch[s].Position = floor;
        _scorch[s].Size = new Vector3(3.2f * size, 4f, 3.2f * size);
        _scorch[s].RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0);
        _scorch[s].Visible = true;
        _scorchAge[s] = 0;
    }

    void AddFire(Vector3 at, Vector3 vel, float life, float size, float grow)
    {
        if (_fire.Count < MaxFire) _fire.Add(new Fire { At = at, Vel = vel, Life = life, Size = size, Grow = grow });
    }

    Vector3 RandDir()
    {
        var v = new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1), _rng.RandfRange(-1, 1));
        return v.LengthSquared() > 1e-4f ? v.Normalized() : Vector3.Up;
    }

    /// <summary>Fire cools: white, yellow, orange, dull red, gone. Over 1 so the hot part blooms.</summary>
    static Color Heat(float t)
    {
        if (t < 0.12f) return new Color(1f, 0.95f, 0.8f).Lerp(new Color(1f, 0.75f, 0.3f), t / 0.12f) * 3f;
        if (t < 0.45f) return new Color(1f, 0.75f, 0.3f).Lerp(new Color(1f, 0.38f, 0.08f), (t - 0.12f) / 0.33f) * Mathf.Lerp(3f, 1.6f, (t - 0.12f) / 0.33f);
        float u = (t - 0.45f) / 0.55f;
        var c = new Color(1f, 0.38f, 0.08f).Lerp(new Color(0.16f, 0.07f, 0.04f), u) * Mathf.Lerp(1.6f, 1f, u);
        return c;
    }

    public void Clear()
    {
        _fire.Clear(); _chunks.Clear(); _sparks.Clear(); _rings.Clear();
        foreach (var l in _lights) l.Visible = false;
        for (int i = 0; i < Scorches; i++) { _scorch[i].Visible = false; _scorchAge[i] = 999; }
        Draw();
    }

    public override void _Process(double delta) => Advance((float)delta);

    public void Advance(float dt)
    {
        for (int i = _fire.Count - 1; i >= 0; i--)
        {
            var f = _fire[i];
            f.Age += dt;
            if (f.Age >= f.Life) { _fire[i] = _fire[^1]; _fire.RemoveAt(_fire.Count - 1); continue; }
            f.Vel *= Mathf.Exp(-5f * dt);
            f.Vel.Y += 2.5f * dt;   // hot air rises
            f.At += f.Vel * dt;
            _fire[i] = f;
        }
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            c.Age += dt;
            if (c.Age >= c.Life) { _chunks[i] = _chunks[^1]; _chunks.RemoveAt(_chunks.Count - 1); continue; }
            c.Vel.Y -= Gravity * dt;
            c.At += c.Vel * dt;
            if (c.At.Y < c.Ground + c.Size * 0.5f && c.Vel.Y < 0)
            {
                c.At.Y = c.Ground + c.Size * 0.5f;
                if (!c.Bounced) { c.Vel = new Vector3(c.Vel.X * 0.45f, -c.Vel.Y * 0.3f, c.Vel.Z * 0.45f); c.Bounced = true; Dust?.Add(c.At, Vector3.Up * 0.5f, 0.35f, Dirt); }
                else { c.Vel = Vector3.Zero; c.Spin = 0; }
            }
            _chunks[i] = c;
        }
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            s.Vel.Y -= Gravity * dt;
            s.Vel *= Mathf.Exp(-1.5f * dt);
            s.At += s.Vel * dt;
            if (s.Age >= s.Life || s.At.Y < s.Ground) { _sparks[i] = _sparks[^1]; _sparks.RemoveAt(_sparks.Count - 1); continue; }
            _sparks[i] = s;
        }
        for (int i = _rings.Count - 1; i >= 0; i--)
        {
            var r = _rings[i];
            r.Age += dt;
            if (r.Age >= r.Life) { _rings[i] = _rings[^1]; _rings.RemoveAt(_rings.Count - 1); continue; }
            _rings[i] = r;
        }
        for (int i = 0; i < Lights; i++)
        {
            if (!_lights[i].Visible) continue;
            _lightAge[i] += dt;
            float t = _lightAge[i] / _lightLife[i];
            if (t >= 1) { _lights[i].Visible = false; continue; }
            _lights[i].LightEnergy = _lightPeak[i] * (1 - t) * (1 - t);
        }
        for (int i = 0; i < Scorches; i++)
        {
            if (!_scorch[i].Visible) continue;
            _scorchAge[i] += dt;
            // Glowing at first, the embers dying in a couple of seconds; the burn fades over twenty.
            _scorch[i].EmissionEnergy = 2.5f * Mathf.Max(0, 1 - _scorchAge[i] / 2.5f);
            float fade = Mathf.Clamp(1 - (_scorchAge[i] - 12f) / 8f, 0, 1);
            _scorch[i].Modulate = new Color(1, 1, 1, fade);
            if (fade <= 0) _scorch[i].Visible = false;
        }
        Draw();
    }

    void Draw()
    {
        if (_fireMesh == null) return;
        int n = 0;
        foreach (var f in _fire)
        {
            float t = f.Age / f.Life;
            float size = f.Size * (0.5f + f.Grow * (1 - Mathf.Pow(1 - t, 2.5f)));
            var col = Heat(t);
            _fireMesh.SetInstanceTransform(n, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), f.At));
            _fireMesh.SetInstanceColor(n, new Color(col.R, col.G, col.B, 0.95f * (1f - t * t * t)));
            n++;
        }
        _fireMesh.VisibleInstanceCount = n;

        n = 0;
        foreach (var c in _chunks)
        {
            float shrink = c.Age > c.Life - 0.3f ? (c.Life - c.Age) / 0.3f : 1f;
            var b = new Basis(c.Axis, c.Age * c.Spin) * Basis.Identity.Scaled(new Vector3(1f, 0.45f, 0.7f) * c.Size * shrink);
            _chunkMesh.SetInstanceTransform(n, new Transform3D(b, c.At));
            _chunkMesh.SetInstanceColor(n, c.Col);
            n++;
        }
        _chunkMesh.VisibleInstanceCount = n;

        n = 0;
        foreach (var s in _sparks)
        {
            float speed = s.Vel.Length();
            if (speed < 1e-3f) continue;
            var dir = s.Vel / speed;
            var up = Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
            var basis = Basis.LookingAt(dir, up) * new Basis(Vector3.Right, Mathf.Pi / 2);
            float len = Mathf.Clamp(speed * 0.04f, 0.15f, 0.7f), w = 0.05f;
            _sparkMesh.SetInstanceTransform(n, new Transform3D(new Basis(basis.X * w, basis.Y * len, basis.Z * w), s.At));
            float fade = 1 - s.Age / s.Life;
            _sparkMesh.SetInstanceColor(n, new Color(1f, 0.75f, 0.35f, fade) * new Color(2.5f, 2.5f, 2.5f, 1f));
            n++;
        }
        _sparkMesh.VisibleInstanceCount = n;

        n = 0;
        foreach (var r in _rings)
        {
            float t = r.Age / r.Life;
            float rad = r.Radius * (0.15f + 0.85f * (1 - (1 - t) * (1 - t)));
            _ringMesh.SetInstanceTransform(n, new Transform3D(Basis.Identity.Scaled(new Vector3(rad * 2, 1, rad * 2)), r.At));
            _ringMesh.SetInstanceColor(n, new Color(1f, 0.7f, 0.4f, (1 - t) * (1 - t) * 0.5f));
            n++;
        }
        _ringMesh.VisibleInstanceCount = n;
    }
}
