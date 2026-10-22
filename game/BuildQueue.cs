using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The build queue along the bottom of the screen (the user, 2026-10-22: "we should have a build queue with icons of
/// things queued along the bottom"): one tile per thing of mine on the way, with its picture, a bar of how far along
/// it is, and a line under it.
///   - buildings going up (seconds left)
///   - scouts and mobile bases ordered at a utility wing
///   - upgrades waiting at a building for the metal (what, and the cost)
///   - what the factories are making, one tile per kind (how many factories; "army full" at the cap)
/// Clicking a tile picks that building and looks at it.
/// </summary>
public partial class Main
{
    const int TileSize = 76;
    HBoxContainer _queueBar;
    readonly List<QueueTile> _tiles = new();

    sealed class QueueTile
    {
        public Button Root;
        public TextureRect Icon;
        public Label Count, Note;
        public ColorRect Fill;
        public int BuildingId;
    }

    /// <summary>One thing on the way: its model, how far along (0..1), a count over it (blank for one), a note under
    /// it, the building it's at, and a tint for the bar.</summary>
    readonly record struct Queued(string Model, float Progress, string Count, string Note, int BuildingId, Color Bar, string Tip);

    static readonly Color BarBuild = new(0.35f, 0.75f, 1f), BarMake = new(0.45f, 0.85f, 0.4f),
        BarWait = new(0.95f, 0.75f, 0.25f), BarStuck = new(0.6f, 0.6f, 0.6f);
    static readonly string[] UpgradeShort = { "Speed", "Damage", "Armour" }, OutpostUpgradeShort = { "Reach", "Damage", "Armour" };

    void BuildQueueBar(CanvasLayer ui)
    {
        var anchor = new CenterContainer
        {
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetTop = -TileSize - 34, OffsetBottom = -10,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _queueBar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _queueBar.AddThemeConstantOverride("separation", 6);
        anchor.AddChild(_queueBar);
        ui.AddChild(anchor);
    }

    QueueTile MakeTile()
    {
        var t = new QueueTile();
        t.Root = new Button { CustomMinimumSize = new Vector2(TileSize, TileSize + 20), FocusMode = Control.FocusModeEnum.None };
        t.Root.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.07f, 0.85f), BorderColor = new Color(0.5f, 0.45f, 0.35f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        });
        t.Root.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.14f, 0.12f, 0.9f), BorderColor = new Color(0.85f, 0.78f, 0.6f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        });
        t.Icon = new TextureRect
        {
            Position = new Vector2(4, 2), Size = new Vector2(TileSize - 8, TileSize - 8),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        t.Root.AddChild(t.Icon);
        t.Count = new Label { Position = new Vector2(6, 2), MouseFilter = Control.MouseFilterEnum.Ignore };
        t.Count.AddThemeFontSizeOverride("font_size", 16);
        t.Count.AddThemeColorOverride("font_outline_color", Colors.Black);
        t.Count.AddThemeConstantOverride("outline_size", 4);
        t.Root.AddChild(t.Count);
        t.Root.AddChild(new ColorRect { Position = new Vector2(4, TileSize - 6), Size = new Vector2(TileSize - 8, 5), Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore });
        t.Fill = new ColorRect { Position = new Vector2(4, TileSize - 6), Size = new Vector2(0, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
        t.Root.AddChild(t.Fill);
        t.Note = new Label
        {
            Position = new Vector2(0, TileSize - 1), Size = new Vector2(TileSize, 20), HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        t.Note.AddThemeFontSizeOverride("font_size", 13);
        t.Root.AddChild(t.Note);
        t.Root.Pressed += () =>
        {
            if (W.Find(t.BuildingId) is not { } b) return;
            _selected = b.Id;
            _focus = ToV3(b.Pos);
        };
        _queueBar.AddChild(t.Root);
        return t;
    }

    /// <summary>Everything of mine on the way, in order: buildings going up, scouts, waiting upgrades, then factories.</summary>
    List<Queued> WhatsQueued()
    {
        var list = new List<Queued>();
        var civ = MyCiv;
        int tps = World.TicksPerSecond;
        bool full = W.ArmyCount(_me) >= World.MaxArmyOf(civ);
        var making = new SortedDictionary<Kind, (int Factories, float Best, int Id)>();
        foreach (var e in W.Entities)
        {
            if (e.Dead || e.Owner != _me) continue;
            if (e.Building > 0)
            {
                int total = World.ConstructionTicks(e.Kind);
                list.Add(new Queued(BuildingModel(e.Kind, e.Makes, civ), 1f - e.Building / (float)total, "",
                    $"{Mathf.CeilToInt(e.Building / (float)tps)} s", e.Id, BarBuild, "Going up: " + Describe(e.Kind, e.Makes)));
                continue;
            }
            if (World.IsUtility(e))   // what's ordered: the one being built, then the rest waiting
                for (int i = 0; i < e.Orders.Count; i++)
                {
                    var what = (Kind)e.Orders[i];
                    int ticks = World.StatsFor[(int)what].BuildTicks;
                    list.Add(new Queued(UnitModel(what, civ), i == 0 ? 1f - e.Timer / (float)ticks : 0, "",
                        i == 0 ? $"{Mathf.CeilToInt(e.Timer / (float)tps)} s" : "waiting", e.Id, BarMake, NameOf(what) + " at a utility wing"));
                }
            for (int i = 0; i < e.UpgradeQueue.Count; i++)
            {
                var which = (Upgrade)e.UpgradeQueue[i];
                int level = e.Level(which) + 1;
                for (int j = 0; j < i; j++) if (e.UpgradeQueue[j] == (int)which) level++;
                int cost = World.UpgradeCost(level);
                float ready = i == 0 ? Mathf.Clamp(W.Players[_me].Metal / (float)cost, 0, 1) : 0;
                var names = e.Kind == Kind.Outpost ? OutpostUpgradeShort : UpgradeShort;
                list.Add(new Queued(BuildingModel(e.Kind, e.Makes, civ), ready, "+" + names[(int)which][0] + level,
                    $"{names[(int)which]} {cost}", e.Id, BarWait, $"{names[(int)which]} {level} queued, waiting for {cost} metal"));
            }
            if (e.Kind == Kind.Factory && !World.IsUtility(e))
            {
                float done = full ? 0 : 1f - e.Timer / (float)World.BuildTicksOf(e);
                making.TryGetValue(e.Makes, out var m);
                making[e.Makes] = (m.Factories + 1, done >= m.Best || m.Factories == 0 ? done : m.Best, done >= m.Best || m.Factories == 0 ? e.Id : m.Id);
            }
        }
        foreach (var (kind, m) in making)
        {
            var tick = (1 - m.Best) * World.StatsFor[(int)kind].BuildTicks;
            list.Add(new Queued(ModelOf(kind), m.Best, m.Factories > 1 ? "x" + m.Factories : "",
                full ? "army full" : $"{Mathf.CeilToInt(tick / tps)} s", m.Id, full ? BarStuck : BarMake,
                $"{NameOf(kind)}: {m.Factories} factor{(m.Factories > 1 ? "ies" : "y")} making them"));
        }
        return list;
    }

    string Describe(Kind kind, Kind makes) => kind == Kind.Factory ? NameOf(makes) + " factory" : NameOf(kind);

    void UpdateQueueBar()
    {
        var items = W.Started || W.Entities.Exists(e => e.Owner == _me && e.Building > 0) ? WhatsQueued() : new List<Queued>();
        while (_tiles.Count < items.Count) _tiles.Add(MakeTile());
        for (int i = 0; i < _tiles.Count; i++)
        {
            var t = _tiles[i];
            t.Root.Visible = i < items.Count;
            if (i >= items.Count) continue;
            var q = items[i];
            t.Icon.Texture = Icons.For(this, q.Model, TeamColor[_me]);
            t.Count.Text = q.Count;
            t.Note.Text = q.Note;
            t.Fill.Size = new Vector2((TileSize - 8) * Mathf.Clamp(q.Progress, 0, 1), 5);
            t.Fill.Color = q.Bar;
            t.Root.TooltipText = q.Tip;
            t.BuildingId = q.BuildingId;
        }
    }
}
