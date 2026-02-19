namespace MapBoy.Models;

public record Coord
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    public Coord() { }

    public Coord(double lat, double lon) : this()
    {
        Lat = lat;
        Lon = lon;
    }

    public double DistanceTo(Coord other)
    {
        return Math.Sqrt(Math.Pow(Lat - other.Lat, 2) + Math.Pow(Lon - other.Lon, 2));
    }

    public double DistanceSquaredTo(Coord other)
    {
        return Math.Pow(Lat - other.Lat, 2) + Math.Pow(Lon - other.Lon, 2);
    }

    public bool LocationEquals(Coord other)
    {
        return Lat == other.Lat && Lon == other.Lon;
    }
}
