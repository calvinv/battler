using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>Health bars drawn over the 3D view, in screen space so they stay crisp at any zoom. Main fills
/// <see cref="Bars"/> every frame and calls QueueRedraw.</summary>
public partial class Overlay : Control
{
    public readonly List<(Vector2 Centre, float Width, float Fraction, Color Team, bool Selected)> Bars = new();
    /// <summary>Upgrade and veterancy marks over a unit or factory: a sword per damage level, a shield per armour
    /// level (shown as pips under each), gold chevrons per veterancy rank.</summary>
    public readonly List<(Vector2 Centre, int Sword, int Shield, int Rank)> Badges = new();
    /// <summary>Short text tags over things: a base's factory slots in use ("2/3").</summary>
    public readonly List<(Vector2 Centre, string Text, Color Colour)> Tags = new();
    /// <summary>Floating numbers (salvage): where, the text, and how strong (fading 1 to 0).</summary>
    public readonly List<(Vector2 Centre, string Text, float Strength)> Floats = new();
    /// <summary>Radar blips: where a hidden enemy unit was at the last sweep, and how strong (fading 1 to 0).</summary>
    public readonly List<(Vector2 Centre, float Strength)> Blips = new();
    /// <summary>The box being dragged to pick scouts, if any.</summary>
    public Rect2? Box;

    static readonly Color Steel = new(0.85f, 0.88f, 0.92f), Gold = new(1f, 0.8f, 0.25f), Ink = new(0f, 0f, 0f, 0.9f);

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
        foreach (var (c, sword, shield, rank) in Badges)
        {
            DrawSetTransform(c, 0, Vector2.One * 1.4f);   // icons drawn about their own centre, 1.4 times size
            DrawBadges(Vector2.Zero, sword, shield, rank);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var font = ThemeDB.FallbackFont;
        foreach (var (c, text, colour) in Tags)
        {
            // A dark pill with the text in it, so it reads over any ground.
            const int Size = 17;
            var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, Size).X;
            var pill = new Rect2(c.X - width / 2 - 7, c.Y - 12, width + 14, 23);
            DrawRect(pill, new Color(0.04f, 0.05f, 0.07f, 0.8f));
            DrawRect(pill, colour, false, 1.5f);
            DrawString(font, new Vector2(c.X - width / 2, c.Y + 6), text, HorizontalAlignment.Left, -1, Size, colour);
        }
        // Radar blips: a red dot in a ring, fading as the sweep gets older.
        foreach (var (c, strength) in Blips)
        {
            var red = new Color(1f, 0.25f, 0.2f, 0.9f * strength);
            DrawCircle(c, 4.5f, red);
            DrawArc(c, 9f + 6f * (1 - strength), 0, Mathf.Tau, 20, red with { A = 0.6f * strength }, 1.5f);
        }
        foreach (var (c, text, strength) in Floats)
        {
            const int Size = 24;
            var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, Size).X;
            var at = new Vector2(c.X - width / 2, c.Y);
            DrawString(font, at + new Vector2(1.5f, 1.5f), text, HorizontalAlignment.Left, -1, Size, new Color(0, 0, 0, 0.8f * strength));
            DrawString(font, at, text, HorizontalAlignment.Left, -1, Size, new Color(1f, 0.85f, 0.3f, strength));
        }
        if (Box is { } box)
        {
            DrawRect(box, new Color(1, 1, 1, 0.08f));
            DrawRect(box, new Color(1, 1, 1, 0.8f), false, 1.5f);
        }
    }

    void DrawBadges(Vector2 c, int sword, int shield, int rank)
    {
        const float Cell = 15f;
        int n = (sword > 0 ? 1 : 0) + (shield > 0 ? 1 : 0) + (rank > 0 ? 1 : 0);
        float x = c.X - (n - 1) * Cell / 2;
        if (sword > 0) { Sword(new Vector2(x, c.Y)); Pips(new Vector2(x, c.Y + 9), sword, Steel); x += Cell; }
        if (shield > 0) { Shield(new Vector2(x, c.Y)); Pips(new Vector2(x, c.Y + 9), shield, Steel); x += Cell; }
        if (rank > 0) Chevrons(new Vector2(x, c.Y), rank);
    }

    void Sword(Vector2 c)
    {
        // A blade on the diagonal, a crossguard across it, a pommel; inked under.
        var tip = c + new Vector2(5, -6);
        var hilt = c + new Vector2(-3, 2);
        DrawLine(hilt + new Vector2(-1, 1), tip, Ink, 4f);
        DrawLine(hilt, tip, Steel, 2f);
        DrawLine(c + new Vector2(-5, -1), c + new Vector2(0, 4), Ink, 3.5f);
        DrawLine(c + new Vector2(-5, -1), c + new Vector2(0, 4), Gold, 2f);
        DrawCircle(c + new Vector2(-5, 5), 1.6f, Gold);
    }

    void Shield(Vector2 c)
    {
        var pts = new[]
        {
            c + new Vector2(-5, -6), c + new Vector2(5, -6), c + new Vector2(5, 0), c + new Vector2(0, 6), c + new Vector2(-5, 0),
        };
        var ink = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++) ink[i] = c + (pts[i] - c) * 1.3f;
        DrawColoredPolygon(ink, Ink);
        DrawColoredPolygon(pts, Steel);
        DrawLine(c + new Vector2(0, -5), c + new Vector2(0, 4), new Color(0.45f, 0.5f, 0.6f), 1.5f);
    }

    void Chevrons(Vector2 c, int rank)
    {
        for (int i = 0; i < rank; i++)
        {
            float y = c.Y + 3 - i * 4;
            var pts = new[] { new Vector2(c.X - 5, y - 3), new Vector2(c.X, y), new Vector2(c.X + 5, y - 3) };
            DrawPolyline(pts, Ink, 4f);
            DrawPolyline(pts, Gold, 2f);
        }
    }

    void Pips(Vector2 c, int count, Color colour)
    {
        for (int i = 0; i < count; i++)
        {
            var p = new Vector2(c.X + (i - (count - 1) / 2f) * 4f, c.Y);
            DrawCircle(p, 1.8f, Ink);
            DrawCircle(p, 1.2f, colour);
        }
    }
}
