using Godot;

public class StreetMouseController : Controller
{

    private bool lookDirectionChanged = false;
    private Vector2 lookDirection = Vector2.Zero;

    private double cameraSensitivity = 1.0f;
    private double cameraSensitivityModifier = 1.0f;

    public override void Control(Camera3D camera, Node3D cameraCollectionNode, double delta, Viewport viewport)
    {
        if (lookDirectionChanged)
        {
            //camera.Rotation.Y -= look_direction.X * camera_sens * sens_mod;
            camera.Rotation = Vector3WithYMod(camera.Rotation, lookDirection.X * cameraSensitivity * cameraSensitivityModifier);
            //camera.Rotation.X = Mathf.Clamp(camera.Rotation.X - look_direction.Y * camera_sens * sens_mod, -1.5, 1.5);
            camera.Rotation = Vector3WithX(camera.Rotation, Mathf.Clamp(camera.Rotation.X - lookDirection.Y * cameraSensitivity * cameraSensitivityModifier, -1.5f, 1.5f));
            
            lookDirectionChanged = false;
        }
    }

    public override void HandleInput(InputEvent inputEvent)
    {
        base.HandleInput(inputEvent);
        if (inputEvent is InputEventMouseMotion inputEventMouseMotion)
        {
            lookDirection = inputEventMouseMotion.Relative * -0.001f;
            lookDirectionChanged = true;

            //if mouse_captured: _rotate_camera()
        }
    }

    public override bool IsStreetController => true;

}
