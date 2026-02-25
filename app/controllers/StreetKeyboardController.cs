using Godot;

public class StreetKeyboardController : Controller
{

    public override void Control(Camera3D camera, Node3D cameraCollectionNode, double delta, Viewport viewport)
    {
        var player = camera.GlobalTransform.Basis;
        var forward = -player.Z;
        var backward = player.Z;
        var left = -player.X;
        var right = player.X;

        var move_factor = 25.0 * delta;
        var rotate_factor = 1.0 * delta;
        if (Input.IsActionPressed("rotate_left"))
        {
            cameraCollectionNode.RotateY(rotate_factor);
        }
        if (Input.IsActionPressed("rotate_right"))
        {
            cameraCollectionNode.RotateY(-rotate_factor);
        }
        if (Input.IsActionPressed("left"))
        {
            cameraCollectionNode.Position += left * move_factor;
        }
        if (Input.IsActionPressed("right"))
        {
            cameraCollectionNode.Position += right * move_factor;
        }
        if (Input.IsActionPressed("up"))
        {
            cameraCollectionNode.Position += forward * move_factor;
        }
        if (Input.IsActionPressed("down"))
        {
            cameraCollectionNode.Position += backward * move_factor;
        }
        // hack to keep player on the ground plane
        cameraCollectionNode.Position = new Vector3(cameraCollectionNode.Position.X, 0.0, cameraCollectionNode.Position.Z);
    }

    public override bool IsStreetController => true;

}
