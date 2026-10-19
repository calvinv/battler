using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// Dirt kicked up behind moving vehicles (the user, 2026-10-19: "particle effects like dirt when tanks drive"): soft
/// dusty puffs that rise, swell and fade, all drawn as one MultiMesh of camera-facing quads. Looks only.
/// </summary>
public partial class Dust : MultiMeshInstance3D
{
    const int Max = 1500;
    const float Life = 1.1f;

    struct Puff { public Vector3 At, Drift; public float Age, Size; public Color Col; }
    static readonly Color Dirt = new(0.55f, 0.48f, 0.38f);

    readonly List<Puff> _puffs = new();
    readonly RandomNumberGenerator _rng = new() { Seed = 99 };
    readonly Dictionary<int, float> _due = new();   // per vehicle: seconds until its next puff

    public override void _Ready()
    {
        var soft = new GradientTexture2D
        {
            Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 1f } },
        };
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = Max, VisibleInstanceCount = 0,
            Mesh = new QuadMesh { Size = Vector2.One },
        };
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoTexture = soft, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardKeepScale = true,
        };
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>A vehicle moving at `speed` (world units a second) at `at`, facing `forward`: puffs from its rear corners.</summary>
    public void Drive(int id, Vector3 at, Vector3 forward, float halfLength, float halfWidth, float speed, float dt)
    {
        if (speed < 0.3f) return;
        _due.TryGetValue(id, out var due);
        due -= dt;
        if (due <= 0)
        {
            var right = forward.Cross(Vector3.Up).Normalized();
            var back = at - forward * halfLength;
            foreach (var side in new[] { -1f, 1f })
            {
                if (_puffs.Count >= Max) break;
                _puffs.Add(new Puff
                {
                    At = back + right * side * halfWidth + Vector3.Up * 0.15f,
                    Drift = -forward * 0.4f + new Vector3(_rng.RandfRange(-0.3f, 0.3f), _rng.RandfRange(0.4f, 0.8f), _rng.RandfRange(-0.3f, 0.3f)),
                    Size = _rng.RandfRange(0.35f, 0.6f) * (halfWidth + 0.4f), Col = Dirt,
                });
            }
            due += Mathf.Clamp(0.5f / speed, 0.06f, 0.3f);   // quicker vehicles kick up more
        }
        _due[id] = due;
    }

    /// <summary>One puff of smoke or dirt (gun smoke, shell bursts).</summary>
    public void Add(Vector3 at, Vector3 drift, float size, Color col)
    {
        if (_puffs.Count < Max) _puffs.Add(new Puff { At = at, Drift = drift, Size = size, Col = col });
    }

    public void Clear() { _puffs.Clear(); _due.Clear(); }

    public void Forget(int id) => _due.Remove(id);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            var p = _puffs[i];
            p.Age += dt;
            if (p.Age >= Life) { _puffs[i] = _puffs[^1]; _puffs.RemoveAt(_puffs.Count - 1); continue; }
            p.At += p.Drift * dt;
            p.Drift *= 1f - 1.5f * dt;   // slows as it settles
            _puffs[i] = p;
        }
        var mm = Multimesh;
        mm.VisibleInstanceCount = _puffs.Count;
        for (int i = 0; i < _puffs.Count; i++)
        {
            var p = _puffs[i];
            float t = p.Age / Life;
            float size = p.Size * (1f + 2.2f * t);
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), p.At));
            mm.SetInstanceColor(i, new Color(p.Col.R, p.Col.G, p.Col.B, 0.55f * (1f - t) * Mathf.Min(1f, t * 8f)));
        }
    }
}
