using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// Drawing the base networks, the sulfur patches and napalm fires (the user, 2026-10-22: "a base is like a single
/// structure that branches out ... a sulfur patch that could increase explosive damage ... or give you access to
/// napalm").
///   - Each link between two nodes (or a node and its HQ) is a conduit along the ground: the Coalition's a steel pipe
///     on little stands with a lit strip along its top; the Lattice's a low rusty tendril with an amber vein. The strip
///     goes dark when no power runs through it.
///   - Sulfur patches are clusters of yellow crystals.
///   - Napalm burns as a glowing patch with flames licking up, dying down as it burns out.
/// </summary>
public partial class Main
{
    Node3D _conduits;
    readonly Dictionary<(int, int), (MeshInstance3D Mesh, bool Lit)> _links = new();
    StandardMaterial3D _pipeMat, _tendrilMat, _stripLit, _stripDark, _veinLit, _veinDark;

    void ClearNetworkViews()
    {
        foreach (var l in _links.Values) l.Mesh.QueueFree();
        _links.Clear();
        foreach (var f in _fireViews) f.QueueFree();
        _fireViews.Clear();
    }

    void DrawNetwork()
    {
        if (_conduits == null)
        {
            _conduits = new Node3D();
            AddChild(_conduits);
            _pipeMat = new StandardMaterial3D { AlbedoColor = new Color(0.46f, 0.47f, 0.46f), Metallic = 0.5f, Roughness = 0.55f };
            _tendrilMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.32f, 0.18f), Metallic = 0.3f, Roughness = 0.75f };
            _stripLit = Glow(new Color(0.45f, 0.9f, 1f), 2.2f);
            _veinLit = Glow(new Color(1f, 0.6f, 0.15f), 3.0f);
            _stripDark = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.14f), Roughness = 0.7f };
            _veinDark = new StandardMaterial3D { AlbedoColor = new Color(0.14f, 0.1f, 0.08f), Roughness = 0.8f };
        }
        var keep = new HashSet<(int, int)>();
        foreach (var (a, b) in W.Links)
        {
            var ea = W.Find(a);
            var eb = W.Find(b);
            if (ea == null || eb == null || !Shown(ea) || !Shown(eb)) continue;
            bool lit = ea.Powered && eb.Powered && ea.Building == 0 && eb.Building == 0;
            bool lattice = W.Players[ea.Owner].Civ == Civ.Lattice;
            keep.Add((a, b));
            if (!_links.TryGetValue((a, b), out var link))
            {
                link = (new MeshInstance3D { Mesh = Conduit(ea, eb, lattice) }, !lit);
                _conduits.AddChild(link.Mesh);
                // Trees in its way come down as it's laid.
                var from = ToV3(ea.Pos);
                var along = ToV3(eb.Pos) - from;
                for (float t = 0; t <= 1; t += 1.5f / Mathf.Max(along.Length(), 1.5f))
                    _forest.Trample(from + along * t, along.Normalized(), 0.6f);
            }
            if (link.Lit != lit)
                link.Mesh.SetSurfaceOverrideMaterial(1, lattice ? (lit ? _veinLit : _veinDark) : (lit ? _stripLit : _stripDark));
            link.Mesh.SetSurfaceOverrideMaterial(0, lattice ? _tendrilMat : _pipeMat);
            _links[(a, b)] = (link.Mesh, lit);
        }
        var gone = new List<(int, int)>();
        foreach (var key in _links.Keys) if (!keep.Contains(key)) gone.Add(key);
        foreach (var key in gone)
        {
            _links[key].Mesh.QueueFree();
            _links.Remove(key);
        }
    }

    static StandardMaterial3D Glow(Color c, float energy) => new()
    {
        AlbedoColor = c * 0.4f, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy, Roughness = 0.4f,
    };

    /// <summary>The conduit between two nodes: a tube following the ground between their edges (surface 0), with a thin
    /// strip along its top (surface 1) that lights up with power. The Coalition's stands on little posts.</summary>
    ArrayMesh Conduit(Entity a, Entity b, bool lattice)
    {
        var from = ToV3(a.Pos);
        var to = ToV3(b.Pos);
        var dir = (to - from).Normalized();
        from += dir * (a.Stats.Radius.ToFloat() * 0.8f);
        to -= dir * (b.Stats.Radius.ToFloat() * 0.8f);
        float length = from.DistanceTo(to);
        int n = Mathf.Max(2, (int)(length / 1.2f));
        float lift = lattice ? 0.3f : 0.48f, r = lattice ? 0.3f : 0.17f;
        var side = dir.Cross(Vector3.Up).Normalized();
        var points = new List<Vector3>();
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            var p = from.Lerp(to, t);
            if (lattice) p += side * Mathf.Sin(t * Mathf.Pi * 3 + a.Id) * 0.35f * Mathf.Sin(t * Mathf.Pi);   // (a tendril wanders)
            p.Y = _terrain.HeightAt(p.X, p.Z) + lift;
            points.Add(p);
        }
        var mesh = new ArrayMesh();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Tube(st, points, r, lattice ? 6 : 8);
        if (!lattice)   // posts holding the pipe up, every few metres
            for (int i = 1; i < points.Count - 1; i += 4)
            {
                var top = points[i];
                Tube(st, new List<Vector3> { top with { Y = top.Y - lift - 0.1f }, top }, 0.07f, 4);
            }
        st.GenerateNormals();
        st.Commit(mesh);
        var strip = new List<Vector3>();
        foreach (var p in points) strip.Add(p + Vector3.Up * r * 0.85f);
        st.Begin(Mesh.PrimitiveType.Triangles);
        Tube(st, strip, r * 0.32f, 4);
        st.GenerateNormals();
        st.Commit(mesh);
        return mesh;
    }

    /// <summary>A tube of `sides` faces along `points`.</summary>
    static void Tube(SurfaceTool st, List<Vector3> points, float r, int sides)
    {
        var rings = new List<Vector3[]>();
        for (int i = 0; i < points.Count; i++)
        {
            var t = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).Normalized();
            var up = Mathf.Abs(t.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
            var u = t.Cross(up).Normalized();
            var v = u.Cross(t).Normalized();
            var ring = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float ang = Mathf.Tau * k / sides;
                ring[k] = points[i] + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * r;
            }
            rings.Add(ring);
        }
        for (int i = 0; i + 1 < rings.Count; i++)
            for (int k = 0; k < sides; k++)
            {
                int k2 = (k + 1) % sides;
                var a = rings[i][k]; var b = rings[i][k2]; var c = rings[i + 1][k]; var d = rings[i + 1][k2];
                st.AddVertex(a); st.AddVertex(c); st.AddVertex(b);
                st.AddVertex(b); st.AddVertex(c); st.AddVertex(d);
            }
    }

    // ---- sulfur ----------------------------------------------------------------------------------------------

    /// <summary>A sulfur patch: a low pale rock bristling with yellow crystals that glow a little.</summary>
    static Node3D MakeSulfur(int seed)
    {
        var root = new Node3D();
        var rock = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.31f, 0.2f), Roughness = 0.95f };
        var crust = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.62f, 0.22f), Roughness = 0.9f };
        var crystal = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.15f), Roughness = 0.25f, Metallic = 0.1f,
            EmissionEnabled = true, Emission = new Color(1f, 0.78f, 0.1f), EmissionEnergyMultiplier = 0.9f,
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
