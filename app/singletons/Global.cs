using Godot;

public partial class Global : Node
{

    public readonly double coordFactor = 40_075_000.0 / 360.0;

    public bool MouseCaptured { get; private set; } = false;

    public bool IsTextFocused { get; private set; } = false;

    private bool captureMouseAfterTestFocused = false;

    [Signal]
    public delegate void TeleportEventHandler(double lat, double lon);

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Input.IsActionJustPressed("ui_teleport"))
        {
            var teleportUi = GD.Load<PackedScene>("res://ui/modals/teleport_modal.tscn").Instantiate();
            AddChild(teleportUi);
        }

        if (Input.IsActionPressed("quit"))
        {
            GetTree().Quit();
        }
    }

    public void DoTeleport(double lat, double lon)
    {
        EmitSignal(SignalName.Teleport, lat, lon);
    }

    public void ConnectTeleport(Callable callable)
    {
        Connect(SignalName.Teleport, callable);
    }

    public static Vector2 LatLonToVector(double lat, double lon)
    {
        var lon_length = 40_075_000.0 * Mathf.Cos(lat / 180.0 * Mathf.Pi) / 360.0;
        var r = new Vector2(lat * 111320, lon * lon_length);
        return r;
    }

    public Vector2 VectorToLatLon(Vector2 v)
    {
        var lat = v.X / 111320.0;
        var lonLength = 40_075_000.0 * Mathf.Cos(lat / 180.0 * Mathf.Pi) / 360.0;
        return new Vector2(lat, v.Y / lonLength);
    }

    public Vector3 LatLonToVector3(double lat, double height, double lon)
    {
        var v2 = LatLonToVector(lat, lon);
        return new Vector3(v2.X, height, v2.Y);
    }

    public void CaptureMouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        MouseCaptured = true;
    }

    public void ReleaseMouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        MouseCaptured = false;
    }

    /// <summary>
    /// Signals that keystrokes should be registered for text entry and not movement.
    /// 
    /// Releases the mouse, if currently captured.
    /// </summary>
    public void TextFocused()
    {
        IsTextFocused = true;
        captureMouseAfterTestFocused = false;
        if (MouseCaptured) {
            captureMouseAfterTestFocused = true;
            ReleaseMouse();
        }
    }

    /// <summary>
    /// Signals that keystrokes should be controlling movement again.
    /// 
    /// Captures the mouse if the mouse was captured previously.
    /// </summary>
    public void TextUnfocused()
    {
        IsTextFocused = false;
        if (captureMouseAfterTestFocused) {
            CaptureMouse();
        }
    }
}
