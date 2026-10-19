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

    static Shader _teamShader;
    static readonly Dictionary<(string, Color), ShaderMaterial> Materials = new();

    public static Node3D Make(string name, Color team)
    {
        var model = GD.Load<PackedScene>($"res://art/units/{name}.glb").Instantiate<Node3D>();
        model.Scale = Vector3.One * Scale;
        if (!Materials.TryGetValue((name, team), out var mat))
        {
            _teamShader ??= GD.Load<Shader>("res://game/team_paint.gdshader");
            mat = new ShaderMaterial { Shader = _teamShader };
            mat.SetShaderParameter("skin", GD.Load<Texture2D>($"res://art/units/{name}.png"));
            mat.SetShaderParameter("team", team);
            Materials[(name, team)] = mat;
        }
        foreach (var mesh in Meshes(model)) mesh.MaterialOverride = mat;
        var root = new Node3D();   // so callers can turn it with LookingAt (-Z forward) without fighting the model's +Z
        model.RotationDegrees = new Vector3(0, 180, 0);
        root.AddChild(model);
        return root;
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
    }

    static IEnumerable<MeshInstance3D> Meshes(Node n)
    {
        if (n is MeshInstance3D m) yield return m;
        foreach (var c in n.GetChildren())
            foreach (var mm in Meshes(c)) yield return mm;
    }
}
