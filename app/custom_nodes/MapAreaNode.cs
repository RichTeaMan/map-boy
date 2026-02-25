using Godot;

public partial class MapAreaNode : Node3D
{

    public long AreaId { get; set; }

    public bool IsLarge { get; set; } = false;

    public Vector2 MinVert { get; set; } = new Vector2();
    public Vector2 MaxVert { get; set; } = new Vector2();
}
