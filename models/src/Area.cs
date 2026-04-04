namespace MapBoy.Models;

public class Area
{
    public long Id { get; set; }

    public required string Source { get; set; }
    public bool Visible { get; set; }
    public long? Uid { get; set; }
    public required Coord[][] OuterCoordinates { get; set; }
    public required Coord[][] InnerCoordinates { get; set; }
    public required string[] Names { get; set; }
    public required string SuggestedColour { get; set; }
    public long TileId { get; set; }
    public int Layer { get; set; }
    public double Height { get; set; }

    public double MinHeight { get; set; }
    public double RoofHeight { get; set; }
    public required string RoofType { get; set; }
    public required string RoofColour { get; set; }
    public required string RoofOrientation { get; set; }
    public bool IsLarge { get; set; }

    public bool Is3d { get; set; }

}