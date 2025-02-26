
using OsmTool;

public static class CoordUtils
{

    public static Coord[][] CoordsFromString(this string coordsStr)
    {
        if (string.IsNullOrWhiteSpace(coordsStr)) {
            return Array.Empty<Coord[]>();
        }
        var coords = new List<Coord[]>();
        var coordsVectors = coordsStr.Split('|');
        foreach (var coordsVector in coordsVectors)
        {
            var c = coordsVector.Split(';').Select(s => {
                var parts = s.Split(',');
                return new Coord { Lat = double.Parse(parts[0]), Lon = double.Parse(parts[1]) };
            }).ToArray();
            coords.Add(c);
        }
        return coords.ToArray();
    }

    public static string AsString(this Coord[] coords)
    {
        var cs = new Coord[][] { coords };
        return AsString(cs);
    }

    public static string AsString(this Coord[][] coords)
    {
        return string.Join("|", coords.Select(cs =>
            string.Join(";", cs.Select(c => $"{c.Lat},{c.Lon}")))
        );
    }
}