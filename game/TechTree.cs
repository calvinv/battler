using System.Collections.Generic;
using System.Linq;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The tech tree (the user, 2026-10-24: "lets have the production things be like a tech tree in a submenu"), opened
/// from the HQ's card or with Tab: a column per branch (Tech.cs), each research a tile with its picture, cost and
/// time, lines down from what it needs. Click one to queue it (what it needs can be queued ahead of it); click one
/// queued or under way to drop it. Researched tiles are lit green; the one under way fills as it goes. The play goes
/// on underneath.
/// </summary>
public partial class Main
{
    const int TreeLane = 150, TreeRow = 96, TreeTileW = 138, TreeTileH = 78, TreeTop = 56;
    Control _techPanel, _techCanvas;
    Label _techStatus;
    bool _techOpen;
    readonly Dictionary<Tech, (Button Root, TextureRect Icon, Label Name, Label Info, ColorRect Fill)> _techTiles = new();

    static readonly Branch[] TreeOrder = { Branch.Armour, Branch.Walkers, Branch.Guns, Branch.Air, Branch.Support, Branch.Core };
    static readonly Dictionary<Branch, string> BranchName = new()
    {
        [Branch.Armour] = "Armour", [Branch.Walkers] = "Walkers", [Branch.Guns] = "Guns",
        [Branch.Air] = "Air", [Branch.Support] = "Support", [Branch.Core] = "Core",
    };

    /// <summary>Lanes before each branch's first column.</summary>
    static int LaneOf(Branch b)
    {
        int lane = 0;
        foreach (var o in TreeOrder)
        {
            if (o == b) return lane;
            lane += Techs.All.Where(t => t.Branch == o).Max(t => t.Lane) + 1;
        }
        return lane;
    }

    static int TreeLanes => LaneOf(Branch.Core) + 2;

    /// <summary>A research's picture: the unit it unlocks, else its branch's add-on.</summary>
    static string TechModel(TechInfo t) => t.Unlocks is { } k ? UnitModel(k, Civ.Coalition) : "addon_" + t.Branch.ToString().ToLowerInvariant();

    static Vector2 TileAt(TechInfo t) => new(16 + (LaneOf(t.Branch) + t.Lane) * TreeLane, TreeTop + t.Row * TreeRow);

    void BuildTechTree(CanvasLayer ui)
    {
        var width = 32 + TreeLanes * TreeLane - (TreeLane - TreeTileW);
        var height = TreeTop + 3 * TreeRow + 40;
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.08f, 0.94f), BorderColor = new Color(0.85f, 0.65f, 0.3f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        _techCanvas = new Control { CustomMinimumSize = new Vector2(width, height) };
        _techCanvas.Draw += DrawTreeLines;
        panel.AddChild(_techCanvas);
        centre.AddChild(panel);
        ui.AddChild(centre);
        _techPanel = centre;

        var title = new Label { Text = "TECH TREE   (Tab or Esc to close)", Position = new Vector2(16, 6) };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.Modulate = new Color(1f, 0.9f, 0.7f);
        _techCanvas.AddChild(title);
        foreach (var b in TreeOrder)
        {
            var head = new Label { Text = BranchName[b].ToUpperInvariant(), Position = new Vector2(16 + LaneOf(b) * TreeLane, 32) };
            head.AddThemeFontSizeOverride("font_size", 14);
            head.Modulate = new Color(0.75f, 0.8f, 0.85f);
            _techCanvas.AddChild(head);
        }
        _techStatus = new Label { Position = new Vector2(16, height - 30) };
        _techStatus.AddThemeFontSizeOverride("font_size", 15);
        _techCanvas.AddChild(_techStatus);

        foreach (var t in Techs.All)
        {
            var info = t;
            var b = new Button { Position = TileAt(t), Size = new Vector2(TreeTileW, TreeTileH), FocusMode = Control.FocusModeEnum.None };
            var icon = new TextureRect
            {
                Position = new Vector2(2, 4), Size = new Vector2(46, 46), MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            var name = new Label { Position = new Vector2(50, 4), Size = new Vector2(TreeTileW - 54, 44), AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 14);
            var cost = new Label { Position = new Vector2(6, TreeTileH - 26), MouseFilter = Control.MouseFilterEnum.Ignore };
            cost.AddThemeFontSizeOverride("font_size", 13);
            var fill = new ColorRect { Position = new Vector2(3, TreeTileH - 6), Size = new Vector2(0, 3), Color = new Color(0.35f, 0.75f, 1f), MouseFilter = Control.MouseFilterEnum.Ignore };
            b.AddChild(icon); b.AddChild(name); b.AddChild(cost); b.AddChild(fill);
            b.Pressed += () => PressTech(info.Id);
            _techCanvas.AddChild(b);
            _techTiles[t.Id] = (b, icon, name, cost, fill);
        }
    }

    void SetTechTree(bool open)
    {
        if (_session == null) return;
        _techOpen = open && W.Winner < 0;
        _techPanel.Visible = _techOpen;
    }

    /// <summary>Keys while the tree is up: Tab and Esc close it; it lets the rest through.</summary>
    bool TechTreeKey(Key key)
    {
        if (key is Key.Tab or Key.Escape) { SetTechTree(false); return true; }
        return false;
    }

    void PressTech(Tech t)
    {
        var p = W.Players[_me];
        int at = p.Research.IndexOf((int)t);
        if (at >= 0) _session.Issue(new CancelResearch(_me, at));
        else if (W.CanResearch(_me, t, out _)) _session.Issue(new Research(_me, t));
        else if (Techs.Of(t).Needs is { } need && !p.Has[(int)need] && W.CanResearch(_me, need, out _))
        {
            // What it needs isn't queued yet: queue that first, then this.
            _session.Issue(new Research(_me, need));
            _session.Issue(new Research(_me, t));
        }
    }

    void UpdateTechTree()
    {
        if (!_techOpen) return;
        var p = W.Players[_me];
        var team = TeamColor[_me];
        foreach (var t in Techs.All)
        {
            var (root, icon, name, info, fill) = _techTiles[t.Id];
            bool has = p.Has[(int)t.Id];
            int queued = p.Research.IndexOf((int)t.Id);
            bool going = queued == 0 && p.ResearchLeft > 0;
            bool open = !has && queued < 0 && (t.Needs is not { } need || p.Has[(int)need] || p.Research.Contains((int)need));
            icon.Texture ??= Icons.For(this, TechModel(t), team);
            name.Text = t.Name;
            info.Text = has ? "done" : going ? $"{Mathf.CeilToInt(p.ResearchLeft / (float)World.TicksPerSecond)} s left" : queued > 0 ? $"queued #{queued + 1}" : $"{t.Cost}   {t.Seconds} s";
            info.AddThemeColorOverride("font_color", has ? new Color(0.5f, 1f, 0.55f) : queued >= 0 ? new Color(0.5f, 0.85f, 1f) : p.Metal >= t.Cost ? Colors.White : new Color(1f, 0.55f, 0.45f));
            fill.Size = new Vector2((TreeTileW - 6) * (has ? 1 : going ? 1f - p.ResearchLeft / (float)(t.Seconds * World.TicksPerSecond) : 0), 3);
            fill.Color = has ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.35f, 0.75f, 1f);
            root.Modulate = has || queued >= 0 || open ? Colors.White : new Color(1, 1, 1, 0.45f);
            var does = t.Does + (t.Unlocks is { } k ? "\n\n" + UnitStats(k).Replace("[color=#a0a0a0]", "").Replace("[/color]", "") : "");
            root.TooltipText = $"{t.Name}  ({BranchName[t.Branch]})\n{t.Cost} metal (paid as it goes), {t.Seconds} s\n{does}"
                + (t.Needs is { } n2 && !p.Has[(int)n2] ? $"\nNeeds {Techs.Of(n2).Name} (click queues both)" : "")
                + (queued >= 0 ? "\nClick to drop it" : "");
        }
        string Line(int i) => Techs.Of((Tech)p.Research[i]).Name;
        _techStatus.Text = p.Research.Count == 0 ? $"Nothing being researched.   Metal {p.Metal}"
            : $"Researching {Line(0)}" + (p.ResearchLeft > 0 ? $" ({Mathf.CeilToInt(p.ResearchLeft / (float)World.TicksPerSecond)} s)" : " (waiting for metal)")
              + (p.Research.Count > 1 ? "   then " + string.Join(", ", Enumerable.Range(1, p.Research.Count - 1).Select(Line)) : "") + $"   Metal {p.Metal}";
        _techCanvas.QueueRedraw();
    }

    /// <summary>A line down from each research to what follows it: lit once the first is done.</summary>
    void DrawTreeLines()
    {
        if (_session == null) return;
        var p = W.Players[_me];
        foreach (var t in Techs.All)
        {
            if (t.Needs is not { } need) continue;
            var from = TileAt(Techs.Of(need)) + new Vector2(TreeTileW / 2f, TreeTileH);
            var to = TileAt(t) + new Vector2(TreeTileW / 2f, 0);
            var colour = p.Has[(int)need] ? new Color(0.5f, 0.9f, 0.55f) : new Color(0.45f, 0.45f, 0.45f);
            if (Mathf.IsEqualApprox(from.X, to.X)) _techCanvas.DrawLine(from, to, colour, 3);
            else
            {
                float midY = (from.Y + to.Y) / 2;
                _techCanvas.DrawPolyline(new[] { from, new Vector2(from.X, midY), new Vector2(to.X, midY), to }, colour, 3);
            }
        }
    }
}
