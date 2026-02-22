using Godot;

public class ControlScheme
{
    public Camera3D Camera { get; set; }
    public Controller[] Controllers { get; set; } = [];
    public bool LocksMouse { get; set; }
}
