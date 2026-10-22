using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The landscape as drawn: the sim's ground (heights that matter in the fight: sim/Ground.cs), coloured, rolling ground
/// that's flat round the bases and gentle down the middle lane, ponds, dead trees and scrub pines, rocks. All of it
/// fixed, the same every match. Things are stood on it with <see cref="HeightAt"/>.
/// (The user, 2026-10-19: "work on landscape art and trees and ponds and rocks, some elevation would be good".)
/// </summary>
public sealed class Terrain : System.IDisposable
{
    const float Margin = Ground.Margin;   // ground past the map's edge, so the view doesn't end in a cliff of nothing
    static readonly Vector2[] Bases = { V(World.HqStart(0)), V(World.HqStart(1)) };
    static Vector2 V(FixVec2 p) => new(p.X.ToFloat(), p.Z.ToFloat());
    static readonly (Vector2 At, float R)[] Ponds = System.Array.ConvertAll(Ground.Ponds, p => (V(p.At), p.R.ToFloat()));

    /// <summary>How far out into the water a place is (Ground.WaterDepth; negative on land).</summary>
    static float WaterDepth(float x, float z) => Ground.WaterDepthF(x, z);
    public const float WaterLevel = -0.75f;

    readonly FastNoiseLite _noise = new()
    {
        Seed = 7, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 0.03f,
        FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 3,
    };
    readonly float _halfX, _halfZ;

    public Terrain(float halfX, float halfZ) { _halfX = halfX; _halfZ = halfZ; }

    public void Dispose() => _noise.Dispose();

    // ---- the drawn ground (the user, 2026-10-20: "higher definition battlefields") --------------------------------
    // The sim keeps one height per world unit (sim/Ground.cs) and fights on those. What's drawn is finer: the same
    // heights joined by smooth curves (Catmull-Rom between the sim's points), with a little relief on top (ruts and
    // bumps a few hands high), on a mesh of Res points per unit. Everything stood on the ground uses HeightAt, so units
    // and buildings sit on what's drawn. Looks only: the fight still uses the sim's heights.

    const int Res = 2;                   // mesh points per world unit (two: the map is four times the area it was)
    const float ReliefHigh = 0.14f;      // how high the small bumps go
    float[] _grid;                       // the sim's heights at whole units, from (_gx0, _gz0)
    int _gw, _gh;
    float _gx0, _gz0;

    void EnsureGrid()
    {
        if (_grid != null) return;
        _gx0 = -_halfX - Margin; _gz0 = -_halfZ - Margin;
        _gw = (int)((_halfX + Margin) * 2) + 1; _gh = (int)((_halfZ + Margin) * 2) + 1;
        _grid = new float[_gw * _gh];
        for (int j = 0; j < _gh; j++)
            for (int i = 0; i < _gw; i++) _grid[j * _gw + i] = World.Ground.HeightAtF(_gx0 + i, _gz0 + j);
    }

    float G(int i, int j) => _grid[System.Math.Clamp(j, 0, _gh - 1) * _gw + System.Math.Clamp(i, 0, _gw - 1)];

    static float CatmullRom(float p0, float p1, float p2, float p3, float t) =>
        p1 + 0.5f * t * (p2 - p0 + t * (2 * p0 - 5 * p1 + 4 * p2 - p3 + t * (3 * (p1 - p2) + p3 - p0)));

    /// <summary>The sim's heights joined smoothly (no relief).</summary>
    public float SmoothAt(float x, float z)
    {
        EnsureGrid();
        float gx = x - _gx0, gz = z - _gz0;
        int i = (int)Mathf.Floor(gx), j = (int)Mathf.Floor(gz);
        float tx = gx - i, tz = gz - j;
        float Row(int r) => CatmullRom(G(i - 1, r), G(i, r), G(i + 1, r), G(i + 2, r), tx);
        return CatmullRom(Row(j - 1), Row(j), Row(j + 1), Row(j + 2), tz);
    }

    /// <summary>The ground as drawn: smooth, with its small bumps. Stand things on this.</summary>
    public float HeightAt(float x, float z) => SmoothAt(x, z) + Relief(x, z);

    /// <summary>Where the trees stand, filled by BuildDecor: kind, foot, yaw, size (Forest plants them).</summary>
    public readonly List<(Forest.Kind Kind, Vector3 Foot, float Yaw, float Size)> TreeSpots = new();

    /// <summary>Tread marks lie on the drawn ground (it's fine enough now that its triangles hug HeightAt).</summary>
    public float SurfaceAt(float x, float z) => HeightAt(x, z);

    /// <summary>Small bumps and ruts: two octaves of value noise, flattened under the bases and in the ponds.</summary>
    float Relief(float x, float z)
    {
        float n = ValueNoise(x * 0.45f, z * 0.45f) * 0.65f + ValueNoise(x * 1.3f + 31f, z * 1.3f + 17f) * 0.35f;
        float calm = 1f;
        foreach (var b in Bases) calm = Mathf.Min(calm, Mathf.Clamp((new Vector2(x, z).DistanceTo(b) - 22f) / 10f, 0f, 1f));
        return (n - 0.5f) * 2f * ReliefHigh * calm;
    }

    static float Hash(int x, int y)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / 4294967295f;
    }

    static float ValueNoise(float x, float y)
    {
        int ix = (int)Mathf.Floor(x), iy = (int)Mathf.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>How sheltered a spot is, 0..1 (0.5 on open ground): above it is its surroundings' average height over
    /// about four units, so hollows and valley floors read darker and ridges lighter (the shader's ambient shading).</summary>
    float Hollow(float x, float z)
    {
        float h = SmoothAt(x, z), around = 0;
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.Tau / 8;
            around += SmoothAt(x + Mathf.Cos(a) * 4f, z + Mathf.Sin(a) * 4f);
        }
        return Mathf.Clamp(0.5f + (around / 8 - h) * 0.35f, 0f, 1f);
    }

    /// <summary>How wet the ground is from a pond: 1 at its edge, nothing three units out.</summary>
    static float Wet(float x, float z)
    {
        float wet = 0;
        foreach (var (at, r) in Ponds) wet = Mathf.Max(wet, 1f - Mathf.Clamp((new Vector2(x, z).DistanceTo(at) - r) / 3f, 0f, 1f));
        wet = Mathf.Max(wet, 1f - Mathf.Clamp(-WaterDepth(x, z) / 3f, 0f, 1f));   // (about three units of shore)
        return wet;
    }

    /// <summary>Where scrub grows: low, damp, flat ground in patches (the shader paints grass there; tufts stand on it).</summary>
    public float Grassy(float x, float z)
    {
        if (Map.Test) return 0;   // (the test map: bare dirt)
        float patch = ValueNoise(x * 0.09f + 5f, z * 0.09f - 3f) * 0.7f + ValueNoise(x * 0.3f, z * 0.3f) * 0.3f;
        float low = Mathf.Clamp(0.6f - SmoothAt(x, z) * 0.35f, 0f, 1f);
        return Mathf.Clamp((patch - 0.48f) * 4f, 0f, 1f) * low;
    }

    /// <summary>The ground mesh: Res points per unit, smooth normals from the heights, and in its vertex colours what the
    /// shader needs per place: r how sheltered (Hollow), g how wet (by a pond), b how grassy.</summary>
    public MeshInstance3D BuildGround(Material material)
    {
        float x0 = -_halfX - Margin, z0 = -_halfZ - Margin;
        int nx = (int)((_halfX + Margin) * 2) * Res + 1, nz = (int)((_halfZ + Margin) * 2) * Res + 1;
        const float Step = 1f / Res;
        var heights = new float[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++) heights[j * nx + i] = HeightAt(x0 + i * Step, z0 + j * Step);
        var verts = new Vector3[nx * nz];
        var normals = new Vector3[nx * nz];
        var colours = new Color[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i;
                float x = x0 + i * Step, z = z0 + j * Step;
                verts[k] = new Vector3(x, heights[k], z);
                float hl = heights[j * nx + System.Math.Max(i - 1, 0)], hr = heights[j * nx + System.Math.Min(i + 1, nx - 1)];
                float hd = heights[System.Math.Max(j - 1, 0) * nx + i], hu = heights[System.Math.Min(j + 1, nz - 1) * nx + i];
                normals[k] = new Vector3(hl - hr, 2 * Step, hd - hu).Normalized();
                colours[k] = new Color(Hollow(x, z), Wet(x, z), Grassy(x, z));
            }
        var indices = new int[(nx - 1) * (nz - 1) * 6];
        int n = 0;
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx + 1, d = a + nx;
                indices[n++] = a; indices[n++] = b; indices[n++] = c;
                indices[n++] = a; indices[n++] = c; indices[n++] = d;
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colours;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
    }

    /// <summary>Ponds' water, trees and rocks, placed the same way every time, clear of the bases and the lane.</summary>
    public Node3D BuildDecor(Material water)
    {
        var root = new Node3D { Name = "Decor" };
        foreach (var (at, r) in Ponds)
            root.AddChild(new MeshInstance3D { Mesh = Disc(r + 0.9f, 48), MaterialOverride = water, Position = new Vector3(at.X, WaterLevel, at.Y) });
        // The north and south water: a sheet each following its shore out past the map's edge (the south one is the
        // north one turned round, as the map is). Its UV.x runs 1 at the shore's edge (just up the bank, fading out)
        // to 0 far out, as water_fog.gdshader expects of a pond.
        if (Map.Test) { TreeSpots.Clear(); return root; }   // (the test map: bare dirt, nothing on it)
        var sheet = WaterSheet();
        root.AddChild(new MeshInstance3D { Mesh = sheet, MaterialOverride = water, Position = new Vector3(0, WaterLevel, 0) });
        root.AddChild(new MeshInstance3D { Mesh = sheet, MaterialOverride = water, Position = new Vector3(0, WaterLevel, 0), Rotation = new Vector3(0, Mathf.Pi, 0) });

        var rng = new RandomNumberGenerator { Seed = 1234 };
        var rocks = new List<Transform3D>();
        bool Clear(Vector2 p, float room)
        {
            foreach (var b in Bases) if (p.DistanceTo(b) < 31) return false;
            foreach (var (at, r) in Ponds) if (p.DistanceTo(at) < r + room) return false;
            if (WaterDepth(p.X, p.Y) > -2f - room) return false;                                     // not in the water
            foreach (var d in World.Deposits) if (p.DistanceTo(new Vector2(d.X.ToFloat(), d.Z.ToFloat())) < 5 + room) return false;   // room to mine
            foreach (var v in World.Vents) if (p.DistanceTo(new Vector2(v.X.ToFloat(), v.Z.ToFloat())) < 5 + room) return false;     // and for power plants
            for (int i = 0; i < World.Pads.Length; i++)                                               // nor on an outpost pad
                if (World.HasPad[i] && p.DistanceTo(new Vector2(World.Pads[i].X.ToFloat(), World.Pads[i].Z.ToFloat())) < 6 + room) return false;
            return Mathf.Abs(p.X) < _halfX + Margin - 2 && Mathf.Abs(p.Y) < _halfZ + Margin - 2;
        }
        Transform3D Stand(Vector2 p, float scale, float lean = 0f)
        {
            var basis = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)) * new Basis(Vector3.Right, lean);
            return new Transform3D(basis.Scaled(Vector3.One * scale), new Vector3(p.X, HeightAt(p.X, p.Y) - 0.1f, p.Y));
        }
        // Copses, in clumps: mostly one kind each, now and then another among them (Forest draws them).
        TreeSpots.Clear();
        for (int c = 0; c < 800; c++)
        {
            var centre = new Vector2(rng.RandfRange(-_halfX - 6, _halfX + 6), rng.RandfRange(-_halfZ - 6, _halfZ + 6));   // mostly on the map
            float roll = rng.Randf();
            var main = roll < 0.35f ? Forest.Kind.Pine : roll < 0.6f ? Forest.Kind.Fir : roll < 0.82f ? Forest.Kind.Broadleaf : Forest.Kind.Dead;
            int n = rng.RandiRange(3, 10);
            for (int i = 0; i < n; i++)
            {
                var p = centre + new Vector2(rng.RandfRange(-5f, 5f), rng.RandfRange(-5f, 5f));
                if (!Clear(p, 1.5f)) continue;
                var kind = rng.Randf() < 0.8f ? main : (Forest.Kind)rng.RandiRange(0, 3);
                TreeSpots.Add((kind, new Vector3(p.X, HeightAt(p.X, p.Y) - 0.05f, p.Y), rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.75f, 1.3f)));
            }
        }
        // Rocks: scattered, and a ring of boulders round each pond.
        for (int i = 0; i < 1500; i++)
        {
            var p = new Vector2(rng.RandfRange(-_halfX - Margin, _halfX + Margin), rng.RandfRange(-_halfZ - Margin, _halfZ + Margin));
            if (Clear(p, 0.8f)) rocks.Add(Stand(p, rng.RandfRange(0.4f, 1.4f)));
        }
        foreach (var (at, r) in Ponds)
            for (int i = 0; i < 6; i++)
            {
                float a = rng.RandfRange(0, Mathf.Tau);
                var p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + rng.RandfRange(0.8f, 2f));
                if (Mathf.Abs(p.Y) > 13) rocks.Add(Stand(p, rng.RandfRange(0.5f, 1.1f)));
            }

        var rock = Flat(new Color(0.5f, 0.47f, 0.43f));
        // Rock: a lumpy low ball.
        root.AddChild(Many(new SphereMesh { Radius = 0.7f, Height = 0.9f, RadialSegments = 6, Rings = 3 }, rock, rocks, new Vector3(0, 0.2f, 0), scale: new Vector3(1.3f, 0.8f, 1f)));
        root.AddChild(GrassTufts(rng));
        // Pebbles strewn on the open ground: small, many, a little detail at any zoom.
        var pebbles = new List<Transform3D>();
        for (int i = 0; i < 16000; i++)
        {
            var p = new Vector2(rng.RandfRange(-_halfX - Margin, _halfX + Margin), rng.RandfRange(-_halfZ - Margin, _halfZ + Margin));
            if (!Clear(p, 0.3f) && rng.Randf() < 0.85f) continue;   // (a few even in the lane)
            var basis = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)).Scaled(new Vector3(rng.RandfRange(0.12f, 0.3f), rng.RandfRange(0.08f, 0.18f), rng.RandfRange(0.12f, 0.3f)));
            pebbles.Add(new Transform3D(basis, new Vector3(p.X, HeightAt(p.X, p.Y), p.Y)));
        }
        root.AddChild(Many(new SphereMesh { Radius = 1f, Height = 1.2f, RadialSegments = 5, Rings = 2 }, Flat(new Color(0.42f, 0.4f, 0.37f)), pebbles, Vector3.Zero));
        return root;
    }


    /// <summary>Tufts of scrub grass standing in the grassy places (where the shader paints grass): crossed cards with
    /// blades cut out of them, each its own shade and size, swaying a little. Many thousands, in one MultiMesh.</summary>
    MultiMeshInstance3D GrassTufts(RandomNumberGenerator rng)
    {
        // The blades: a 64x64 card of thin tapering strands, see-through between them.
        var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var brng = new RandomNumberGenerator { Seed = 5 };
        for (int b = 0; b < 14; b++)
        {
            float x0 = brng.RandfRange(8, 56), lean = brng.RandfRange(-14, 14), top = brng.RandfRange(20, 62), w = brng.RandfRange(1.5f, 3f);
            for (int y = 0; y < (int)top; y++)
            {
                float t = y / top, x = x0 + lean * t * t, half = w * (1 - t);
                for (int px = (int)(x - half - 1); px <= (int)(x + half + 1); px++)
                    if (px >= 0 && px < 64 && Mathf.Abs(px - x) <= half + 0.5f)
                        img.SetPixel(px, 63 - y, new Color(0.75f + 0.25f * t, 0.8f + 0.2f * t, 0.6f, 1));   // lighter toward the tips
            }
        }
        var card = new ArrayMesh();
        {
            // Two cards crossed at right angles, a unit wide and tall, standing on the ground.
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var idx = new List<int>();
            foreach (var dir in new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 1) })
            {
                int o = v.Count;
                v.Add(-dir * 0.5f); v.Add(dir * 0.5f); v.Add(dir * 0.5f + Vector3.Up); v.Add(-dir * 0.5f + Vector3.Up);
                uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                for (int k = 0; k < 4; k++) n.Add(Vector3.Up);   // lit like the ground under them, not like flat cards
                idx.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = v.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = n.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
            card.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        var spots = new List<(Transform3D At, Color Tint)>();
        for (int i = 0; i < 600000 && spots.Count < 100000; i++)
        {
            var p = new Vector2(rng.RandfRange(-_halfX - Margin, _halfX + Margin), rng.RandfRange(-_halfZ - Margin, _halfZ + Margin));
            float g = Grassy(p.X, p.Y);
            if (g < 0.25f || rng.Randf() > g) continue;
            bool nearBase = false;
            foreach (var b in Bases) if (p.DistanceTo(b) < 26) nearBase = true;
            foreach (var (at, r) in Ponds) if (p.DistanceTo(at) < r + 0.6f) nearBase = true;   // (nor in the water)
            if (WaterDepth(p.X, p.Y) > -1f) nearBase = true;
            if (nearBase) continue;
            float size = rng.RandfRange(0.35f, 0.8f) * (0.6f + 0.6f * g);
            var basis = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)).Scaled(new Vector3(size * 1.3f, size, size * 1.3f));
            var tint = new Color(0.14f, 0.19f, 0.07f).Lerp(new Color(0.26f, 0.27f, 0.11f), rng.Randf());
            spots.Add((new Transform3D(basis, new Vector3(p.X, HeightAt(p.X, p.Y) - 0.05f, p.Y)), tint));
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = card, InstanceCount = spots.Count };
        for (int i = 0; i < spots.Count; i++) { mm.SetInstanceTransform(i, spots[i].At); mm.SetInstanceColor(i, spots[i].Tint); }
        var mat = new ShaderMaterial { Shader = new Shader { Code = GrassShader } };
        mat.SetShaderParameter("blades", ImageTexture.CreateFromImage(img));
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    const string GrassShader = @"
shader_type spatial;
render_mode cull_disabled, specular_disabled;
uniform sampler2D blades : source_color, filter_linear_mipmap;
void vertex() {
	// A little sway at the tips, each tuft out of step with the next.
	vec3 at = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float sway = sin(TIME * 1.6 + at.x * 0.7 + at.z * 0.5) * 0.08 * (1.0 - UV.y);   // (UV.y is 0 at the tips)
	VERTEX.x += sway;
}
void fragment() {
	vec4 c = texture(blades, UV);
	if (c.a < 0.5) discard;
	if (!FRONT_FACING) NORMAL = -NORMAL;   // both sides lit as from above (the cards are double-sided)
	ALBEDO = c.rgb * COLOR.rgb * 1.15;
	ROUGHNESS = 0.9;
}";
    static StandardMaterial3D Flat(Color c) => new() { AlbedoColor = c, Roughness = 0.95f };

    /// <summary>The north water's surface: strips from just up the shore out to beyond the map's top edge, in rows
    /// whose UV.x falls from 1 (the shore) to 0 (open water).</summary>
    ArrayMesh WaterSheet()
    {
        // Rows by how far out from the shore line (negative: up the bank) and the UV.x each gets.
        (float Out, float U)[] rows = { (-2.5f, 1f), (1.5f, 0.9f), (5f, 0.8f), (12f, 0.55f), (30f, 0.25f), (float.MaxValue, 0f) };   // (the last: the far edge)
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var idx = new List<int>();
        float edge = _halfX + Margin + 5;
        int columns = 0;
        for (float x = -edge; x <= edge + 0.01f; x += 2f, columns++)
        {
            float shore = Ground.NorthShore(Fix.FromFloat(x)).ToFloat();
            foreach (var (o, u) in rows)
            {
                verts.Add(new Vector3(x, 0, Mathf.Max(shore - o, -_halfZ - Margin)));   // north of the shore is smaller z; not past the ground's edge
                uvs.Add(new Vector2(u, 0));
                normals.Add(Vector3.Up);
            }
        }
        int n = rows.Length;
        for (int c = 0; c < columns - 1; c++)
            for (int r = 0; r < n - 1; r++)
            {
                int a = c * n + r, b = (c + 1) * n + r, cc = (c + 1) * n + r + 1, d = c * n + r + 1;
                idx.AddRange(new[] { a, cc, b, a, d, cc });   // wound to face up (clockwise seen from above)
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>A flat disc facing up, in rings, its UV.x how far out a point is (0 middle, 1 rim): water_fog.gdshader
    /// shades by it.</summary>
    static ArrayMesh Disc(float radius, int sides, int rings = 6)
    {
        var verts = new List<Vector3> { Vector3.Zero };
        var uvs = new List<Vector2> { Vector2.Zero };
        var idx = new List<int>();
        for (int r = 1; r <= rings; r++)
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.Tau / sides, f = r / (float)rings;
                verts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * f);
                uvs.Add(new Vector2(f, 0));
            }
        int At(int r, int s) => r == 0 ? 0 : 1 + (r - 1) * sides + (s % sides);
        for (int s = 0; s < sides; s++) { idx.Add(0); idx.Add(At(1, s)); idx.Add(At(1, s + 1)); }   // (wound to face up)
        for (int r = 1; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                idx.Add(At(r, s)); idx.Add(At(r + 1, s + 1)); idx.Add(At(r, s + 1));
                idx.Add(At(r, s)); idx.Add(At(r + 1, s)); idx.Add(At(r + 1, s + 1));
            }
        var normals = new Vector3[verts.Count];
        System.Array.Fill(normals, Vector3.Up);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>One mesh drawn at many places (a MultiMesh), each offset, turned and scaled as given in its place.</summary>
    static MultiMeshInstance3D Many(Mesh mesh, Material mat, List<Transform3D> at, Vector3 offset, Vector3? tilt = null, Vector3? scale = null)
    {
        var local = new Transform3D(Basis.FromEuler(tilt ?? Vector3.Zero).Scaled(scale ?? Vector3.One), offset);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = at.Count };
        for (int i = 0; i < at.Count; i++) mm.SetInstanceTransform(i, at[i] * local);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat };
    }
}
