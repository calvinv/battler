using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// Things drawn on the field rather than as units or buildings:
///   - sulfur patches, clusters of yellow crystals (the user, 2026-10-22: "a sulfur patch that could increase explosive
///     damage ... or give you access to napalm");
///   - napalm burning as a glowing patch with flames licking up, dying down as it burns out;
///   - the radar (the user, 2026-10-22: "a radar that pings every 30s to show where units are in the fog"): a ring
///     sweeping out from each of my bases as it pings, and a red blip where each hidden enemy unit was, fading out.
/// </summary>
public partial class Main
{
    void ClearFieldViews()
    {
        foreach (var f in _fireViews) f.QueueFree();
        _fireViews.Clear();
    }

    // ---- radar -----------------------------------------------------------------------------------------------

    const float BlipSeconds = 10f, SweepSeconds = 2.5f;

    /// <summary>Seconds since my side's last radar sweep (-1 before the first).</summary>
    float SinceSweep => W.Players[_me].BlipTick < 0 ? -1 : (W.Tick - W.Players[_me].BlipTick) / (float)World.TicksPerSecond;

    /// <summary>The fading blips, for the overlay to draw: where, and how strong (1 at the sweep, 0 when gone).</summary>
    void DrawRadarBlips()
    {
        _overlay.Blips.Clear();
        float since = SinceSweep;
        if (since < 0 || since > BlipSeconds) return;
        float strength = 1 - since / BlipSeconds;
        foreach (var at in W.Players[_me].Blips)
        {
            var p = ToV3(at);
            p.Y = _terrain.HeightAt(p.X, p.Z) + 1;
            if (!_camera.IsPositionBehind(p)) _overlay.Blips.Add((_camera.UnprojectPosition(p), strength));
        }
    }

    /// <summary>The sweep: a ring running out from each of my bases to its radar reach, just after a ping.</summary>
    void SweepRings(System.Action<FixVec2, float> circle)
    {
        float since = SinceSweep;
        if (since < 0 || since > SweepSeconds) return;
        foreach (var b in W.Entities)
            if (b.Owner == _me && World.IsBase(b) && b.Building == 0)
                circle(b.Pos, World.RadarOf(b).ToFloat() * since / SweepSeconds);
    }

    // ---- power vents ---------------------------------------------------------------------------------------------

    readonly List<Node3D> _ventViews = new();
    StandardMaterial3D _steamMat;

    /// <summary>A power vent: a scorched patch of ground (a decal) with a glowing crack, steam rising off it.</summary>
    Node3D MakeVent(int seed)
    {
        _steamMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.85f, 0.82f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        var root = new Node3D();
        root.AddChild(new Decal
        {
            Size = new Vector3(6, 3, 6),
            TextureAlbedo = Radial(new[] { new Color(0.08f, 0.07f, 0.06f, 0.95f), new Color(0.15f, 0.12f, 0.1f, 0.7f), new Color(0.2f, 0.17f, 0.14f, 0f) }),
            TextureEmission = Radial(new[] { new Color(1f, 0.45f, 0.1f), new Color(0.2f, 0.05f, 0f), new Color(0, 0, 0) }),
            EmissionEnergy = 1.4f, UpperFade = 0.3f, LowerFade = 0.3f,
        });
        for (int i = 0; i < 5; i++)   // steam puffs (rising and fading in AnimateVents)
            root.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.6f, 1.6f) }, MaterialOverride = _steamMat });
        root.SetMeta("seed", seed);
        return root;
    }

    void AnimateVents()
    {
        float now = (float)Time.GetTicksMsec() * 0.001f;
        foreach (var vent in _ventViews)
        {
            if (!vent.Visible) continue;
            int seed = (int)vent.GetMeta("seed");
            for (int k = 1; k < vent.GetChildCount(); k++)
            {
                float t = (now * 0.35f + k * 0.2f + seed * 0.13f) % 1f;   // each puff's age, 0 to 1
                var puff = vent.GetChild<MeshInstance3D>(k);
                puff.Position = new Vector3(Mathf.Sin(seed + k * 2.1f) * 0.6f + t * 0.8f, 0.5f + t * 6f, Mathf.Cos(seed + k * 1.3f) * 0.6f);
                puff.Scale = Vector3.One * (0.6f + t * 1.8f);
                puff.Transparency = t;
            }
        }
    }

    // ---- sulfur ----------------------------------------------------------------------------------------------

    /// <summary>A sulfur patch: a low pale rock bristling with yellow crystals that glow a little.</summary>
    static Node3D MakeSulfur(int seed) => MakeCrystals(seed, new Color(0.36f, 0.31f, 0.2f), new Color(0.72f, 0.62f, 0.22f), new Color(1f, 0.85f, 0.15f), new Color(1f, 0.78f, 0.1f));

    /// <summary>A crystal field (the user, 2026-10-23: "natural resources"): dark rock bristling with tall cyan crystals that glow.</summary>
    static Node3D MakeCrystal(int seed) => MakeCrystals(seed + 101, new Color(0.22f, 0.24f, 0.28f), new Color(0.3f, 0.42f, 0.5f), new Color(0.45f, 0.95f, 1f), new Color(0.3f, 0.85f, 1f));

    /// <summary>An oil seep: a black glossy pool in a ring of stained rock, a few tar blobs, a sheen on it.</summary>
    static Node3D MakeOil(int seed)
    {
        var root = new Node3D();
        var tar = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.02f, 0.025f), Roughness = 0.05f, Metallic = 0.6f, MetallicSpecular = 1f };
        var sheen = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.25f, 0.45f, 0.1f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f,
            EmissionEnabled = true, Emission = new Color(0.12f, 0.1f, 0.22f), EmissionEnergyMultiplier = 0.15f,
        };
        var stain = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.13f, 0.1f), Roughness = 0.95f };
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.6f, Height = 0.12f, RadialSegments = 24 }, MaterialOverride = tar, Position = new Vector3(0, 0.05f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.9f, BottomRadius = 0.9f, Height = 0.02f, RadialSegments = 20 }, MaterialOverride = sheen, Position = new Vector3(0.6f, 0.13f, -0.4f) });
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 6007 + 5) };
        for (int i = 0; i < 9; i++)   // stained rocks round the rim
        {
            float ang = Mathf.Tau * i / 9 + rng.RandfRange(-0.2f, 0.2f), r = rng.RandfRange(0.4f, 0.8f);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r, Height = r * 1.1f, RadialSegments = 6, Rings = 3 }, MaterialOverride = stain,
                Position = new Vector3(Mathf.Cos(ang) * 2.8f, r * 0.2f, Mathf.Sin(ang) * 2.8f),
            });
        }
        for (int i = 0; i < 4; i++)   // tar blobs welling up
        {
            float ang = rng.RandfRange(0, Mathf.Tau), d = rng.RandfRange(0.2f, 1.6f), r = rng.RandfRange(0.25f, 0.45f);
            root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r, RadialSegments = 8, Rings = 4 }, MaterialOverride = tar, Position = new Vector3(Mathf.Cos(ang) * d, 0.1f, Mathf.Sin(ang) * d) });
        }
        return root;
    }

    /// <summary>A patch of glowing crystals: lumps of rock, some crusted, crystals jutting out of them in clumps.</summary>
    static Node3D MakeCrystals(int seed, Color rockCol, Color crustCol, Color crystalCol, Color glowCol)
    {
        var root = new Node3D();
        var rock = new StandardMaterial3D { AlbedoColor = rockCol, Roughness = 0.95f };
        var crust = new StandardMaterial3D { AlbedoColor = crustCol, Roughness = 0.9f };
        var crystal = new StandardMaterial3D
        {
            AlbedoColor = crystalCol, Roughness = 0.25f, Metallic = 0.1f,
            EmissionEnabled = true, Emission = glowCol, EmissionEnergyMultiplier = 0.9f,
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 7919 + 13) };
        for (int i = 0; i < 6; i++)   // lumps of rock, crusted yellow
        {
            float ang = Mathf.Tau * i / 6 + rng.RandfRange(-0.4f, 0.4f), dist = i == 0 ? 0 : rng.RandfRange(1.2f, 2.2f);
            float r = i == 0 ? 1.6f : rng.RandfRange(0.7f, 1.2f);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r, Height = r * rng.RandfRange(0.9f, 1.3f), RadialSegments = 7, Rings = 3 },
                MaterialOverride = i % 2 == 0 ? rock : crust,
                Position = new Vector3(Mathf.Cos(ang) * dist, r * 0.15f, Mathf.Sin(ang) * dist),
                Rotation = new Vector3(rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(0, Mathf.Tau), 0),
            });
        }
        for (int i = 0; i < 16; i++)   // crystals jutting out of them in clumps
        {
            float ang = rng.RandfRange(0, Mathf.Tau), dist = rng.RandfRange(0.1f, 2.4f), h = rng.RandfRange(1.2f, 3.2f) * (1.2f - dist * 0.25f);
            var c = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = rng.RandfRange(0.25f, 0.5f), Height = h, RadialSegments = 6, Rings = 1 },
                MaterialOverride = crystal,
            };
            c.Position = new Vector3(Mathf.Cos(ang) * dist, 0.4f + h * 0.4f, Mathf.Sin(ang) * dist);
            c.Rotation = new Vector3(Mathf.Sin(ang) * dist * 0.25f, rng.RandfRange(0, Mathf.Tau), -Mathf.Cos(ang) * dist * 0.25f);   // (leaning outward)
            root.AddChild(c);
        }
        return root;
    }

    // ---- napalm fires -----------------------------------------------------------------------------------------

    readonly List<Node3D> _fireViews = new();
    StandardMaterial3D _flameMat;
    Texture2D _scorch, _scorchGlow;

    /// <summary>A fire: a glowing scorch laid over the ground (a decal, so it follows the slope), with flames standing
    /// up out of it (camera-facing, additive, so they read as light).</summary>
    Node3D MakeFire()
    {
        if (_flameMat == null)
        {
            _scorch = Radial(new[] { new Color(0.05f, 0.03f, 0.02f, 0.95f), new Color(0.12f, 0.05f, 0.02f, 0.8f), new Color(0.1f, 0.06f, 0.04f, 0f) });
            _scorchGlow = Radial(new[] { new Color(0.55f, 0.18f, 0.03f), new Color(0.45f, 0.1f, 0.01f), new Color(0, 0, 0) });
            var flame = new GradientTexture2D
            {
                Width = 32, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.8f), FillTo = new Vector2(0.5f, 0.05f),
                Gradient = new Gradient   // (a teardrop: brightest low down, fading out all round)
                {
                    Offsets = new[] { 0f, 0.4f, 1f },
                    Colors = new[] { new Color(0.9f, 0.6f, 0.2f, 0.9f), new Color(0.75f, 0.28f, 0.04f, 0.6f), new Color(0.3f, 0.05f, 0f, 0f) },
                },
            };
            _flameMat = new StandardMaterial3D
            {
                AlbedoTexture = flame, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
            };
        }
        var fire = new Node3D();
        fire.AddChild(new Decal
        {
            Size = new Vector3(2, 3, 2), TextureAlbedo = _scorch, TextureEmission = _scorchGlow, EmissionEnergy = 1.3f,
            UpperFade = 0.3f, LowerFade = 0.3f,
        });
        for (int i = 0; i < 9; i++)
        {
            float ang = Mathf.Tau * i / 9 + i * 0.7f, dist = i == 0 ? 0 : 0.35f + 0.45f * ((i * 37) % 10) / 10f;
            fire.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(0.9f, 1.6f) }, MaterialOverride = _flameMat,
                Position = new Vector3(Mathf.Cos(ang) * dist, 0.8f, Mathf.Sin(ang) * dist),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        AddChild(fire);
        return fire;
    }

    static GradientTexture2D Radial(Color[] colours) => new()
    {
        Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient { Offsets = new[] { 0f, 0.6f, 1f }, Colors = colours },
    };

    void DrawFires()
    {
        var fires = W.Fires;
        while (_fireViews.Count < fires.Count) _fireViews.Add(MakeFire());
        float now = (float)Time.GetTicksMsec() * 0.001f;
        for (int i = 0; i < _fireViews.Count; i++)
        {
            var view = _fireViews[i];
            view.Visible = i < fires.Count;
            if (i >= fires.Count) continue;
            var f = fires[i];
            var at = ToV3(f.At);
            at.Y = _terrain.HeightAt(at.X, at.Z);
            float r = f.Radius.ToFloat(), life = Mathf.Clamp(f.TicksLeft / (float)World.FireTicks, 0, 1);
            view.Position = at;
            var scorch = view.GetChild<Decal>(0);
            scorch.Size = new Vector3(r * 2.2f, 3, r * 2.2f);
            scorch.EmissionEnergy = 0.3f + 1.0f * life;
            for (int k = 1; k < view.GetChildCount(); k++)   // flames flicker, and die down as it burns out
            {
                var flame = view.GetChild<MeshInstance3D>(k);
                float h = (0.75f + 0.35f * Mathf.Sin(now * 11 + k * 1.9f + i * 2.3f)) * (0.3f + 0.9f * life) * Mathf.Min(1.4f, 0.6f + r * 0.35f);
                float ang = Mathf.Tau * k / 9 + k * 0.7f, dist = (0.35f + 0.45f * ((k * 37) % 10) / 10f) * r;
                flame.Scale = new Vector3(0.8f + 0.2f * Mathf.Sin(now * 7 + k), h, 1);
                flame.Position = new Vector3(Mathf.Cos(ang) * dist, 0.8f * h, Mathf.Sin(ang) * dist);
            }
        }
    }
}
