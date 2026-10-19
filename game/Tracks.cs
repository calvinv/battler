using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// Tread marks pressed into the dirt behind moving vehicles (the user, 2026-10-19: "can they also leave little track
/// marks"): short flat strips with a cleat pattern, laid every little way along each track and fading out over half
/// a minute. One MultiMesh of ground-hugging quads. Looks only.
/// </summary>
public partial class Tracks : MultiMeshInstance3D
{
    const int Max = 5000;
    const float Life = 30f;
    const float Spacing = 0.5f;   // world units between marks

    struct Mark { public Transform3D At; public float Age, Strength; }

    readonly Mark[] _marks = new Mark[Max];
    int _next, _count;
    readonly Dictionary<int, Vector3> _last = new();   // per vehicle: where it last left a mark
    public System.Func<float, float, float> HeightAt;

    public override void _Ready()
    {
        // A tread strip: dark cleats across it, soft at the sides.
        var img = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 16; x++)
            {
                float side = Mathf.Clamp(Mathf.Min(x, 15 - x) / 1.5f, 0, 1);
                float cleat = (y % 8) < 4 ? 1f : 0.45f;
                img.SetPixel(x, y, new Color(1, 1, 1, side * cleat));
            }
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = Max, VisibleInstanceCount = 0,
            Mesh = new PlaneMesh { Size = new Vector2(1, 1) },
        };
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img), VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
        };
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>A vehicle at `at` heading `forward`: marks under its two tracks, `width` wide, `halfWidth` either side.</summary>
    public void Drive(int id, Vector3 at, Vector3 forward, float halfWidth, float width, float strength)
    {
        if (!_last.TryGetValue(id, out var last)) { _last[id] = at; return; }
        var gone = new Vector3(at.X - last.X, 0, at.Z - last.Z);
        if (gone.Length() < Spacing) return;
        if (gone.Length() > 3f) { _last[id] = at; return; }   // (it was hidden or standing a long while: no long smear)
        _last[id] = at;
        var right = forward.Cross(Vector3.Up).Normalized();
        var mid = (at + last) * 0.5f;
        foreach (var side in new[] { -1f, 1f })
        {
            var p = mid + right * side * halfWidth;
            float y = HeightAt?.Invoke(p.X, p.Z) ?? p.Y;
            // Lie along the ground: tilt the strip to the slope along its length.
            var ahead = p + forward * 0.5f;
            var back = p - forward * 0.5f;
            float ya = HeightAt?.Invoke(ahead.X, ahead.Z) ?? y, yb = HeightAt?.Invoke(back.X, back.Z) ?? y;
            var along = new Vector3(forward.X, ya - yb, forward.Z).Normalized();
            var up = right.Cross(along).Normalized() * -1f;
            if (up.Y < 0) up = -up;
            var basis = new Basis(-right * width, up, along * (gone.Length() + 0.1f));   // (-right keeps it right-handed, face up)
            _marks[_next] = new Mark { At = new Transform3D(basis, new Vector3(p.X, y + 0.035f, p.Z)), Age = 0, Strength = strength };
            _next = (_next + 1) % Max;
            _count = Mathf.Min(_count + 1, Max);
        }
    }

    public void Forget(int id) => _last.Remove(id);

    public void Clear() { _count = 0; _next = 0; _last.Clear(); }

    public override void _Process(double delta) => Advance((float)delta);

    public void Advance(float dt)
    {
        if (Multimesh == null) return;
        var mm = Multimesh;
        int n = 0;
        for (int k = 0; k < _count; k++)
        {
            int i = ((_next - _count + k) % Max + Max) % Max;   // oldest first
            ref var m = ref _marks[i];
            m.Age += dt;
            if (m.Age >= Life) continue;
            float fade = 1f - m.Age / Life;
            mm.SetInstanceTransform(n, m.At);
            mm.SetInstanceColor(n, new Color(0.1f, 0.08f, 0.06f, 0.6f * m.Strength * fade * fade));
            n++;
        }
        mm.VisibleInstanceCount = n;
    }
}
