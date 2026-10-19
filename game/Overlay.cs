using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>Health bars drawn over the 3D view, in screen space so they stay crisp at any zoom. Main fills
/// <see cref="Bars"/> every frame and calls QueueRedraw.</summary>
public partial class Overlay : Control
{
    public readonly List<(Vector2 Centre, float Width, float Fraction, Color Team, bool Selected)> Bars = new();

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;   // clicks go through to the game

    public override void _Draw()
    {
        foreach (var (c, width, frac, team, selected) in Bars)
        {
            float w = Mathf.Max(width, 14f), h = Mathf.Clamp(w * 0.12f, 3f, 6f);
            var r = new Rect2(c.X - w / 2, c.Y - h / 2, w, h);
            DrawRect(r.Grow(1), selected ? Colors.White : new Color(0, 0, 0, 0.85f));
            DrawRect(r, new Color(0.15f, 0.12f, 0.1f));
            // Green when healthy, through yellow, to red near death; the team colour as a thin cap on the left.
            var fill = frac > 0.5f ? new Color(0.35f, 0.85f, 0.3f) : frac > 0.25f ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
            DrawRect(new Rect2(r.Position, new Vector2(w * frac, h)), fill);
            DrawRect(new Rect2(r.Position, new Vector2(Mathf.Min(3f, w), h)), team);
        }
    }
}
