using Godot;
using System;
using Godot.Collections;
using System.Linq;

public enum PortalColor {
    Blue,
    Orange
}

[Tool]
public partial class Portal : Node3D {
    public SubViewport sub;
    public Camera3D camera;
    public MeshInstance3D screen;
    public Area3D area;

    public StaticBody3D adjWall; // the wall that the portal is attached to, can be null

    // store copies of objects that go through the portal
    public Dictionary<Node, Node> clones = new Dictionary<Node, Node>();

    [Export]
    public PortalColor portalColor = PortalColor.Blue;

    bool init = false;

    public override void _EnterTree() {
        if (Engine.IsEditorHint()) {
            _Ready();
        }
    }

    public override void _Ready() {
        sub = GetNode<SubViewport>("SubViewport");
        camera = sub.GetNode<Camera3D>("Camera3D");
        screen = GetNode<MeshInstance3D>("Screen");
        area = GetNode<Area3D>("Area3D");

        // get the wall that the portal is attached to (closest static body)

        adjWall = FindAdjacentWall();

        // set the portal to the correct portal and change cull mask
        // all are true by default
        if (portalColor == PortalColor.Blue) {
            GameManager.BluePortal = this;
            // set cull mask for the camera
            camera.SetCullMaskValue(2, false); // don't render the blue portal wall
        }
        else {
            GameManager.OrangePortal = this;
            // set cull mask for the camera
            camera.SetCullMaskValue(3, false); // don't render the orange portal wall
        }

        if (!Engine.IsEditorHint()) {
            area.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
            area.Connect("body_exited", new Callable(this, nameof(OnBodyExited)));
        }
    }

    public override void _Process(double delta) {
        if (Engine.IsEditorHint()) {
            if (EditorInterface.Singleton.GetEditedSceneRoot().Name == "Portal") {
                return;
            }
            if (GameManager.BluePortal == null || GameManager.OrangePortal == null) {
                return;
            }
        }

        adjWall = FindAdjacentWall();
        MeshInstance3D wallMesh = adjWall.GetNode<MeshInstance3D>("CollisionShape3D/MeshInstance3D");

        if (portalColor == PortalColor.Blue) {
            // set layer mask for the wall
            wallMesh.SetLayerMaskValue(1, false);
            wallMesh.SetLayerMaskValue(2, true); // don't render the blue portal wall

        }
        else {
            // set layer mask for the wall
            wallMesh.SetLayerMaskValue(1, false);
            wallMesh.SetLayerMaskValue(3, true); // don't render the orange portal wall
        }

        var tex = (portalColor == PortalColor.Blue) 
                ? GameManager.OrangePortal.sub.GetTexture() 
                : GameManager.BluePortal.sub.GetTexture();

        var shader = ResourceLoader.Load<Shader>("res://assets/shaders/portal.gdshader");
        var shader_mat = new ShaderMaterial {
            Shader = shader
        };
        shader_mat.SetShaderParameter("texture_albedo", tex);
        screen.MaterialOverride = shader_mat;

        Portal targetPortal = (portalColor == PortalColor.Blue) 
                            ? GameManager.OrangePortal 
                            : GameManager.BluePortal;

        Camera3D playerCamera = GetCamera();

        // what this line does is it moves the camera based on the player's position relative to the portal
        // and the relative rotations of the two portals
        camera.GlobalRotation = playerCamera.GlobalRotation - (targetPortal.GlobalRotation - GlobalRotation);
        camera.RotateY(Mathf.DegToRad(180));


        // move the camera to the player's position relative to the portal so that the player sees the same thing
        // for example the camera would have to be x meters behind the wall if the player is x meters away from the other portal

        camera.GlobalPosition = playerCamera.GlobalPosition - (targetPortal.GlobalPosition - GlobalPosition);


        // move copies of objects that went through the portal relative to the original object

        for (int i = 0; i < clones.Count; i++) {
            Node body = clones.Keys.ElementAt(i);
            Node copy = clones.Values.ElementAt(i);

            // common position and rotation calculations
            Vector3 positionOffset = targetPortal.GlobalPosition - GlobalPosition;
            float rotationOffset = targetPortal.GlobalRotation.Y - GlobalRotation.Y;

            if (body is Player player) {
                // rotation
                //player.RotateY(rotationOffset);
                //player.RotateY(Mathf.DegToRad(180));

                // position
                //player.GlobalPosition += positionOffset;
            }
            else if (body is TeleportableRigidBody3D rigidBody) {
                // rotation
                rigidBody.RotateY(rotationOffset);
                rigidBody.RotateY(Mathf.DegToRad(180));

                // position
                rigidBody.GlobalPosition += positionOffset;
            }
        }
    }

    public void CreateCopy(Node3D body, Portal portal) {
        // create a copy of the rigid body

        RigidBody3D copy = body.Duplicate() as RigidBody3D;
        
        // disable collision for the copy, only collide with floor
        if (body is Player) {
            ((Player)body).SetCollisionMaskValue(1, false);
            ((Player)body).SetCollisionMaskValue(2, true);
        }
        else if (body is RigidBody3D) {
            ((RigidBody3D)body).SetCollisionMaskValue(1, false);
            ((RigidBody3D)body).SetCollisionMaskValue(2, true);
        }

        copy.SetCollisionMaskValue(1, false);
        copy.SetCollisionMaskValue(2, true);
        copy.Freeze = true;

        clones[body] = copy;

        // add the copy to the scene

        GetTree().Root.AddChild(copy);

        if (body is Player player) {
        }
        else if (body is TeleportableRigidBody3D rigidBody) {
            copy.LinearVelocity = rigidBody.LinearVelocity.Rotated(Vector3.Up, Mathf.DegToRad(180)); // flip the velocity
        }

        copy.GlobalTransform = portal.GlobalTransform * (GlobalTransform.Inverse() * body.GlobalTransform);

        // teleport the copy

        //TeleportNode(copy, portal);
    }

    public void OnBodyEntered(Node body) {
        GD.Print("Body entered portal");

        if (body is RigidBody3D clone) {
            if (clone.Freeze) {
                return;
            }
        }

        if (GameManager.BluePortal.clones.ContainsKey(body) || GameManager.OrangePortal.clones.ContainsKey(body)) {
            return;
        }

        GD.Print("Body entered portal22");

        // there's definitely a better way to do this, but can't figure out a way to make c# happy

        if (body is Player player) {
            if (player.IgnorePortal == this) {
                return;
            }

            if (portalColor == PortalColor.Blue) {
                TeleportNode(player, GameManager.OrangePortal);
                //CreateCopy(player, GameManager.OrangePortal);
            }
            else {
                TeleportNode(player, GameManager.BluePortal);
                //CreateCopy(player, GameManager.BluePortal);
            }
        }
        else if (body is TeleportableRigidBody3D rigidBody) {
            if (rigidBody.IgnorePortal == this) {
                return;
            }

            if (portalColor == PortalColor.Blue) {
                TeleportNode(rigidBody, GameManager.OrangePortal);
                //CreateCopy(rigidBody, GameManager.OrangePortal);
            }
            else {
                TeleportNode(rigidBody, GameManager.BluePortal);
                //CreateCopy(rigidBody, GameManager.BluePortal);
            }
        }
    }

    public void OnBodyExited(Node body) {
        GD.Print("Body exited portal");

        if (body is Player player) {
            player.IgnorePortal = null;
        }
        else if (body is TeleportableRigidBody3D rigidBody) {
            rigidBody.IgnorePortal = null;
        }

        if (clones.ContainsKey(body)) {
            clones[body].QueueFree();
            clones.Remove(body);
        }
    }

    private void TeleportNode(Node body, Portal targetPortal) {
        // common position and rotation calculations
        Vector3 positionOffset = targetPortal.GlobalPosition - GlobalPosition;
        float rotationOffset = targetPortal.GlobalRotation.Y - GlobalRotation.Y;

        if (body is Player player) {
            // rotation
            player.RotateY(rotationOffset);
            player.RotateY(Mathf.DegToRad(180));

            // position
            player.GlobalPosition += positionOffset;

            player.IgnorePortal = targetPortal;
        }
        else if (body is TeleportableRigidBody3D rigidBody) {
            // rotation
            rigidBody.RotateY(rotationOffset);
            rigidBody.RotateY(Mathf.DegToRad(180));

            // position
            rigidBody.GlobalPosition += positionOffset;

            rigidBody.IgnorePortal = targetPortal;
        }
    }

    public bool InFrontOfPortal(Node3D portal, Transform3D pos) {
        var portal_pos = portal.GlobalTransform;
        return (portal_pos * pos.Origin).Z < 0;
    }

    public Camera3D GetCamera() {
        if (Engine.IsEditorHint()) {
            return EditorInterface.Singleton.GetEditorViewport3D().GetCamera3D();
        }
        else {
            return GetViewport().GetCamera3D();
        }
    }

    // finds the closest wall to the portal (too much code for what it's worth, but it works)

    public StaticBody3D FindAdjacentWall() {
        StaticBody3D closestStaticBody = null;
        float closestDistance = float.MaxValue;

        // Recursively find all StaticBody3D nodes in the scene
        foreach (StaticBody3D staticBody in FindWall(GetTree().Root)) {
            float distance = GlobalPosition.DistanceTo(staticBody.GlobalPosition);
            if (distance < closestDistance) {
                closestDistance = distance;
                closestStaticBody = staticBody;
            }
        }

        return closestStaticBody;
    }

    private System.Collections.Generic.IEnumerable<StaticBody3D> FindWall(Node node) {
        // If the node is a StaticBody3D, yield it
        if (node is StaticBody3D staticBody) {
            yield return staticBody;
        }

        // Recursively find StaticBody3D nodes in the children
        foreach (Node child in node.GetChildren()) {
            foreach (var childStaticBody in FindWall(child)) {
                if (childStaticBody.Name == "Floor") continue; // ignore the floor
                yield return childStaticBody;
            }
        }
    }
}
