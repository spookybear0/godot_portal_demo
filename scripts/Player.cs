using Godot;
using System;

public partial class Player : CharacterBody3D {
    public float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;

    public Portal IgnorePortal { get; set; }

    public Camera3D camera;

    public Vector3 BluePortalPosition { get; set; }
    public Vector3 OrangePortalPosition { get; set; }

    [Export]
    public float pushForce = 5;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready() {
        // Hide the mouse cursor.
        Input.MouseMode = Input.MouseModeEnum.Captured;

        camera = GetNode<Camera3D>("Camera3D");
    }

    public override void _UnhandledInput(InputEvent @event) {
        // Handle the mouse input.
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured) {
            // Rotate the player body.
            RotateY(Mathf.DegToRad(-mouseMotion.Relative.X * 0.1f));

            // Rotate the camera.
            camera.RotateX(Mathf.DegToRad(-mouseMotion.Relative.Y * 0.1f));
            camera.RotationDegrees = new Vector3(
                Mathf.Clamp(camera.RotationDegrees.X, -80, 80),
                camera.RotationDegrees.Y,
                camera.RotationDegrees.Z
            );
        }

        // shoot

        if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed) {
            // use raycast to get where was shot

            var from = camera.ProjectRayOrigin(eventMouseButton.Position);
            var to = from + camera.ProjectRayNormal(eventMouseButton.Position) * 1000;

            PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
            var result = spaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));

            if (result.Count > 0) {
                Vector3 hitPosition = (Vector3)result["position"];
                Vector3 hitNormal = (Vector3)result["normal"];

                if (eventMouseButton.ButtonIndex == MouseButton.Left) {
                    GD.Print("shoot blue portal");
                    PlacePortal(GameManager.BluePortal, hitPosition, hitNormal);
                } else if (eventMouseButton.ButtonIndex == MouseButton.Right) {
                    GD.Print("shoot orange portal");
                    PlacePortal(GameManager.OrangePortal, hitPosition, hitNormal);
                }
            }
        }

        if (Input.IsActionPressed("sprint")) {
            Speed = 7.5f;
        }
        else {
            Speed = 5.0f;
        }
    }

    private void PlacePortal(Portal portal, Vector3 position, Vector3 normal) {
        // slightly offset the portal from the wall
        Vector3 offsetPosition = position + normal * 0.1f; 
        portal.GlobalPosition = offsetPosition;

        // face right direction
        Vector3 up = Vector3.Up;
        if (Mathf.Abs(normal.Dot(up)) > 0.99) {
            up = Vector3.Forward;
        }

        Vector3 tangent = up.Cross(normal).Normalized();
        Vector3 bitangent = normal.Cross(tangent);

        Basis basis = new Basis(tangent, bitangent, normal);
        portal.GlobalTransform = new Transform3D(basis, offsetPosition);
    }

    public override void _PhysicsProcess(double delta) {
        Vector3 velocity = Velocity;

        // Add the gravity.
        if (!IsOnFloor())
            velocity.Y -= gravity * (float)delta;

        // Handle Jump.
        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // Get the input direction and handle the movement/deceleration.
        // As good practice, you should replace UI actions with custom gameplay actions.
        Vector2 inputDir = Input.GetVector("left", "right", "forward", "backward");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        if (direction != Vector3.Zero) {
            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
        }

        Velocity = velocity;
        MoveAndSlide();


        // move rigidbodies

        for (int i = 0; i < GetSlideCollisionCount(); i++) {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is RigidBody3D body) {
                body.ApplyCentralImpulse(-collision.GetNormal() * pushForce);
            }
        }
    }
}
