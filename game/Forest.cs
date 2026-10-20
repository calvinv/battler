using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// The trees (the user, 2026-10-20: "improve the look of the trees and have them get knocked over when machines drive
/// over the terrain"). Four kinds, each one mesh built here in code and drawn as one MultiMesh: a layered pine, a tall
/// narrow fir, a round broadleaf, a bare dead snag. Foliage is shaded darker inside and lighter at the tips (vertex
/// colours), each tree a little different in shade and size, and it sways in the wind.
/// A vehicle that drives into a tree knocks it over: it tips away from the vehicle's heading, quickening as it goes,
/// lands with a small bounce, and stays down. Looks only: the sim knows nothing of trees.
/// </summary>
public partial class Forest : Node3D
{
    public enum Kind { Pine, Fir, Broadleaf, Dead }

    sealed class Tree
    {
        public Kind Kind;
        public int Index;            // its instance in its tile's MultiMesh
        public Vector3 Foot;
        public Basis Stand;          // its turn and size, standing
        public float Radius;         // how close a vehicle must come to hit it
        public bool Falling;
        public Vector3 Axis;         // what it tips about
        public float Age;            // seconds since it was hit
    }

    readonly List<Tree> _trees = new();
    readonly List<Tree> _falling = new();
    public int Knocked;   // trees knocked down so far (for checking)
    public Vector3? FirstKnocked;   // where the first went down (for a screenshot)
    readonly Dictionary<(int, int), List<Tree>> _grid = new();
    const float GridCell = 6f;
    readonly RandomNumberGenerator _rng = new() { Seed = 77 };
    // The trees are drawn in tiles, a MultiMesh per kind per tile, so the ones off screen aren't drawn at all (one
    // MultiMesh for the whole map drew every tree, every frame, and again for each shadow split).
    const float Tile = 64f;
    readonly Dictionary<(Kind, int, int), MultiMesh> _tiles = new();

    static (Kind, int, int) TileOf(Kind kind, Vector3 p) => (kind, Mathf.FloorToInt(p.X / Tile), Mathf.FloorToInt(p.Z / Tile));
    MultiMesh MeshOf(Tree t) => _tiles[TileOf(t.Kind, t.Foot)];

    /// <summary>Builds the trees at the given places (kind, foot on the ground, yaw, size).</summary>
    public void Plant(List<(Kind Kind, Vector3 Foot, float Yaw, float Size)> spots)
    {
        var counts = new Dictionary<(Kind, int, int), int>();
        foreach (var s in spots) { var key = TileOf(s.Kind, s.Foot); counts[key] = counts.GetValueOrDefault(key) + 1; }
        var shader = new Shader { Code = TreeShader };
        var material = new ShaderMaterial { Shader = shader };
        var meshes = new Mesh[4];
        for (int k = 0; k < 4; k++) meshes[k] = BuildMesh((Kind)k);
        foreach (var (key, count) in counts)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true,
                Mesh = meshes[(int)key.Item1], InstanceCount = count,
            };
            _tiles[key] = mm;
            AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
        }
        var next = new Dictionary<(Kind, int, int), int>();
        foreach (var (kind, foot, yaw, size) in spots)
        {
            var key = TileOf(kind, foot);
            int index = next.GetValueOrDefault(key);
            next[key] = index + 1;
            var t = new Tree
            {
                Kind = kind, Index = index, Foot = foot,
                Stand = new Basis(Vector3.Up, yaw).Scaled(Vector3.One * size),
                Radius = (kind == Kind.Broadleaf ? 0.9f : 0.6f) * size,
            };
            _trees.Add(t);
            var mm = _tiles[key];
            mm.SetInstanceTransform(t.Index, new Transform3D(t.Stand, foot));
            // Each a little different: a shade of its kind's colour, and where it is in the wind's swing.
            float shade = _rng.RandfRange(0.8f, 1.15f), warm = _rng.RandfRange(-0.06f, 0.06f);
            mm.SetInstanceColor(t.Index, new Color(shade + warm, shade, shade - warm));
            mm.SetInstanceCustomData(t.Index, new Color(1f, _rng.Randf() * Mathf.Tau, 0, 0));   // r: sways (1) or not; g: phase
            var cell = CellOf(foot);
            if (!_grid.TryGetValue(cell, out var list)) _grid[cell] = list = new List<Tree>();
            list.Add(t);
        }
    }

    /// <summary>Every tree standing again (a new match).</summary>
    public void Reset()
    {
        _falling.Clear();
        _grid.Clear();
        foreach (var t in _trees)
        {
            t.Falling = false;
            MeshOf(t).SetInstanceTransform(t.Index, new Transform3D(t.Stand, t.Foot));
            MeshOf(t).SetInstanceCustomData(t.Index, new Color(1f, _rng.Randf() * Mathf.Tau, 0, 0));
            var cell = CellOf(t.Foot);
            if (!_grid.TryGetValue(cell, out var list)) _grid[cell] = list = new List<Tree>();
            list.Add(t);
        }
    }

    static (int, int) CellOf(Vector3 p) => (Mathf.FloorToInt(p.X / GridCell), Mathf.FloorToInt(p.Z / GridCell));

    /// <summary>A vehicle of radius `radius` at `at`, heading `forward`: any standing tree it touches goes over.</summary>
    public void Trample(Vector3 at, Vector3 forward, float radius)
    {
        var (cx, cz) = CellOf(at);
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_grid.TryGetValue((cx + dx, cz + dz), out var list)) continue;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var t = list[i];
                    var gap = new Vector2(t.Foot.X - at.X, t.Foot.Z - at.Z);
                    float reach = radius + t.Radius;
                    if (gap.LengthSquared() > reach * reach) continue;
                    // It falls away from the vehicle: along its heading, pushed a little to whichever side it was on.
                    var away = forward + new Vector3(gap.X, 0, gap.Y).Normalized() * 0.5f;
                    away.Y = 0;
                    if (away.LengthSquared() < 1e-4f) away = forward;
                    t.Axis = Vector3.Up.Cross(away.Normalized()).Normalized();
                    t.Falling = true;
                    t.Age = 0;
                    MeshOf(t).SetInstanceCustomData(t.Index, new Color(0, 0, 0, 0));   // no more swaying
                    _falling.Add(t);
                    Knocked++;
                    FirstKnocked ??= t.Foot;
                    list.RemoveAt(i);   // (it can't be hit again)
                }
            }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _falling.Count - 1; i >= 0; i--)
        {
            var t = _falling[i];
            t.Age += dt;
            // Tips slowly at first, faster as it goes, to lying just off the ground (86 degrees), a small bounce at the end.
            const float Fall = 0.9f;
            float u = Mathf.Min(t.Age / Fall, 1f);
            float angle = 86f * u * u;
            if (t.Age > Fall) angle = 86f - 6f * Mathf.Sin(Mathf.Min((t.Age - Fall) / 0.25f, 1f) * Mathf.Pi);
            var tipped = new Basis(t.Axis, Mathf.DegToRad(angle)) * t.Stand;
            MeshOf(t).SetInstanceTransform(t.Index, new Transform3D(tipped, t.Foot));
            if (t.Age > Fall + 0.25f) _falling.RemoveAt(i);   // down for good
        }
    }

    // ---- the meshes --------------------------------------------------------------------------------------------

    /// <summary>A tree of the kind, about 1 unit = 1 metre, foot at the origin. Vertex colours are given as seen (sRGB).</summary>
    static ArrayMesh BuildMesh(Kind kind)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new RandomNumberGenerator { Seed = (ulong)kind + 11 };
        var bark = new Color(0.33f, 0.25f, 0.18f);
        switch (kind)
        {
            case Kind.Pine:
                Cone(st, Vector3.Zero, 0.17f, 0.08f, 2.2f, 6, bark, bark * 0.8f);
                // Five tiers of boughs, each a drooping skirt: wide and dark at its rim, narrow and light at its top.
                for (int i = 0; i < 5; i++)
                {
                    float y = 0.9f + i * 0.62f, r = 1.25f - i * 0.2f;
                    Skirt(st, new Vector3(rng.RandfRange(-0.05f, 0.05f), y, rng.RandfRange(-0.05f, 0.05f)), r, 1.05f, 9, rng,
                          new Color(0.14f, 0.26f, 0.14f), new Color(0.32f, 0.46f, 0.27f));
                }
                break;
            case Kind.Fir:
                Cone(st, Vector3.Zero, 0.15f, 0.06f, 3.2f, 6, bark, bark * 0.8f);
                for (int i = 0; i < 7; i++)
                {
                    float y = 0.8f + i * 0.6f, r = 0.85f - i * 0.1f;
                    Skirt(st, new Vector3(0, y, 0), r, 0.95f, 8, rng, new Color(0.1f, 0.2f, 0.15f), new Color(0.22f, 0.36f, 0.27f));
                }
                break;
            case Kind.Broadleaf:
                Cone(st, Vector3.Zero, 0.2f, 0.12f, 1.9f, 6, bark, bark * 0.85f);
                Cone(st, new Vector3(0, 1.4f, 0), 0.08f, 0.04f, 0.9f, 5, bark, bark, new Vector3(0.55f, 1, 0.1f));
                Cone(st, new Vector3(0, 1.5f, 0), 0.07f, 0.03f, 0.8f, 5, bark, bark, new Vector3(-0.45f, 1, -0.3f));
                // The crown: lumpy clumps of leaves around the top of the trunk.
                var dark = new Color(0.14f, 0.22f, 0.09f); var light = new Color(0.3f, 0.38f, 0.15f);
                Blob(st, new Vector3(0, 2.6f, 0), 1.25f, rng, dark, light);
                Blob(st, new Vector3(0.7f, 2.3f, 0.3f), 0.85f, rng, dark, light);
                Blob(st, new Vector3(-0.6f, 2.35f, -0.4f), 0.9f, rng, dark, light);
                Blob(st, new Vector3(0.1f, 3.2f, -0.2f), 0.8f, rng, dark, light);
                break;
            default:   // Dead: a bare grey snag, broken at the top, crooked limbs
                var grey = new Color(0.45f, 0.42f, 0.38f);
                Cone(st, Vector3.Zero, 0.17f, 0.05f, 2.9f, 6, grey * 0.8f, grey);
                Cone(st, new Vector3(0, 1.6f, 0), 0.06f, 0.02f, 1.1f, 4, grey, grey, new Vector3(0.9f, 0.7f, 0));
                Cone(st, new Vector3(0, 2.1f, 0), 0.05f, 0.015f, 0.8f, 4, grey, grey, new Vector3(-0.7f, 0.8f, 0.4f));
                Cone(st, new Vector3(0, 1.1f, 0), 0.05f, 0.02f, 0.7f, 4, grey, grey, new Vector3(0.2f, 0.6f, -0.9f));
                break;
        }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>A tapered pole from `foot` along `dir` (up if not given), `length` long, coloured `low` to `high`.</summary>
    static void Cone(SurfaceTool st, Vector3 foot, float r0, float r1, float length, int sides, Color low, Color high, Vector3? dir = null)
    {
        var d = (dir ?? Vector3.Up).Normalized();
        var u = d.Cross(Mathf.Abs(d.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        var v = d.Cross(u);
        var top = foot + d * length;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.Tau / sides, a1 = (i + 1) * Mathf.Tau / sides;
            Vector3 R(float a, float r) => (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
            var b0 = foot + R(a0, r0); var b1 = foot + R(a1, r0); var t0 = top + R(a0, r1); var t1 = top + R(a1, r1);
            Tri(st, b0, t0, b1, low, high, low);
            Tri(st, b1, t0, t1, low, high, high);
        }
    }

    /// <summary>A drooping skirt of boughs: a ring of points round its rim, a little below `at`, jagged, up to a tip.</summary>
    static void Skirt(SurfaceTool st, Vector3 at, float radius, float height, int points, RandomNumberGenerator rng, Color rim, Color tip)
    {
        var apex = at + Vector3.Up * height;
        var ring = new Vector3[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float a = i * Mathf.Tau / (points * 2) + rng.RandfRange(-0.08f, 0.08f);
            bool point = i % 2 == 0;   // alternately a bough tip reaching out and down, and a notch between
            float r = radius * (point ? rng.RandfRange(0.9f, 1.1f) : 0.62f);
            float drop = point ? rng.RandfRange(0.15f, 0.3f) : 0.02f;
            ring[i] = at + new Vector3(Mathf.Cos(a) * r, -drop, Mathf.Sin(a) * r);
        }
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Length];
            Tri(st, a, apex, b, rim, tip, rim);
        }
    }

    /// <summary>A lumpy ball of leaves: an icosahedron split once, its points pushed in and out.</summary>
    static void Blob(SurfaceTool st, Vector3 at, float radius, RandomNumberGenerator rng, Color dark, Color light)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var mids = new Dictionary<(int, int), int>();
        int Mid(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (mids.TryGetValue(key, out var m)) return m;
            v.Add((v[a] + v[b]) / 2);
            return mids[key] = v.Count - 1;
        }
        var faces = new List<int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        var p = new Vector3[v.Count];
        for (int i = 0; i < v.Count; i++) p[i] = at + v[i].Normalized() * radius * rng.RandfRange(0.82f, 1.12f) * new Vector3(1, 0.85f, 1);
        Color Shade(Vector3 q) => dark.Lerp(light, Mathf.Clamp((q.Y - at.Y) / radius * 0.5f + 0.5f, 0, 1));   // lighter on top
        for (int i = 0; i < faces.Count; i += 3)
        {
            var a = p[faces[i]]; var b = p[faces[i + 1]]; var c = p[faces[i + 2]];
            Tri(st, a, c, b, Shade(a), Shade(c), Shade(b));
        }
    }

    static void Tri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        st.SetColor(ca); st.AddVertex(a);
        st.SetColor(cb); st.AddVertex(b);
        st.SetColor(cc); st.AddVertex(c);
    }

    const string TreeShader = @"
shader_type spatial;
render_mode cull_disabled;
// Trees: vertex colours (sRGB, so made linear) times each tree's shade (the instance colour), swaying in the wind
// more toward the top, each tree in its own phase. INSTANCE_CUSTOM.r: 1 to sway, 0 once knocked down; .g its phase.
varying vec3 tint;
void vertex() {
	tint = pow(COLOR.rgb, vec3(2.2));   // (COLOR is the vertex colour times the instance colour)
	float sway = INSTANCE_CUSTOM.r * VERTEX.y * VERTEX.y * 0.006;
	float t = TIME * 1.3 + INSTANCE_CUSTOM.g;
	VERTEX.x += sin(t) * sway;
	VERTEX.z += sin(t * 0.7 + 1.3) * sway * 0.6;
}
void fragment() {
	ALBEDO = tint;
	BACKLIGHT = tint * 0.6;   // light through the foliage: the side away from the sun isn't black
	if (!FRONT_FACING) NORMAL = -NORMAL;
	ROUGHNESS = 0.9;
	SPECULAR = 0.2;
}";
}
