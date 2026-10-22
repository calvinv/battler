using System.Collections.Generic;
using Godot;
using Battler.Sim;

namespace Battler.Game;

/// <summary>
/// Shots you can watch fly (the user, 2026-10-19: "make the units face what they're shooting and add projectiles").
/// Looks only: the sim lands the damage the tick a shot is fired; these just play it out. Rounds are glowing slugs
/// stretched along their path (one MultiMesh); flashes are soft additive billboards for muzzles and hits (another).
/// Scouts rattle off short bursts, tanks and guns fire one fast shell, artillery lobs a slow smoking shell that bursts.
/// </summary>
public partial class Projectiles : Node3D
{
    const int MaxRounds = 600, MaxFlashes = 400;

    struct Round
    {
        public Vector3 From, To;
        public float Delay, Age, Flight, Arc, Thick, Length, SmokeDue;
        public Color Col;
        public Kind By;
        public bool Burst, Sparks;
    }

    struct Flash { public Vector3 At; public float Age, Life, Size; public Color Col; }

    readonly List<Round> _rounds = new();
    readonly List<Flash> _flashes = new();
    MultiMesh _roundMesh, _flashMesh;
    readonly RandomNumberGenerator _rng = new() { Seed = 7 };

    /// <summary>Somewhere to puff smoke and dirt (the vehicles' dust).</summary>
    public Dust Dust;
    /// <summary>Called when an artillery shell lands, for the big blast.</summary>
    public System.Action<Vector3, Kind> OnShellLands;

    public override void _Ready()
    {
        _roundMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = MaxRounds, VisibleInstanceCount = 0,
            Mesh = new CapsuleMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 1 },
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _roundMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        });

        var soft = new GradientTexture2D
        {
            Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 0.3f, 1f } },
        };
        _flashMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = MaxFlashes, VisibleInstanceCount = 0,
            Mesh = new QuadMesh { Size = Vector2.One },
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _flashMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = soft, VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false,
            },
        });
    }

    /// <summary>A shot from a muzzle at `from` to `to`, fired by a `by`, in team colour `team`.</summary>
    public void Fire(Vector3 from, Vector3 to, Kind by, Color team, float flight = 0f)
    {
        float dist = from.DistanceTo(to);
        var hot = new Color(1f, 0.85f, 0.45f);
        switch (by)
        {
            case Kind.Scout or Kind.Ranger:   // a short burst of light tracers
                for (int i = 0; i < 3; i++)
                {
                    var miss = new Vector3(_rng.RandfRange(-0.35f, 0.35f), _rng.RandfRange(-0.1f, 0.3f), _rng.RandfRange(-0.35f, 0.35f));
                    Add(new Round { From = from, To = to + miss, Delay = i * 0.07f, Flight = dist / 70f, Thick = 0.07f, Length = 1.4f, Col = hot.Lerp(team, 0.2f), By = by, Sparks = true });
                    AddFlash(from, 0.6f, 0.06f, hot, i * 0.07f);
                }
                break;
            case Kind.Artillery or Kind.Bombard:   // a slow lobbed shell that trails smoke and bursts
                Add(new Round { From = from, To = to, Flight = flight > 0 ? flight : 0.55f + dist / 30f, Arc = dist * 0.4f, Thick = 0.3f, Length = 0.8f, Col = new Color(0.25f, 0.22f, 0.18f), By = by, Burst = true });
                AddFlash(from, 2.6f, 0.16f, new Color(1f, 0.6f, 0.25f));
                for (int i = 0; i < 3; i++) Dust?.Add(from, new Vector3(_rng.RandfRange(-1, 1), _rng.RandfRange(0.6f, 1.4f), _rng.RandfRange(-1, 1)), 0.9f, Smoke);
                break;
            case Kind.Mortar or Kind.Bomber:   // a small shell lobbed high and short, a lighter burst
                Add(new Round { From = from, To = to, Flight = flight > 0 ? flight : 0.4f + dist / 30f, Arc = dist * 0.55f, Thick = 0.2f, Length = 0.5f, Col = new Color(0.25f, 0.22f, 0.18f), By = by, Burst = true });
                AddFlash(from, 1.5f, 0.12f, new Color(1f, 0.6f, 0.25f));
                Dust?.Add(from, new Vector3(0, 1f, 0), 0.6f, Smoke);
                break;
            case Kind.LightTank or Kind.Flier:   // a quick light round
                Add(new Round { From = from, To = to, Flight = dist / 55f, Thick = 0.1f, Length = 1.6f, Col = hot.Lerp(team, 0.15f), By = by, Sparks = true });
                AddFlash(from, 0.9f, 0.08f, new Color(1f, 0.75f, 0.35f));
                break;
            case Kind.Spider:   // laser bolts: long, thin, very fast, bright, from alternate barrels
            {
                var laser = new Color(0.45f, 1f, 1f).Lerp(team, 0.35f) * 1.8f;   // over 1: it blooms
                var side = (to - from).Cross(Vector3.Up).Normalized() * (_rng.Randf() < 0.5f ? 0.08f : -0.08f);
                Add(new Round { From = from + side, To = to, Flight = dist / 120f, Thick = 0.06f, Length = 2.6f, Col = laser, By = by, Sparks = true });
                AddFlash(from + side, 0.5f, 0.05f, new Color(0.5f, 1f, 1f));
                break;
            }
            case Kind.Crawler or Kind.Feral or Kind.FeralBrute:   // a quick spit of amber from its claw-gun
                Add(new Round { From = from, To = to, Flight = dist / 60f, Thick = 0.08f, Length = 1.0f, Col = new Color(1f, 0.6f, 0.15f) * 1.5f, By = by, Sparks = true });
                AddFlash(from, 0.5f, 0.05f, new Color(1f, 0.6f, 0.2f));
                break;
            case Kind.Piercer:   // a heavy glowing spike: thick, very fast, a big flash
                Add(new Round { From = from, To = to, Flight = dist / 90f, Thick = 0.2f, Length = 3.2f, Col = new Color(1f, 0.55f, 0.1f) * 2f, By = by, Sparks = true });
                AddFlash(from, 1.8f, 0.12f, new Color(1f, 0.6f, 0.2f));
                Dust?.Add(from, new Vector3(0, 0.6f, 0), 0.5f, Smoke);
                break;
            default:   // tanks, towers, outposts, the HQ: one fast shell
                float thick = by == Kind.Outpost ? 0.1f : 0.16f;
                Add(new Round { From = from, To = to, Flight = dist / 45f, Thick = thick, Length = 2.2f, Col = hot.Lerp(team, 0.15f), By = by, Sparks = true });
                AddFlash(from, by == Kind.Outpost ? 0.9f : 1.6f, 0.1f, new Color(1f, 0.75f, 0.35f));
                if (by == Kind.Tank) Dust?.Add(from, new Vector3(0, 0.6f, 0), 0.6f, Smoke);
                break;
        }
    }

    static readonly Color Smoke = new(0.42f, 0.4f, 0.38f);

    void Add(Round r) { if (_rounds.Count < MaxRounds) _rounds.Add(r); }

    void AddFlash(Vector3 at, float size, float life, Color col, float delay = 0f)
    {
        if (_flashes.Count < MaxFlashes) _flashes.Add(new Flash { At = at, Size = size, Life = life, Col = col, Age = -delay });
    }

    public void Clear() { _rounds.Clear(); _flashes.Clear(); Redraw(); }

    public override void _Process(double delta) => Advance((float)delta);

    /// <summary>Moves everything on by `dt` seconds (also called while fast-forwarding, when frames don't run).</summary>
    public void Advance(float dt)
    {
        for (int i = _rounds.Count - 1; i >= 0; i--)
        {
            var r = _rounds[i];
            if (r.Delay > 0) { r.Delay -= dt; _rounds[i] = r; continue; }
            r.Age += dt;
            if (r.Age >= r.Flight) { Land(r); _rounds[i] = _rounds[^1]; _rounds.RemoveAt(_rounds.Count - 1); continue; }
            if (r.Burst && (r.SmokeDue -= dt) <= 0)   // a thin smoke trail behind the shell
            {
                r.SmokeDue = 0.05f;
                Dust?.Add(At(r, r.Age / r.Flight), Vector3.Up * 0.2f, 0.35f, Smoke);
            }
            _rounds[i] = r;
        }
        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.Age += dt;
            if (f.Age >= f.Life) { _flashes[i] = _flashes[^1]; _flashes.RemoveAt(_flashes.Count - 1); continue; }
            _flashes[i] = f;
        }
        Redraw();
    }

    static Vector3 At(in Round r, float t) => r.From.Lerp(r.To, t) + Vector3.Up * r.Arc * 4 * t * (1 - t);

    void Land(in Round r)
    {
        if (r.Burst)
        {
            OnShellLands?.Invoke(r.To, r.By);
            float big = r.By == Kind.Mortar ? 0.6f : 1f;   // a mortar bomb bursts smaller
            AddFlash(r.To + Vector3.Up * big, 6f * big, 0.25f, new Color(1f, 0.55f, 0.2f));
            for (int i = 0; i < (r.By == Kind.Mortar ? 4 : 8); i++)   // dirt thrown up
                Dust?.Add(r.To, new Vector3(_rng.RandfRange(-2.5f, 2.5f), _rng.RandfRange(2f, 4.5f), _rng.RandfRange(-2.5f, 2.5f)), _rng.RandfRange(0.9f, 1.6f), new Color(0.38f, 0.32f, 0.25f));
        }
        else if (r.Sparks)
        {
            bool zap = r.By == Kind.Spider;   // a laser leaves a small cyan spark and no smoke
            AddFlash(r.To, r.By == Kind.Scout || zap ? 0.5f : 1.2f, 0.09f, zap ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.8f, 0.4f));
            if (r.By != Kind.Scout && !zap) Dust?.Add(r.To, Vector3.Up * 0.8f, 0.5f, Smoke);
        }
    }

    void Redraw()
    {
        if (_roundMesh == null) return;   // not ready yet
        RedrawNow();
    }

    void RedrawNow()
    {
        int n = 0;
        foreach (var r in _rounds)
        {
            if (r.Delay > 0) continue;
            float t = r.Age / r.Flight;
            var p = At(r, t);
            var dir = At(r, Mathf.Min(t + 0.02f, 1f)) - p;
            if (dir.LengthSquared() < 1e-8f) dir = r.To - r.From;
            dir = dir.Normalized();
            var up = Mathf.Abs(dir.Y) > 0.95f ? Vector3.Right : Vector3.Up;
            // The capsule's long axis is Y: turn Y onto the path, then stretch it.
            var basis = Basis.LookingAt(dir, up) * new Basis(Vector3.Right, Mathf.Pi / 2);
            var scaled = new Basis(basis.X * r.Thick, basis.Y * r.Length, basis.Z * r.Thick);
            _roundMesh.SetInstanceTransform(n, new Transform3D(scaled, p));
            _roundMesh.SetInstanceColor(n, r.Col);
            n++;
        }
        _roundMesh.VisibleInstanceCount = n;

        n = 0;
        foreach (var f in _flashes)
        {
            if (f.Age < 0) continue;
            float t = f.Age / f.Life;
            _flashMesh.SetInstanceTransform(n, new Transform3D(Basis.Identity.Scaled(Vector3.One * f.Size * (0.7f + 0.6f * t)), f.At));
            _flashMesh.SetInstanceColor(n, new Color(f.Col.R, f.Col.G, f.Col.B, 1f - t));
            n++;
        }
        _flashMesh.VisibleInstanceCount = n;
    }
}
