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
    public const float WaterLevel = -0.75f;

    readonly FastNoiseLite _noise = new()
    {
        Seed = 7, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 0.03f,
        FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 3,
    };
    readonly float _halfX, _halfZ;

    public Terrain(float halfX, float halfZ) { _halfX = halfX; _halfZ = halfZ; }

    public void Dispose() => _noise.Dispose();

    /// <summary>The sim's ground (Battler.Sim.Ground): what's drawn is what the fight uses.</summary>
    public float HeightAt(float x, float z) => World.Ground.HeightAtF(x, z);

    /// <summary>The height of the drawn ground exactly: its flat triangles (corners a b c and a c d of each unit square,
    /// as <see cref="BuildGround"/> lays them), which can sit a little off the smooth <see cref="HeightAt"/>. For
    /// things laid flat on it, like tread marks.</summary>
    public float SurfaceAt(float x, float z)
    {
        float gx = x + _halfX + Margin, gz = z + _halfZ + Margin;
        float i = Mathf.Floor(gx), j = Mathf.Floor(gz), fx = gx - i, fz = gz - j;
        float x0 = i - _halfX - Margin, z0 = j - _halfZ - Margin;
        float a = HeightAt(x0, z0), b = HeightAt(x0 + 1, z0), c = HeightAt(x0 + 1, z0 + 1), d = HeightAt(x0, z0 + 1);
        return fx >= fz ? a + (b - a) * fx + (c - b) * fz : a + (c - d) * fx + (d - a) * fz;
    }

    /// <summary>The ground mesh, coloured by height and slope (vertex colours: the fog shader multiplies them).</summary>
    public MeshInstance3D BuildGround(Material material)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float x0 = -_halfX - Margin, z0 = -_halfZ - Margin;
        int nx = (int)((_halfX + Margin) * 2), nz = (int)((_halfZ + Margin) * 2);
        var dust = new Color(0.47f, 0.42f, 0.33f);
        var scrub = new Color(0.36f, 0.37f, 0.25f);    // low, damp ground: a little green
        var stone = new Color(0.55f, 0.52f, 0.46f);    // high and steep: bare rock
        Vector3 P(int i, int j) { float x = x0 + i, z = z0 + j; return new Vector3(x, HeightAt(x, z), z); }
        Color C(Vector3 p)
        {
            float slope = Mathf.Abs(HeightAt(p.X + 1, p.Z) - p.Y) + Mathf.Abs(HeightAt(p.X, p.Z + 1) - p.Y);
            var c = dust.Lerp(scrub, Mathf.Clamp(-p.Y / 1.6f, 0, 1));
            c = c.Lerp(stone, Mathf.Clamp((p.Y - 1.2f) / 2f + slope * 0.6f, 0, 1));
            return c;   // (fine mottling is done in ground_fog.gdshader: per-corner grain drew dashes along the triangle edges)
        }
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                var a = P(i, j); var b = P(i + 1, j); var c = P(i + 1, j + 1); var d = P(i, j + 1);
                foreach (var v in new[] { a, b, c, a, c, d }) { st.SetColor(C(v)); st.AddVertex(v); }
            }
        st.Index();   // weld shared corners, so the normals come out smooth rather than one per triangle
        st.GenerateNormals();
        return new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = material };
    }

    /// <summary>Ponds' water, trees and rocks, placed the same way every time, clear of the bases and the lane.</summary>
    public Node3D BuildDecor(Material water)
    {
        var root = new Node3D { Name = "Decor" };
        foreach (var (at, r) in Ponds)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = r + 0.6f, BottomRadius = r + 0.6f, Height = 0.05f, RadialSegments = 20 },
                MaterialOverride = water,
                Position = new Vector3(at.X, WaterLevel, at.Y),
            });

        var rng = new RandomNumberGenerator { Seed = 1234 };
        var pines = new List<Transform3D>();
        var dead = new List<Transform3D>();
        var rocks = new List<Transform3D>();
        bool Clear(Vector2 p, float room)
        {
            foreach (var b in Bases) if (p.DistanceTo(b) < 31) return false;
            if (Mathf.Abs(p.Y) < 13 && Mathf.Abs(p.X) < _halfX) return false;   // the lane the armies use
            foreach (var (at, r) in Ponds) if (p.DistanceTo(at) < r + room) return false;
            foreach (var d in World.Deposits) if (p.DistanceTo(new Vector2(d.X.ToFloat(), d.Z.ToFloat())) < 5 + room) return false;   // room to mine
            return Mathf.Abs(p.X) < _halfX + Margin - 2 && Mathf.Abs(p.Y) < _halfZ + Margin - 2;
        }
        Transform3D Stand(Vector2 p, float scale, float lean = 0f)
        {
            var basis = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)) * new Basis(Vector3.Right, lean);
            return new Transform3D(basis.Scaled(Vector3.One * scale), new Vector3(p.X, HeightAt(p.X, p.Y) - 0.1f, p.Y));
        }
        // Copses: a few pines or dead trees each, in clumps.
        for (int c = 0; c < 80; c++)
        {
            var centre = new Vector2(rng.RandfRange(-_halfX - 6, _halfX + 6), rng.RandfRange(-_halfZ - 6, _halfZ + 6));   // mostly on the map
            bool living = rng.Randf() < 0.45f;
            int n = rng.RandiRange(3, 9);
            for (int i = 0; i < n; i++)
            {
                var p = centre + new Vector2(rng.RandfRange(-4.5f, 4.5f), rng.RandfRange(-4.5f, 4.5f));
                if (!Clear(p, 1.5f)) continue;
                if (living) pines.Add(Stand(p, rng.RandfRange(0.8f, 1.35f)));
                else dead.Add(Stand(p, rng.RandfRange(0.8f, 1.3f), rng.RandfRange(-0.15f, 0.15f)));
            }
        }
        // Rocks: scattered, and a ring of boulders round each pond.
        for (int i = 0; i < 150; i++)
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

        var bark = Flat(new Color(0.28f, 0.22f, 0.17f));
        var needles = Flat(new Color(0.24f, 0.32f, 0.2f));
        var greyBark = Flat(new Color(0.36f, 0.33f, 0.3f));
        var rock = Flat(new Color(0.5f, 0.47f, 0.43f));
        // Pine: a trunk and two stacked cones. Dead tree: a tall bare trunk with a crooked limb. Rock: a lumpy low ball.
        root.AddChild(Many(new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.16f, Height = 1.2f, RadialSegments = 5 }, bark, pines, new Vector3(0, 0.6f, 0)));
        root.AddChild(Many(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.9f, Height = 1.8f, RadialSegments = 7 }, needles, pines, new Vector3(0, 1.6f, 0)));
        root.AddChild(Many(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.65f, Height = 1.4f, RadialSegments = 7 }, needles, pines, new Vector3(0, 2.4f, 0)));
        root.AddChild(Many(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.16f, Height = 2.6f, RadialSegments = 5 }, greyBark, dead, new Vector3(0, 1.3f, 0)));
        root.AddChild(Many(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.07f, Height = 1.1f, RadialSegments = 4 }, greyBark, dead, new Vector3(0.3f, 1.9f, 0), new Vector3(0, 0, -0.9f)));
        root.AddChild(Many(new SphereMesh { Radius = 0.7f, Height = 0.9f, RadialSegments = 6, Rings = 3 }, rock, rocks, new Vector3(0, 0.2f, 0), scale: new Vector3(1.3f, 0.8f, 1f)));
        return root;
    }

    static StandardMaterial3D Flat(Color c) => new() { AlbedoColor = c, Roughness = 0.95f };

    /// <summary>One mesh drawn at many places (a MultiMesh), each offset, turned and scaled as given in its place.</summary>
    static MultiMeshInstance3D Many(Mesh mesh, Material mat, List<Transform3D> at, Vector3 offset, Vector3? tilt = null, Vector3? scale = null)
    {
        var local = new Transform3D(Basis.FromEuler(tilt ?? Vector3.Zero).Scaled(scale ?? Vector3.One), offset);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = at.Count };
        for (int i = 0; i < at.Count; i++) mm.SetInstanceTransform(i, at[i] * local);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat };
    }
}
