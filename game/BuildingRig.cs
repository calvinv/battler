using System.Collections.Generic;
using Godot;

namespace Battler.Game;

/// <summary>
/// A building's moving parts (tools/art/model_base.py): a wing's door sinks into the ground as a unit is about to
/// roll out and rises again behind it; parts named spin_* (the HQ's eye, a sensor dish) turn slowly.
/// </summary>
public sealed class BuildingRig
{
    Node3D _door;
    Vector3 _doorRest;
    float _doorDrop, _open, _sinceMade = 99f;
    readonly List<Node3D> _spinners = new();

    /// <summary>The rig in a building's view, or null if it has no moving parts.</summary>
    public static BuildingRig Find(Node3D view)
    {
        var rig = new BuildingRig();
        Walk(view, rig);
        if (rig._door == null && rig._spinners.Count == 0) return null;
        if (rig._door != null)
        {
            rig._doorRest = rig._door.Position;
            float h = rig._door is MeshInstance3D own ? own.Mesh.GetAabb().Size.Y : 0;
            foreach (var child in rig._door.GetChildren())
                if (child is MeshInstance3D m) h = Mathf.Max(h, m.Mesh.GetAabb().Size.Y);
            rig._doorDrop = h * 0.95f;
        }
        return rig;
    }

    static void Walk(Node node, BuildingRig rig)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n)
            {
                string name = n.Name.ToString();
                if (name == "door") rig._door = n;
                else if (name.StartsWith("spin")) rig._spinners.Add(n);
            }
            Walk(child, rig);
        }
    }

    /// <summary>A unit has just come out: the door stays open a moment for it, then closes.</summary>
    public void Made() => _sinceMade = 0;

    /// <summary>One frame. `soon`: a unit is about to be finished, so the door opens ready.</summary>
    public void Update(float dt, bool soon)
    {
        if (dt <= 0) return;
        _sinceMade += dt;
        bool open = soon || _sinceMade < 1.6f;
        _open = Mathf.MoveToward(_open, open ? 1f : 0f, dt * (open ? 1.6f : 1.1f));
        if (_door != null)
        {
            float e = _open * _open * (3 - 2 * _open);   // eased
            _door.Position = _doorRest + Vector3.Down * _doorDrop * e;
        }
        foreach (var s in _spinners) s.RotateY(dt * 0.5f);
    }
}
