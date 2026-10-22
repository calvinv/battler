using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// Little pictures of the models for the HUD (the build queue along the bottom). Each is the real model, in the team's
/// colour, drawn once from a three-quarter view by an offscreen camera in its own little world, so the icons always
/// match what's on the field.
/// </summary>
public static class Icons
{
    const int Size = 128;
    static readonly Dictionary<(string, Color), Texture2D> Made = new();

    /// <summary>The icon for art/units/<paramref name="model"/>.glb; `host` keeps the offscreen view alive.</summary>
    public static Texture2D For(Node host, string model, Color team)
    {
        if (Made.TryGetValue((model, team), out var tex)) return tex;
        var view = new SubViewport
        {
            Size = new Vector2I(Size, Size), OwnWorld3D = true, TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once, Msaa3D = Viewport.Msaa.Msaa4X,
        };
        host.AddChild(view);
        var thing = UnitArt.Make(model, team);
        view.AddChild(thing);
        view.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.55f, 0.57f, 0.62f),
                AmbientLightEnergy = 1.0f,
            },
        });
        var sun = new DirectionalLight3D { LightEnergy = 1.3f };
        view.AddChild(sun);
        sun.LookAtFromPosition(Vector3.Zero, new Vector3(-0.5f, -1f, -0.3f), Vector3.Up);

        // Frame the model: its bounds, seen from the front-right and above, filling the picture.
        var box = Bounds(thing);
        var centre = box.GetCenter();
        float reach = box.Size.Length() * 0.5f;
        var dir = new Vector3(0.75f, 0.75f, -0.9f).Normalized();   // (models face -Z once UnitArt has turned them)
        var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = reach * 1.3f, Near = 0.05f, Far = reach * 8 };
        view.AddChild(cam);
        cam.LookAtFromPosition(centre + dir * reach * 3, centre, Vector3.Up);
        cam.Current = true;

        tex = view.GetTexture();
        Made[(model, team)] = tex;
        return tex;
    }

    static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        foreach (var mesh in UnitArt.Meshes(root))
        {
            var b = mesh.GlobalTransform * mesh.GetAabb();
            box = box is { } a ? a.Merge(b) : b;
        }
        return box ?? new Aabb(-Vector3.One, Vector3.One * 2);
    }
}
