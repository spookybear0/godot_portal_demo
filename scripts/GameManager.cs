using Godot;
using System;


public partial class GameManager : Node3D {
    public static Portal BluePortal { get; set; }
    public static Portal OrangePortal { get; set; }

    public override void _Process(double delta) {
        if (Input.IsActionJustPressed("fullscreen")) {
            ToggleFullscreen();
        }
    }

    public static void ToggleFullscreen() {
        if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed) {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
    }
}
