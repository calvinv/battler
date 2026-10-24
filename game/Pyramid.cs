using System.Collections.Generic;
using Battler.Sim;
using Godot;

namespace Battler.Game;

/// <summary>
/// The pyramid HQ (tools/art/model_pyramid.py; the user, 2026-10-24: "the main base is a large central pyramid and the
/// unit leave from it, the bases expands as you collect more resources and will have a look that kind of tells you what
/// it is producing"). It shows its side's tier (taller, wider; from tier 2 a spire with a glowing core in a cage,
/// spire concept 15, whose light gets stronger each tier), an add-on for each tech branch
/// researched (bigger with each step down the branch), and a door on each face: a unit being built shows as a hologram
/// in its doorway, the shutter drops as it's finished and it drives out down the ramp.
/// </summary>
public partial class Main
{
    sealed class PyramidView
    {
        public Node3D Root, Model;
        public OmniLight3D Core;
        public int Tier;
        public float Pulse;
        public readonly Dictionary<Branch, (Node3D Node, int Level)> Addons = new();
        public readonly Node3D[] Doors = new Node3D[World.MaxDoors];   // by sim door (World.DoorDir)
        public readonly Vector3[] DoorRest = new Vector3[World.MaxDoors];
        public readonly float[] Open = new float[World.MaxDoors], SinceMade = { 99f, 99f, 99f, 99f };
        public readonly Node3D[] Ghosts = new Node3D[World.MaxDoors];
        public readonly int[] GhostKind = { -1, -1, -1, -1 };
        public readonly List<Node3D> Spinners = new();
    }

    readonly Dictionary<int, PyramidView> _pyramids = new();

    // The model's shape per tier (model units; the same table as model_pyramid.py's SHAPE): half the base's width,
    // steps above the bottom one, the spire's height.
    static readonly (float Half, int Steps, float Spire)[] PyramidShape = { (22, 2, 0), (24, 3, 8), (26, 4, 16), (28, 5, 24), (30, 6, 32) };
    const float PyrBaseH = 11f, PyrStepH = 3.6f, PyrTopHalf = 7f, PyrChamfer = 0.35f;

    static (float Half, int Steps, float Spire, float Inset, float Top) ShapeOf(int tier)
    {
        var (half, steps, spire) = PyramidShape[Mathf.Clamp(tier, 1, 5) - 1];
        return (half, steps, spire, (half - PyrTopHalf) / (steps + 1), PyrBaseH + steps * PyrStepH);
    }

    /// <summary>How tall the pyramid stands at a tier, world units (its health bar goes over it).</summary>
    static float PyramidTop(int tier) { var s = ShapeOf(tier); return (s.Top + s.Spire * 0.5f + 3f) * UnitArt.Scale; }

    Node3D MakePyramid(Entity hq)
    {
        var view = new PyramidView { Root = new Node3D() };
        view.Root.Basis = Basis.LookingAt(TowardEnemy(hq.Owner), Vector3.Up);
        _pyramids[hq.Id] = view;
        SetTier(hq, view, W.Players[hq.Owner].Tier);
        return view.Root;
    }

    void SetTier(Entity hq, PyramidView view, int tier)
    {
        view.Model?.QueueFree();
        view.Tier = tier;
        view.Model = UnitArt.Make($"hq_t{tier}", TeamColor[hq.Owner]);
        view.Root.AddChild(view.Model);
        view.Spinners.Clear();
        var found = new List<Node3D>();
        Collect(view.Model, n =>
        {
            string name = n.Name.ToString();
            if (name.StartsWith("door_")) found.Add(n);
            else if (name.StartsWith("spin")) view.Spinners.Add(n);
        });
        // Which shutter is which sim door: the one lying furthest that way from the middle.
        for (int d = 0; d < World.MaxDoors; d++)
        {
            var want = Flat(W.DoorDir(hq.Owner, d));
            Node3D best = null;
            float bestDot = -2;
            foreach (var door in found)
            {
                var off = (view.Root.Basis * RelativeTo(door, view.Root).Origin) with { Y = 0 };
                float dot = off.Normalized().Dot(want);
                if (dot > bestDot) { bestDot = dot; best = door; }
            }
            view.Doors[d] = best;
            if (best != null) view.DoorRest[d] = best.Position;
        }
        // The core's light: none before the spire, then stronger and further reaching each tier.
        view.Core?.QueueFree();
        view.Core = null;
        var shape = ShapeOf(tier);
        if (shape.Spire > 0)
        {
            view.Core = new OmniLight3D
            {
                Position = Vector3.Up * (shape.Top + 2.2f + shape.Spire * 0.5f) * UnitArt.Scale,
                LightColor = new Color(1f, 0.68f, 0.28f),
                LightEnergy = 0.8f + 0.55f * (tier - 2),
                OmniRange = (shape.Half + 4f * tier) * UnitArt.Scale,
                OmniAttenuation = 1.4f,
                ShadowEnabled = false,
            };
            view.Root.AddChild(view.Core);
        }
        // The add-ons move with the tier's shape.
        foreach (var (branch, (node, level)) in view.Addons) PlaceAddon(view, branch, node, level);
    }

    /// <summary>A node's transform in an ancestor's frame (it may not be in the scene yet).</summary>
    static Transform3D RelativeTo(Node3D n, Node3D ancestor)
    {
        var t = n.Transform;
        for (var p = n.GetParent() as Node3D; p != null && p != ancestor; p = p.GetParent() as Node3D) t = p.Transform * t;
        return t;
    }

    static void Collect(Node node, System.Action<Node3D> each)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n) each(n);
            Collect(child, each);
        }
    }

    /// <summary>Where each branch's add-on goes on the pyramid, in its own frame (forward -Z toward the enemy, +X its
    /// right): the vehicle hangar and the walker crane at the front corners, the dish and the tooling works at the back
    /// ones, the gun battery on a ledge of the front face, the air deck round the spire (or on a back ledge while there
    /// is none).</summary>
    void PlaceAddon(PyramidView view, Branch branch, Node3D node, int level)
    {
        var (half, steps, spire, inset, top) = ShapeOf(view.Tier);
        float s = UnitArt.Scale, corner = half - half * PyrChamfer / 2 + 2f;
        int ledge = Mathf.Max(0, steps / 2 - 1);
        float ledgeZ = PyrBaseH + ledge * PyrStepH, ledgeN = half - inset * (ledge + 1) + 2.5f;
        (Vector3 At, Vector3 Facing) spot = branch switch
        {
            Branch.Armour => (new Vector3(-corner, 0, -corner), new Vector3(-1, 0, -1)),
            Branch.Walkers => (new Vector3(corner, 0, -corner), new Vector3(1, 0, -1)),
            Branch.Support => (new Vector3(-corner, 0, corner), new Vector3(-1, 0, 1)),
            Branch.Core => (new Vector3(corner, 0, corner), new Vector3(1, 0, 1)),
            Branch.Guns => (new Vector3(0, ledgeZ, -ledgeN), Vector3.Forward),
            _ => spire > 0 ? (new Vector3(0, top + 0.6f + spire * 0.22f, 0), Vector3.Forward) : (new Vector3(0, ledgeZ, ledgeN + 3f), Vector3.Back),
        };
        node.Position = spot.At * s;
        node.Basis = Basis.LookingAt(spot.Facing.Normalized(), Vector3.Up).Scaled(Vector3.One * (1f + 0.15f * (level - 1)));
    }

    static string AddonModel(Branch b) => "addon_" + b.ToString().ToLowerInvariant();

    /// <summary>One frame of a pyramid: its tier, its add-ons, its doors and the units forming in them.</summary>
    void UpdatePyramid(Entity hq, Node3D root, float dt)
    {
        if (!_pyramids.TryGetValue(hq.Id, out var view)) return;
        var p = W.Players[hq.Owner];
        if (view.Tier != p.Tier) SetTier(hq, view, p.Tier);
        // Add-ons: a branch shows once anything in it is researched, bigger the further down it.
        var levels = new Dictionary<Branch, int>();
        foreach (var t in Techs.All) if (p.Has[(int)t.Id]) levels[t.Branch] = levels.GetValueOrDefault(t.Branch) + 1;
        foreach (var (branch, level) in levels)
        {
            if (view.Addons.TryGetValue(branch, out var had) && had.Level == level) continue;
            var node = had.Node;
            if (node == null) { node = UnitArt.Make(AddonModel(branch), TeamColor[hq.Owner]); view.Root.AddChild(node); }
            view.Addons[branch] = (node, level);
            PlaceAddon(view, branch, node, level);
        }
        foreach (var s in view.Spinners) s.RotateY(dt * 0.5f);
        if (view.Core != null)   // a slow throb
        {
            view.Pulse += dt;
            view.Core.LightEnergy = (0.8f + 0.55f * (view.Tier - 2)) * (0.85f + 0.15f * Mathf.Sin(view.Pulse * 1.7f));
        }
        foreach (var (_, (node, _)) in view.Addons)
            Collect(node, n => { if (n.Name.ToString().StartsWith("spin")) n.RotateY(dt * 0.6f); });

        // The doors.
        var (half, _, _, _, _) = ShapeOf(view.Tier);
        for (int d = 0; d < World.MaxDoors; d++)
        {
            int making = d < World.DoorsOf(p) ? p.DoorMakes[d] : -1;
            view.SinceMade[d] += dt;
            // A unit forming in the doorway, as a hologram.
            if (view.Ghosts[d] != null && view.GhostKind[d] != making) { view.Ghosts[d].QueueFree(); view.Ghosts[d] = null; view.GhostKind[d] = -1; }
            if (making >= 0 && W.Started && W.Winner < 0)
            {
                var kind = (Kind)making;
                if (view.Ghosts[d] == null)
                {
                    var ghost = UnitArt.MakeBuilding(UnitModel(kind, CivOf(hq.Owner)), TeamColor[hq.Owner]);
                    AddChild(ghost);
                    var dir = Flat(W.DoorDir(hq.Owner, d));
                    ghost.Position = ToV3(hq.Pos) + dir * (half * UnitArt.Scale - 0.3f) + Vector3.Up * HeightOf(kind) * 0.3f;
                    ghost.Basis = Basis.LookingAt(dir, Vector3.Up);
                    view.Ghosts[d] = ghost;
                    view.GhostKind[d] = making;
                }
                view.Ghosts[d].Visible = root.Visible;
                UnitArt.SetBuilt(view.Ghosts[d], 1f - p.DoorTimer[d] / (float)World.BuildTicksFor(p, kind));
            }
            // The shutter drops as the unit is nearly done, stays down while it drives out, and comes up behind it.
            bool open = view.SinceMade[d] < 1.4f || making >= 0 && p.DoorTimer[d] <= World.TicksPerSecond;
            view.Open[d] = Mathf.MoveToward(view.Open[d], open ? 1f : 0f, dt * (open ? 1.8f : 1.1f));
            if (view.Doors[d] is { } door)
            {
                float e = view.Open[d] * view.Open[d] * (3 - 2 * view.Open[d]);
                door.Position = view.DoorRest[d] + Vector3.Down * (PyrBaseH - 2f) * e;
            }
        }
    }

    /// <summary>A unit has just come out of door `d` of its HQ: the door stays open for it, and it drives out from
    /// where it formed. Returns where it formed (null if there was no hologram).</summary>
    Vector3? MadeAtDoor(Entity unit)
    {
        if (unit.Door < 0 || !_pyramids.TryGetValue(unit.FactoryId, out var view)) return null;
        view.SinceMade[unit.Door] = 0;
        return view.Ghosts[unit.Door]?.Position;
    }

    void ForgetPyramid(int id)
    {
        if (!_pyramids.Remove(id, out var view)) return;
        foreach (var g in view.Ghosts) g?.QueueFree();
    }
}
