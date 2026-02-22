using Godot;

public partial class MapAreaNode : Node3D
{

    public long area_id { get; set; }

    public bool is_large { get; set; } = false;

    public Vector2 min_vert { get; set; } = new Vector2();
    public Vector2 max_vert { get; set; } = new Vector2();
}
