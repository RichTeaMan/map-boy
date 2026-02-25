using System.Dynamic;
using Godot;

public partial class Global : Node
{

    public readonly double coord_factor = 40_075_000.0 / 360.0;

    public bool mouse_captured { get; private set; } = false;

    [Signal]
    public delegate void TeleportEventHandler(double lat, double lon);

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Input.IsActionJustPressed("ui_teleport"))
        {
            var teleport_ui = GD.Load<PackedScene>("res://ui/modals/teleport_modal.tscn").Instantiate();
            AddChild(teleport_ui);
        }
    }

    public void doTeleport(double lat, double lon)
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
        //#var r = Vector2(lat, lon) * coord_factor
        return r;
    }

    public Vector2 vector_to_lat_lon(Vector2 v)
    {
        var lat = v.X / 111320.0;
        var lon_length = 40_075_000.0 * Mathf.Cos(lat / 180.0 * Mathf.Pi) / 360.0;
        return new Vector2(lat, v.Y / lon_length);
        //return Vector2(v.x / Global.coord_factor, v.y / Global.coord_factor)
    }

    public Vector3 lat_lon_to_vector3(double lat, double height, double lon)
    {
        var v2 = LatLonToVector(lat, lon);
        return new Vector3(v2.X, height, v2.Y);
    }

    public void capture_mouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        mouse_captured = true;
    }

    public void release_mouse()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        mouse_captured = false;
    }
}