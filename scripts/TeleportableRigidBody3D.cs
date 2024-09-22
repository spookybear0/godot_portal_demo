using Godot;
using System;

public partial class TeleportableRigidBody3D : RigidBody3D {
    public Portal IgnorePortal { get; set; }
}