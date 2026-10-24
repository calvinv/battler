using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The build queue along the bottom of the screen (the user, 2026-10-22: "we should have a build queue with icons of
/// things queued along the bottom"): one tile per thing of mine on the way, with its picture, a bar of how far along
/// it is, and a line under it.
///   - what each of the HQ's doors is building (seconds left)
///   - the units waiting on the HQ's queue (click one to take it off)
///   - the research under way and waiting
///   - defences going up, and upgrades waiting at one for the metal
/// Clicking any other tile picks that building and looks at it.
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
        public int Unqueue = -1, Unresearch = -1;   // a waiting unit's (or research's) place in its queue: a click drops it
    }

    /// <summary>One thing on the way: its model, how far along (0..1), a count over it (blank for one), a note under
    /// it, the building it's at, and a tint for the bar.</summary>
    readonly record struct Queued(string Model, float Progress, string Count, string Note, int BuildingId, Color Bar, string Tip, int Unqueue = -1, int Unresearch = -1);

    static readonly Color BarBuild = new(0.35f, 0.75f, 1f), BarMake = new(0.45f, 0.85f, 0.4f),
        BarWait = new(0.95f, 0.75f, 0.25f), BarStuck = new(0.6f, 0.6f, 0.6f);
    static readonly string[] DefenceUpgradeShort = { "Range", "Damage", "Armour" };

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
            if (t.Unqueue >= 0) { _session.Issue(new Unqueue(_me, t.Unqueue)); return; }
            if (t.Unresearch >= 0) { _session.Issue(new CancelResearch(_me, t.Unresearch)); return; }
            if (W.Find(t.BuildingId) is not { } b) return;
            _selected = b.Id;
            _focus = ToV3(b.Pos);
        };
        _queueBar.AddChild(t.Root);
        return t;
    }

    /// <summary>Everything of mine on the way, in order: the HQ's doors, its queue, research, then defences.</summary>
    List<Queued> WhatsQueued()
    {
        var list = new List<Queued>();
        var civ = MyCiv;
        var p = W.Players[_me];
        int tps = World.TicksPerSecond, hq = MyHq()?.Id ?? -1;
        bool full = W.ArmyCount(_me) >= World.MaxArmyOf(civ);
        for (int d = 0; d < World.DoorsOf(p); d++)
        {
            if (p.DoorMakes[d] < 0) continue;
            var kind = (Kind)p.DoorMakes[d];
            float total = World.BuildTicksFor(p, kind);
            list.Add(new Queued(UnitModel(kind, civ), 1f - p.DoorTimer[d] / total, "", $"{Mathf.CeilToInt(p.DoorTimer[d] / (float)tps)} s", hq, BarMake,
                $"{NameOf(kind)}: being built at door {d + 1}"));
        }
        for (int i = 0; i < p.Queue.Count; i++)
        {
            var kind = (Kind)p.Queue[i];
            int cost = World.UnitCost(kind);
            bool head = i == 0;
            string note = head && World.IsArmy(kind) && full ? "army full" : head && p.Metal < cost ? $"{cost}" : "waiting";
            list.Add(new Queued(UnitModel(kind, civ), head ? Mathf.Clamp(p.Metal / (float)cost, 0, 1) : 0, "", note, hq, head && p.Metal < cost ? BarWait : BarStuck,
                $"{NameOf(kind)} queued ({cost} metal when a door starts it). Click to take it off.", Unqueue: i));
        }
        for (int i = 0; i < p.Research.Count; i++)
        {
            var t = Techs.Of((Tech)p.Research[i]);
            float total = t.Seconds * tps;
            bool going = i == 0 && p.ResearchLeft > 0;
            list.Add(new Queued(TechModel(t), going ? 1f - p.ResearchLeft / total : 0, "", going ? $"{Mathf.CeilToInt(p.ResearchLeft / (float)tps)} s" : t.Name, hq, BarBuild,
                $"Research: {t.Name} ({t.Cost} metal, paid as it goes). Click to drop it.", Unresearch: i));
        }
        foreach (var e in W.Entities)
        {
            if (e.Dead || e.Owner != _me) continue;
            if (e.Building > 0)
            {
                int total = World.ConstructionTicks(e.Kind);
                list.Add(new Queued(BuildingModel(e.Kind, _me), 1f - e.Building / (float)total, "",
                    $"{Mathf.CeilToInt(e.Building / (float)tps)} s", e.Id, BarBuild, "Going up: " + NameOf(e.Kind)));
                continue;
            }
            for (int i = 0; i < e.UpgradeQueue.Count; i++)
            {
                var which = (Upgrade)e.UpgradeQueue[i];
                int level = e.Level(which) + 1;
                for (int j = 0; j < i; j++) if (e.UpgradeQueue[j] == (int)which) level++;
                int cost = World.UpgradeCost(level);
                float ready = i == 0 ? Mathf.Clamp(p.Metal / (float)cost, 0, 1) : 0;
                list.Add(new Queued(BuildingModel(e.Kind, _me), ready, "+" + DefenceUpgradeShort[(int)which][0] + level,
                    $"{DefenceUpgradeShort[(int)which]} {cost}", e.Id, BarWait, $"{DefenceUpgradeShort[(int)which]} {level} queued, waiting for {cost} metal"));
            }
        }
        return list;
    }

    void UpdateQueueBar()
    {
        var items = WhatsQueued();
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
            t.Unqueue = q.Unqueue;
            t.Unresearch = q.Unresearch;
        }
    }
}
