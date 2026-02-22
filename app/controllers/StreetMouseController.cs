using Godot;

public class StreetMouseController : Controller
{

    private bool look_direction_changed = false;
    private Vector2 look_direction = Vector2.Zero;

    private double camera_sens = 1.0f;
    private double sens_mod = 1.0f;

    public override void Control(Camera3D camera, Node3D camera_collection_node, float delta, Viewport viewport)
    {
        if (look_direction_changed)
        {
            double t = camera.Rotation.Z;
            //camera.Rotation.Y -= look_direction.X * camera_sens * sens_mod;
            camera.Rotation = Vector3WithYMod(camera.Rotation, look_direction.X * camera_sens * sens_mod);
            //camera.Rotation.X = Mathf.Clamp(camera.Rotation.X - look_direction.Y * camera_sens * sens_mod, -1.5, 1.5);
            camera.Rotation = Vector3WithX(camera.Rotation, Mathf.Clamp(camera.Rotation.X - look_direction.Y * camera_sens * sens_mod, -1.5f, 1.5f));
            
            look_direction_changed = false;
        }
    }

    public override void HandleInput(InputEvent inputEvent)
    {
        base.HandleInput(inputEvent);
        if (inputEvent is InputEventMouseMotion inputEventMouseMotion)
        {
            look_direction = inputEventMouseMotion.Relative * 0.001f;
            look_direction_changed = true;

            //if mouse_captured: _rotate_camera()
        }
    }

    public override bool IsStreetController => true;

}