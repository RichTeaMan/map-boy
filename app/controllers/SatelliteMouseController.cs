using Godot;

public class SatelliteMouseController : Controller
{

    private bool dragEnabled = false;
    private Vector3 drag_point = Vector3.Zero;

    public override void Control(Camera3D camera, Node3D cameraCollectionNode, double delta, Viewport viewport)
    {

        var zoom_factor = 300.0 * delta;
        if (Input.IsActionJustReleased("mouse_wheel_up"))
        {
            camera.Position = Vector3WithYMod(camera.Position, -zoom_factor);
        }
        if (Input.IsActionJustReleased("mouse_wheel_down"))
        {
            camera.Position = Vector3WithYMod(camera.Position, zoom_factor);
        }
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mouse_position = viewport.GetMousePosition();
            var mouse_position_3d = camera.ProjectPosition(mouse_position, camera.GlobalPosition.Y);

            if (dragEnabled)
            {
                var drag_delta = mouse_position_3d - drag_point;
                cameraCollectionNode.Position -= drag_delta;
            }
            else
            {
                dragEnabled = true;
                drag_point = mouse_position_3d;
            }
        }
        else
        {
            dragEnabled = false;
        }
    }


    public override bool IsSatelliteController => true;
}