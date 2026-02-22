using Godot;

public abstract class Controller
{
    public abstract void Control(Camera3D camera, Node3D camera_collection_node, float delta, Viewport viewport);

    public virtual void HandleInput(InputEvent inputEvent) { }

    public virtual bool IsStreetController { get { return false; } }

    public virtual bool IsSatelliteController { get { return false; } }

    internal Vector3 Vector3WithYMod(Vector3 vector3, double yMod)
    {
        return new Vector3(vector3.X, vector3.Y + yMod, vector3.Z);
    }

    internal Vector3 Vector3WithX(Vector3 vector3, double x)
    {
        return new Vector3(x, vector3.Y, vector3.Z);
    }
}
