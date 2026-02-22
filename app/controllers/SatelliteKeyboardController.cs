using Godot;

public class SatelliteKeyboardController : Controller
{

    public override void Control(Camera3D camera, Node3D camera_collection_node, float delta, Viewport viewport)
    {
        var player = camera.GlobalTransform.Basis;
        var forward = player.Y;
        var backward = -player.Y;
        var left = -player.X;
        var right = player.X;
        var zoom_factor = 30.0f * delta;
        var move_factor = 50.0f * delta;
        float rotate_factor = (float)(2.5f * delta);
        if (Input.IsActionPressed("rotate_left"))
        {
            camera_collection_node.RotateY(-rotate_factor);
        }
        if (Input.IsActionPressed("rotate_right"))
        {
            camera_collection_node.RotateY(rotate_factor);
        }
        if (Input.IsActionPressed("ui_up"))
        {
            camera.Position = Vector3WithYMod(camera.Position, -zoom_factor);
        }
        if (Input.IsActionPressed("ui_down"))
        {
            camera.Position = Vector3WithYMod(camera.Position, zoom_factor);
        }
        if (Input.IsActionPressed("left"))
        {
            camera_collection_node.Position += left * move_factor;
        }
        if (Input.IsActionPressed("right"))
        {
            camera_collection_node.Position += right * move_factor;
        }
        if (Input.IsActionPressed("up"))
        {
            camera_collection_node.Position += forward * move_factor;
        }
        if (Input.IsActionPressed("down"))
        {
            camera_collection_node.Position += backward * move_factor;
        }
    }

    public override bool IsSatelliteController { get { return true; } }
}
