using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// The unit models from tools/art/model_units.py (art/units/&lt;name&gt;.glb + .png), dressed in a team's colour.
/// Models face +Z (their front) and stand on y = 0; <see cref="Scale"/> brings the kit's sizes to the world's.
/// </summary>
public static class UnitArt
{
    public const float Scale = 0.18f;   // a tank is about 2.9 world units long

    static Shader _teamShader, _toonShader;
    static ShaderMaterial _outline;

    /// <summary>Models in the cel shaded look (tools/art/model_hover.py): toon light bands and an ink outline.</summary>
    static readonly HashSet<string> Toon = new() { "hovertank", "mech", "battlewheel", "longleg", "lobber", "skitter", "hq", "factory_lighttank", "factory_ranger", "factory_tank", "connector", "port",
                                                          "factory_mortar", "factory_artillery", "factory_mobilebase", "factory_air",
                                                          "outpost", "extractor", "powerplant" };
    static readonly Dictionary<(string, Color), ShaderMaterial> Materials = new();

    public static Node3D Make(string name, Color team)
    {
        // A model with moving parts has a rig beside it (same texture): the game animates it (UnitRig).
        string rig = $"res://art/units/{name}_rig.glb";
        var model = GD.Load<PackedScene>(ResourceLoader.Exists(rig) ? rig : $"res://art/units/{name}.glb").Instantiate<Node3D>();
        model.Scale = Vector3.One * Scale;
        if (!Materials.TryGetValue((name, team), out var mat))
        {
            bool toon = Toon.Contains(name) || name.StartsWith("module_");
            _teamShader ??= GD.Load<Shader>("res://game/team_paint.gdshader");
            _toonShader ??= GD.Load<Shader>("res://game/toon_unit.gdshader");
            mat = new ShaderMaterial { Shader = toon ? _toonShader : _teamShader };
            mat.SetShaderParameter("skin", GD.Load<Texture2D>($"res://art/units/{name}.png"));
            mat.SetShaderParameter("team", team);
            if (toon) mat.NextPass = _outline ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://game/toon_outline.gdshader") };
            Materials[(name, team)] = mat;
        }
        foreach (var mesh in Meshes(model)) mesh.MaterialOverride = mat;
        var root = new Node3D();   // so callers can turn it with LookingAt (-Z forward) without fighting the model's +Z
        model.RotationDegrees = new Vector3(0, 180, 0);
        root.AddChild(model);
        return root;
    }

    // ---- a unit being built: the same model under the "digitising" shader (build_holo.gdshader) -----------------

    static Shader _holoShader;
    static readonly Dictionary<(string, Color), ShaderMaterial> HoloMaterials = new();

    /// <summary>A ghost of the unit `name` for a factory's door; set its build progress with <see cref="SetBuilt"/>.</summary>
    public static Node3D MakeBuilding(string name, Color team)
    {
        var model = GD.Load<PackedScene>($"res://art/units/{name}.glb").Instantiate<Node3D>();
        model.Scale = Vector3.One * Scale;
        if (!HoloMaterials.TryGetValue((name, team), out var mat))
        {
            _holoShader ??= GD.Load<Shader>("res://game/build_holo.gdshader");
            mat = new ShaderMaterial { Shader = _holoShader };
            mat.SetShaderParameter("skin", GD.Load<Texture2D>($"res://art/units/{name}.png"));
            mat.SetShaderParameter("team", team);
            float top = 1f;
            foreach (var mesh in Meshes(model)) top = Mathf.Max(top, mesh.Mesh.GetAabb().End.Y);
            mat.SetShaderParameter("height", top);
            HoloMaterials[(name, team)] = mat;
        }
        foreach (var mesh in Meshes(model)) mesh.MaterialOverride = mat;
        var root = new Node3D();
        model.RotationDegrees = new Vector3(0, 180, 0);
        root.AddChild(model);
        return root;
    }

    /// <summary>How far built a ghost from <see cref="MakeBuilding"/> is, 0 to 1.</summary>
    public static void SetBuilt(Node3D ghost, float progress)
    {
        foreach (var mesh in Meshes(ghost)) mesh.SetInstanceShaderParameter("progress", progress);
    }

    // ---- upgrade emblems painted on the unit: a sword (damage) and a shield (armour) ---------------------------

    // Where the emblems lie on each model, in its root's space (forward is -Z): a clear patch of deck, its height.
    static readonly Dictionary<string, (float Z, float Y)> Deck = new()
    {
        ["tank"] = (0.85f, 0.87f),       // the back of the hull
        ["artillery"] = (-0.9f, 0.73f),  // the front deck
        ["scout"] = (0.3f, 0.55f),       // its back
        ["lighttank"] = (0.65f, 0.6f),   // the back of the hull
        ["mortar"] = (-0.55f, 0.84f),    // the front deck
        ["spider"] = (0.5f, 0.9f),       // its back, behind the turret
        ["crawler"] = (0.2f, 0.54f),     // its back plate
        ["piercer"] = (0.5f, 0.66f),     // its back, behind the gun
        ["drone"] = (0.0f, 0.3f),        // its top
    };
    static readonly Color[] Metal = { new(0.8f, 0.5f, 0.25f), new(0.85f, 0.87f, 0.92f), new(1f, 0.82f, 0.25f) };   // bronze, silver, gold
    static ImageTexture _swordTex, _shieldTex;
    const int EmblemPx = 64;

    /// <summary>A sword and/or a shield laid on the unit's deck, their metal by level (bronze, silver, gold).</summary>
    public static void AddEmblems(Node3D unit, string name, int sword, int shield)
    {
        if ((sword == 0 && shield == 0) || !Deck.TryGetValue(name, out var deck)) return;
        _swordTex ??= ImageTexture.CreateFromImage(Emblem(sword: true));
        _shieldTex ??= ImageTexture.CreateFromImage(Emblem(sword: false));
        bool both = sword > 0 && shield > 0;
        if (sword > 0) unit.AddChild(Plate(_swordTex, Metal[sword - 1], new Vector3(both ? -0.36f : 0, deck.Y, deck.Z)));
        if (shield > 0) unit.AddChild(Plate(_shieldTex, Metal[shield - 1], new Vector3(both ? 0.36f : 0, deck.Y, deck.Z)));
    }

    static Sprite3D Plate(Texture2D tex, Color metal, Vector3 at) => new()
    {
        Texture = tex,
        PixelSize = 0.62f / EmblemPx,
        Modulate = metal,
        Shaded = false,
        AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
        RotationDegrees = new Vector3(-90, 0, 0),   // lying flat, facing up at the camera
        Position = at + Vector3.Up * 0.03f,
    };

    /// <summary>Draws a sword or a shield, white with a dark outline (the sprite tints the white to its metal).</summary>
    static Image Emblem(bool sword)
    {
        var img = Image.CreateEmpty(EmblemPx, EmblemPx, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var ink = new Color(0.08f, 0.07f, 0.06f);
        if (sword)
        {
            // Point up: blade, crossguard, grip, pommel; the outline is the same shapes drawn fatter first.
            for (int pass = 0; pass < 2; pass++)
            {
                var c = pass == 0 ? ink : Colors.White;
                int g = pass == 0 ? 3 : 0;
                FillRect(img, 28 - g, 6 - g, 36 + g, 42 + g, c);        // blade
                FillPoly(img, new[] { new Vector2(28 - g, 8), new Vector2(32, 1 - g), new Vector2(36 + g, 8) }, c);   // point
                FillRect(img, 16 - g, 41 - g, 48 + g, 47 + g, c);       // crossguard
                FillRect(img, 29 - g, 47, 35 + g, 57 + g, c);           // grip
                FillRect(img, 27 - g, 56 - g, 37 + g, 62, c);           // pommel
            }
            FillRect(img, 31, 9, 33, 40, new Color(0.7f, 0.7f, 0.7f));  // fuller down the blade
        }
        else
        {
            Vector2[] Heater(float grow) => new[]
            {
                new Vector2(10 - grow, 8 - grow), new Vector2(54 + grow, 8 - grow), new Vector2(54 + grow, 30),
                new Vector2(32, 58 + grow), new Vector2(10 - grow, 30),
            };
            FillPoly(img, Heater(3.5f), ink);
            FillPoly(img, Heater(0), Colors.White);
            FillRect(img, 30, 12, 34, 50, new Color(0.6f, 0.6f, 0.6f)); // a band down the middle
            FillRect(img, 14, 22, 50, 26, new Color(0.6f, 0.6f, 0.6f)); // and across
        }
        return img;
    }

    static void FillRect(Image img, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(EmblemPx, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(EmblemPx, x1); x++) img.SetPixel(x, y, c);
    }

    static void FillPoly(Image img, Vector2[] poly, Color c)
    {
        for (int y = 0; y < EmblemPx; y++)
            for (int x = 0; x < EmblemPx; x++)
                if (Geometry2D.IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), poly)) img.SetPixel(x, y, c);
    }

    /// <summary>
    /// Lets go of the cached shader and materials. Call when the game closes: Godot objects held in C# statics
    /// outlive the engine's shutdown, which then complains about render resources it can't free
    /// ("BUG: Unreferenced static string to 0: luminance_buffers" and the like).
    /// </summary>
    public static void Release()
    {
        foreach (var m in Materials.Values) m.Dispose();
        Materials.Clear();
        _teamShader?.Dispose();
        _teamShader = null;
        _swordTex?.Dispose();
        _shieldTex?.Dispose();
        _swordTex = _shieldTex = null;
        foreach (var m in HoloMaterials.Values) m.Dispose();
        HoloMaterials.Clear();
        _holoShader?.Dispose();
        _holoShader = null;
    }

    public static IEnumerable<MeshInstance3D> Meshes(Node n)
    {
        if (n is MeshInstance3D m) yield return m;
        foreach (var c in n.GetChildren())
            foreach (var mm in Meshes(c)) yield return mm;
    }
}
