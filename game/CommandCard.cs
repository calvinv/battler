using System.Collections.Generic;
using System.Linq;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The command card (bottom right): select something of yours to see what it can do. A base shows the wings it can
/// take, the extractor and, a page further, the defences; a wing its upgrades, fitting and Sell; a utility wing scouts
/// and mobile bases; a mobile base "Set up". Hovering a button shows its cost and numbers, and why it can't be done
/// yet if it can't. Each button's letter works as a key while the card is up; B brings up a base's card.
/// (The user, 2026-10-22: "just click on your base and have popups of what to build with stats".)
/// </summary>
public partial class Main
{
    /// <summary>One button: key, label, icon (a model) or text glyph, cost (0 for none), popup title and body, why it
    /// can't be done now (null if it can), and what it does.</summary>
    sealed record CardItem(Key Key, string Label, string Model, string Glyph, int Cost, string Title, string Body, string Blocked, System.Action Do);

    const int CardColumns = 4, CardRows = 3, CardSlot = 74;
    PanelContainer _card, _popup;
    RichTextLabel _popupText;
    Label _cardTitle;
    readonly List<(Button Root, TextureRect Icon, Label Glyph, Label Key, Label Cost)> _slots = new();
    List<CardItem> _items = new();
    int _hover = -1;
    string _cardPage = "main";   // a base's card: "main" or "defences"
    int _cardFor = -1;           // whose card it is (a new pick goes back to the main page)

    void BuildCommandCard(CanvasLayer ui)
    {
        var frame = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.07f, 0.9f), BorderColor = new Color(0.5f, 0.45f, 0.35f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 8,
        };
        _card = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, Visible = false };
        _card.AddThemeStyleboxOverride("panel", frame);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        _cardTitle = new Label();
        _cardTitle.AddThemeFontSizeOverride("font_size", 15);
        _cardTitle.Modulate = new Color(1f, 0.9f, 0.7f);
        column.AddChild(_cardTitle);
        var grid = new GridContainer { Columns = CardColumns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        for (int i = 0; i < CardColumns * CardRows; i++)
        {
            int at = i;
            var b = new Button { CustomMinimumSize = new Vector2(CardSlot, CardSlot), FocusMode = Control.FocusModeEnum.None };
            var icon = new TextureRect
            {
                Position = new Vector2(4, 4), Size = new Vector2(CardSlot - 8, CardSlot - 8), MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            var glyph = new Label
            {
                Size = new Vector2(CardSlot, CardSlot), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            glyph.AddThemeFontSizeOverride("font_size", 14);
            var key = new Label { Position = new Vector2(5, 2), MouseFilter = Control.MouseFilterEnum.Ignore };
            key.AddThemeFontSizeOverride("font_size", 16);
            key.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            key.AddThemeColorOverride("font_outline_color", Colors.Black);
            key.AddThemeConstantOverride("outline_size", 4);
            var cost = new Label { Position = new Vector2(0, CardSlot - 22), Size = new Vector2(CardSlot - 5, 20), HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
            cost.AddThemeFontSizeOverride("font_size", 13);
            cost.AddThemeColorOverride("font_outline_color", Colors.Black);
            cost.AddThemeConstantOverride("outline_size", 4);
            b.AddChild(icon); b.AddChild(glyph); b.AddChild(key); b.AddChild(cost);
            b.Pressed += () => Press(at);
            b.MouseEntered += () => _hover = at;
            b.MouseExited += () => { if (_hover == at) _hover = -1; };
            grid.AddChild(b);
            _slots.Add((b, icon, glyph, key, cost));
        }
        column.AddChild(grid);
        _card.AddChild(column);
        ui.AddChild(_card);

        _popup = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.08f, 0.95f), BorderColor = new Color(0.45f, 0.75f, 0.9f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 10,
        });
        _popupText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(330, 0), ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _popupText.AddThemeFontSizeOverride("normal_font_size", 15);
        _popupText.AddThemeFontSizeOverride("bold_font_size", 18);
        _popup.AddChild(_popupText);
        ui.AddChild(_popup);
    }

    void Press(int at)
    {
        if (at >= _items.Count || W.Winner >= 0) return;
        var item = _items[at];
        if (item.Blocked != null && item.Blocked != "not enough metal") return;   // (short of metal still goes through: upgrades queue)
        item.Do();
    }

    /// <summary>Presses the card button with this key, if any. True if it used the key.</summary>
    bool CardKey(Key key)
    {
        if (!_card.Visible) return false;
        if (key == Key.Escape && _cardPage != "main") { _cardPage = "main"; return true; }
        int at = _items.FindIndex(i => i.Key == key);
        if (at < 0) return false;
        Press(at);
        return true;
    }

    /// <summary>B: select a base to show its card (the picked one, a picked wing's, or the HQ).</summary>
    void OpenBuildCard()
    {
        if (WingTarget() is not { } b) return;
        _scouts.Clear();
        _selected = b.Id;
        _cardPage = "main";
    }

    void UpdateCard()
    {
        var sel = W.Find(_selected);
        if (sel == null || sel.Owner != _me || W.Winner >= 0) { _card.Visible = _popup.Visible = false; _cardFor = -1; return; }
        if (sel.Id != _cardFor) { _cardFor = sel.Id; _cardPage = "main"; }
        _items = CardItems(sel);
        _card.Visible = _items.Count > 0;
        if (!_card.Visible) { _popup.Visible = false; return; }
        _cardTitle.Text = CardTitle(sel);
        for (int i = 0; i < _slots.Count; i++)
        {
            var (root, icon, glyph, key, cost) = _slots[i];
            root.Visible = true;
            if (i >= _items.Count) { root.Disabled = true; icon.Texture = null; glyph.Text = key.Text = cost.Text = ""; root.Modulate = new Color(1, 1, 1, 0.25f); continue; }
            var it = _items[i];
            root.Disabled = false;
            icon.Texture = it.Model != null ? Icons.For(this, it.Model, TeamColor[_me]) : null;
            glyph.Text = it.Model == null ? it.Glyph : "";
            key.Text = KeyName(it.Key);
            cost.Text = it.Cost > 0 ? it.Cost.ToString() : "";
            cost.AddThemeColorOverride("font_color", W.Players[_me].Metal >= it.Cost ? Colors.White : new Color(1f, 0.4f, 0.35f));
            root.Modulate = it.Blocked == null ? Colors.White : new Color(1, 1, 1, 0.45f);
        }
        _card.OffsetLeft = -_card.GetCombinedMinimumSize().X - 16;
        _card.OffsetTop = -_card.GetCombinedMinimumSize().Y - 16;
        _card.OffsetRight = -16; _card.OffsetBottom = -16;

        // The popup for the button under the mouse.
        _popup.Visible = _hover >= 0 && _hover < _items.Count;
        if (_popup.Visible)
        {
            var it = _items[_hover];
            var text = $"[b]{it.Title}[/b]   [color=#ffd860]{KeyName(it.Key)}[/color]";
            if (it.Cost > 0) text += $"\n[color=#c8e0ff]Cost {it.Cost} metal[/color]";
            if (!string.IsNullOrEmpty(it.Body)) text += "\n" + it.Body;
            if (it.Blocked != null) text += $"\n[color=#ff7060]{Capital(it.Blocked)}[/color]";
            _popupText.Text = text;
            var size = _popup.GetCombinedMinimumSize();
            _popup.OffsetLeft = _card.OffsetLeft;
            _popup.OffsetRight = _card.OffsetLeft + Mathf.Max(size.X, 330);
            _popup.OffsetBottom = _card.OffsetTop - 6;
            _popup.OffsetTop = _popup.OffsetBottom - size.Y;
        }
    }

    static string KeyName(Key k) => k switch { Key.Delete => "Del", Key.Escape => "Esc", _ => k.ToString() };
    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    string CardTitle(Entity e) => e.Kind switch
    {
        Kind.Hq or Kind.Outpost => (e.Kind == Kind.Hq ? "HQ" : "Base") + (_cardPage == "defences" ? ": structures" : _cardPage == "modules" ? ": modules" : ": build"),
        Kind.Extractor or Kind.PowerPlant => NameOf(e.Kind) + (_cardPage == "modules" ? ": modules" : ": build"),
        Kind.Module => ModName(e.Mod) + " module",
        Kind.Factory => World.IsUtility(e) ? "Utility wing" : NameOf(e.Makes) + " wing",
        _ => NameOf(e.Kind),
    };

    // ---- each card's buttons ------------------------------------------------------------------------------------

    List<CardItem> CardItems(Entity e)
    {
        var items = new List<CardItem>();
        if (e.Building > 0 && e.Kind != Kind.MobileBase) { items.Add(SellItem(e)); return items.Where(i => i != null).ToList(); }
        switch (e.Kind)
        {
            case Kind.Hq or Kind.Outpost or Kind.Extractor or Kind.PowerPlant:
                if (_cardPage == "defences" && World.IsBase(e)) DefenceItems(e, items);
                else if (_cardPage == "modules" && World.IsWingHost(e)) ModuleItems(items);
                else BaseItems(e, items);
                break;
            case Kind.Module:
                items.Add(SellItem(e));
                break;
            case Kind.Factory when World.IsUtility(e):
                var scout = World.ScoutKindOf(MyCiv);
                items.Add(OrderItem(e, Key.C, scout, $"A {NameOf(scout).ToLowerInvariant()}: unarmed eyes" + (scout == Kind.Drone ? ", flies" : "") + $". Up to {World.MaxScouts}.\nRight-click to send it."));
                items.Add(OrderItem(e, Key.G, Kind.MobileBase, $"Drive it out and set it up (F) where you want a base:\nextractors and power plants within {World.BaseReach}\n(wings go on those), radar {World.OutpostRadar}. At least {World.BaseSpacing} from any base. Up to {World.MaxBases} bases."));
                items.Add(SellItem(e));
                break;
            case Kind.Factory:
                if (World.CanFitNapalm(e.Makes)) items.Add(NapalmItem(e));
                if (e.HasRally) items.Add(new CardItem(Key.T, "Rally", null, "Clear\nrally", 0, "Clear rally point", "New units attack straight away (right-click the ground to set one).", null,
                    () => _session.Issue(new SetRally(_me, e.Id, true, FixVec2.Zero))));
                items.Add(SellItem(e));
                break;
            case Kind.Turret or Kind.Flak or Kind.Bombard:
                UpgradeItems(e, items, true);
                items.Add(SellItem(e));
                break;
            case Kind.Wall:
                items.Add(SellItem(e));
                break;
            case Kind.MobileBase:
                bool can = W.CanDeploy(_me, e.Id, out var why);
                items.Add(new CardItem(Key.F, "Set up", BuildingModel(Kind.Outpost, Kind.Hq, MyCiv), null, 0, "Set up here",
                    $"Becomes a base where it stands, in {World.ConstructionTicks(Kind.Outpost) / World.TicksPerSecond} s:\nextractors and power plants within {World.BaseReach}, radar {World.OutpostRadar}.",
                    can ? null : why, DeployOrPack));
                break;
        }
        return items.Where(i => i != null).ToList();
    }

    static readonly Key[] CoalitionWingKeys = { Key.L, Key.T, Key.M, Key.K, Key.J, Key.F, Key.H, Key.N };
    static readonly Key[] LatticeWingKeys = { Key.C, Key.I, Key.K, Key.J, Key.F, Key.H };

    /// <summary>A base's or a wing host's card: the wings it takes (the HQ, extractors, power plants), the
    /// structures page (the HQ and set-up bases), pack up, sell.</summary>
    void BaseItems(Entity b, List<CardItem> items)
    {
        if (World.IsWingHost(b))
        {
            WingItems(b, items);
            items.Add(new CardItem(Key.V, "Blank", ModModel(Mod.Blank), null, World.ModuleCost(Mod.Blank), "Blank",
                ModDoes(Mod.Blank) + $"\nTakes {World.ModuleBuildTicks(Mod.Blank) / World.TicksPerSecond} s.", LineWhy(Kind.Hq, Mod.Blank), () => StartLinePick(Kind.Hq, Mod.Blank)));
            items.Add(new CardItem(Key.G, "Modules", ModModel(Mod.Attack), null, 0, "Modules  \u25b8",
                "Snap modules onto a line: every wing after them on it gets\nwhat they do (speed, armour, attack, build speed, energy,\nand the wildcards).", null, () => _cardPage = "modules"));
        }
        if (World.IsBase(b))
            items.Add(new CardItem(Key.N, "Structures", BuildingModel(Kind.PowerPlant, Kind.Hq, MyCiv), null, 0, "Structures  \u25b8",
                $"Extractors, power plants and defences, placed within {World.BaseReach} of a base.", W.Started ? null : "once the battle starts", () => _cardPage = "defences"));
        if (b.Kind == Kind.Outpost)
        {
            bool can = W.CanPackUp(_me, b.Id, out var why);
            items.Add(new CardItem(Key.Y, "Pack up", UnitModel(Kind.MobileBase, MyCiv), null, 0, "Pack up", "Folds the base back into a mobile base to move it.",
                can ? null : why.ToLowerInvariant(), DeployOrPack));
        }
        items.Add(SellItem(b));
    }

    /// <summary>What a host's wings would be: the sulfur and power-plant bonuses in each popup.</summary>
    string HostNote(Entity b) =>
        b.Kind == Kind.Extractor && World.DepositResource[b.Deposit] == Battler.Sim.Resource.Sulfur
            ? $"\nOn sulfur: its units hit {World.SulfurHitPercent - 100}% harder and set the ground burning."
            : b.Kind == Kind.PowerPlant ? "\nOn a power plant: it draws half the power." : "";

    void WingItems(Entity b, List<CardItem> items)
    {
        var kinds = World.FactoryKindsOf(MyCiv);
        var keys = MyCiv == Civ.Lattice ? LatticeWingKeys : CoalitionWingKeys;
        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            var why = LineWhy(kind, null);
            items.Add(new CardItem(keys[i], NameOf(kind), UnitModel(kind, MyCiv), null, World.WingCost(kind), $"{NameOf(kind)} {(MyCiv == Civ.Lattice ? "nest" : "wing")}",
                $"Snaps onto the end of a line (pick which) in {World.ConstructionTicks(Kind.Factory) / World.TicksPerSecond} s; then makes {Plural(kind)}\nfor as long as it stands, with every module before it on the line.{HostNote(b)}\nPower draw {(b.Kind == Kind.PowerPlant ? World.PowerDraw(kind) / 2 : World.PowerDraw(kind))}  (you have {W.PowerDrawOf(_me)} drawn of {W.PowerSupply(_me)}).\n\n" + UnitStats(kind),
                why, () => StartLinePick(kind, null)));
        }
        var uwhy = LineWhy(Kind.MobileBase, null);
        items.Add(new CardItem(Key.U, "Utility", BuildingModel(Kind.Factory, Kind.MobileBase, MyCiv), null, World.WingCost(Kind.MobileBase), MyCiv == Civ.Lattice ? "Utility nest" : "Utility wing",
            $"Builds scouts ({World.ScoutCost}) and mobile bases ({World.MobileBaseCost}) to order.\nMobile bases are how you expand.", uwhy,
            () => StartLinePick(Kind.MobileBase, null)));
    }

    /// <summary>Why nothing of this can go on any of my lines just now (null if it can somewhere).</summary>
    string LineWhy(Kind makes, Mod? mod)
    {
        string why = "no line of yours has room";
        foreach (var h in W.Entities)
        {
            if (h.Dead || h.Owner != _me || !World.IsWingHost(h)) continue;
            for (int slot = 0; slot < World.SlotsOf(h); slot++)
                if (W.CanAddPiece(_me, h.Id, slot, makes, mod, out var w)) return null; else why = w;
        }
        return why?.ToLowerInvariant();
    }

    static readonly Key[] ModuleKeys = { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.A, Key.S, Key.D, Key.F };

    /// <summary>The modules page: each snaps onto the end of a line (picked with the mouse).</summary>
    void ModuleItems(List<CardItem> items)
    {
        for (int i = 0; i < (int)Mod.Blank; i++)
        {
            var m = (Mod)i;
            items.Add(new CardItem(ModuleKeys[i], ModName(m), ModModel(m), null, World.ModuleCost(m), ModName(m) + " module",
                ModDoes(m) + $"\n\nSnaps onto the end of a line (point which way) in {World.ModuleBuildTicks(m) / World.TicksPerSecond} s.\nOnly wings after it on the line get it: build modules first.",
                LineWhy(Kind.Hq, m), () => StartLinePick(Kind.Hq, m)));
        }
        items.Add(new CardItem(Key.Escape, "Back", null, "\u25c2 Back", 0, "Back", "To the build card.", null, () => _cardPage = "main"));
    }

    void DefenceItems(Entity b, List<CardItem> items)
    {
        string xwhy = W.Players[_me].Metal < World.ExtractorCost ? "not enough metal" : null;
        items.Add(new CardItem(Key.X, "Extractor", BuildingModel(Kind.Extractor, Kind.Hq, MyCiv), null, World.ExtractorCost, "Extractor",
            $"Goes on a deposit within {World.ClaimReach} of one of your lines (run blanks out to it).\nMetal: {World.ExtractorIncome} a second. Sulfur: +{World.SulfurBonusPercent}% explosive damage, napalm.\nCrystal: shields on all you make. Oil: +{World.OilPower} power, +{World.OilIncome} metal a second.\nTakes {World.ConstructionTicks(Kind.Extractor) / World.TicksPerSecond} s to build.",
            xwhy, () => _placing = Kind.Extractor));
        string pwhy = W.Players[_me].Metal < World.PlantCost ? "not enough metal" : null;
        items.Add(new CardItem(Key.Z, "Power plant", BuildingModel(Kind.PowerPlant, Kind.Hq, MyCiv), null, World.PlantCost, "Power plant",
            $"Goes on a power vent (one beside every metal deposit) within {World.ClaimReach} of one of your lines.\n+{World.PlantPower} power. Your wings draw power; short of it, they all build slower.\nYou have {W.PowerDrawOf(_me)} drawn of {W.PowerSupply(_me)}. Takes {World.ConstructionTicks(Kind.PowerPlant) / World.TicksPerSecond} s to build.",
            pwhy, () => _placing = Kind.PowerPlant));
        var keys = new[] { Key.T, Key.F, Key.O, Key.L };
        for (int i = 0; i < World.DefenceKinds.Length; i++)
        {
            var kind = World.DefenceKinds[i];
            string why = !W.Started ? "once the battle starts" : W.Players[_me].Metal < World.CostOf(kind) ? "not enough metal" : null;
            var body = $"Placed within {World.BaseReach} of a base; takes {World.ConstructionTicks(kind) / World.TicksPerSecond} s to build." + (kind == Kind.Wall ? " Places one after another till Esc." : " Upgrades: range, damage, armour.") + "\n\n" + UnitStats(kind);
            items.Add(new CardItem(keys[i], NameOf(kind), BuildingModel(kind, Kind.Hq, MyCiv), null, World.CostOf(kind), DefenceName(kind), body, why, () => _placing = kind));
        }
        items.Add(new CardItem(Key.Escape, "Back", null, "\u25c2 Back", 0, "Back", "To the base's build card.", null, () => _cardPage = "main"));
    }

    string DefenceName(Kind k) => MyCiv == Civ.Lattice
        ? k switch { Kind.Turret => "Spike turret", Kind.Flak => "Flak spitter", Kind.Bombard => "Bombard mound", _ => "Scrap wall" }
        : NameOf(k);

    static readonly string[] TrackGlyph = { "SPD", "DMG", "ARM" }, DefenceGlyph = { "RNG", "DMG", "ARM" };

    void UpgradeItems(Entity e, List<CardItem> items, bool defence)
    {
        var keys = new[] { Key.Z, Key.X, Key.C };
        for (int i = 0; i < 3; i++)
        {
            var which = (Upgrade)i;
            int level = e.Level(which), queued = World.Queued(e, which), next = level + queued;
            var name = defence ? DefenceTrack[i] : (which == Upgrade.Production ? "Speed" : which.ToString());
            var effect = defence ? DefenceEffect[i] : UpgradeEffect[i];
            bool full = next >= World.MaxUpgradeLevel;
            items.Add(new CardItem(keys[i], name, null, $"{(defence ? DefenceGlyph : TrackGlyph)[i]}\n{level}/{World.MaxUpgradeLevel}" + (queued > 0 ? $" +{queued}" : ""),
                full ? 0 : World.UpgradeCost(next + 1), $"{name} upgrade  ({level}/{World.MaxUpgradeLevel})",
                (full ? "Fully upgraded." : $"Next level: {effect}" + (defence ? "." : " for units made from now on.")) + (queued > 0 ? $"\n{queued} queued: they buy themselves as metal comes in." : "\nShort of metal, it's queued and buys itself later."),
                full ? "fully upgraded" : null, () => _session.Issue(new UpgradeFactory(_me, e.Id, which))));
        }
        if (e.UpgradeQueue.Count > 0)
            items.Add(new CardItem(Key.Y, "Cancel", null, "Cancel\nqueue", 0, "Cancel queued upgrades", $"Drops the {e.UpgradeQueue.Count} waiting (nothing was paid for them).", null,
                () => _session.Issue(new CancelUpgrades(_me, e.Id))));
    }

    CardItem NapalmItem(Entity e)
    {
        bool on = e.Fitting == Fitting.Napalm;
        bool can = W.CanSetFitting(_me, e.Id, on ? Fitting.Standard : Fitting.Napalm, out var why);
        return new CardItem(Key.N, "Napalm", null, on ? "NAPALM\non" : "Napalm\noff", 0, on ? "Napalm fitted (press for standard)" : "Fit napalm",
            $"Hits do {World.NapalmHitPercent}% but leave the ground burning for {World.FireTicks / World.TicksPerSecond} s:\nunits standing in it take 4 a second (2 if armoured). Needs sulfur.",
            can ? null : why.ToLowerInvariant(), () => _session.Issue(new SetFitting(_me, e.Id, on ? Fitting.Standard : Fitting.Napalm)));
    }

    CardItem OrderItem(Entity wing, Key key, Kind what, string body)
    {
        bool can = W.CanOrder(_me, wing.Id, what, out var why);
        return new CardItem(key, NameOf(what), UnitModel(what, MyCiv), null, World.OrderCost(what), NameOf(what),
            body + $"\nTakes {World.StatsFor[(int)what].BuildTicks / World.TicksPerSecond} s.\n\n" + UnitStats(what), can ? null : why.ToLowerInvariant(),
            () => _session.Issue(new Order(_me, wing.Id, what)));
    }

    /// <summary>Sell, for what can be sold: press twice (the first press asks).</summary>
    CardItem SellItem(Entity e)
    {
        if (!W.CanSell(_me, e.Id, out _)) return null;
        bool armed = _sellArmed == e.Id && Time.GetTicksMsec() < _sellArmedUntil;
        int value = World.SellValue(e);
        return new CardItem(Key.Delete, "Sell", null, armed ? "Sure?\nSell" : $"Sell\n{value}", 0, armed ? "Press again to sell" : $"Sell for {value}",
            $"{World.SellBackPercent}% back of the {World.SpentOn(e)} spent on it." + (World.IsWingHost(e) ? " Its wings go with it." : ""), W.Started ? null : "once the battle starts",
            () =>
            {
                if (armed) { _session.Issue(new Sell(_me, e.Id)); _sellArmed = -1; _selected = -1; }
                else { _sellArmed = e.Id; _sellArmedUntil = Time.GetTicksMsec() + 3000; }
            });
    }

    static string Plural(Kind k) => NameOf(k).ToLowerInvariant() switch { var n when n.EndsWith("y") => n[..^1] + "ies", var n => n + "s" };

    /// <summary>A unit's or defence's numbers, for its popup.</summary>
    static string UnitStats(Kind k)
    {
        var s = World.StatsFor[(int)k];
        var lines = new List<string> { $"Health  {s.MaxHp * World.HealthScale / 2}" + (s.Armoured ? "   (armoured)" : "") };
        if (s.Reload > 0)
        {
            float every = s.Reload / (float)World.TicksPerSecond;
            lines.Add(s.Heals ? $"Mends  {s.Damage} health every {every:0.#} s   ({s.Damage / every:0.#} a second)" : $"Damage  {s.Damage} every {every:0.#} s   ({s.Damage / every:0.#} a second)");
            lines.Add($"Range  {s.Range}" + (s.MinRange.Raw > 0 ? $"   (nothing within {s.MinRange})" : "") + (s.Splash.Raw > 0 ? $"   splash {s.Splash}" : ""));
            var notes = new List<string>();
            if (s.VsArmour != 100) notes.Add($"x{s.VsArmour / 100f:0.#} against armour");
            if (s.HitsAir) notes.Add("hits fliers");
            if (s.Explosive) notes.Add("explosive (sulfur boosts it)");
            if (notes.Count > 0) lines.Add(Capital(string.Join(", ", notes)));
        }
        else if (s.IsUnit) lines.Add("Unarmed");
        if (s.IsUnit) lines.Add($"Speed  {s.Speed.ToFloat() * World.TicksPerSecond:0.#} a second" + (s.Flying ? "   (flies)" : ""));
        lines.Add($"Sight  {s.Vision}");
        if (s.IsUnit && World.IsArmy(k)) lines.Add($"One every {s.BuildTicks / World.TicksPerSecond} s from its wing");
        if (World.Salvage(k) > 0) lines.Add($"[color=#a0a0a0]Lost, it's {World.Salvage(k)} salvage to the enemy[/color]");
        return string.Join("\n", lines);
    }
}
