namespace MapBoy.Models;

public enum FurnitureType
{
    TrafficLight,
    StreetLamp,
    Bench,
    Bin,
    Tree
}

public class Furniture
{

    public long Id { get; set; }

    public long Uid { get; set; }

    public long TileId { get; set; }

    public double Lat { get; set; }
    public double Lon { get; set; }

    public FurnitureType FurnitureType { get; set; }
}