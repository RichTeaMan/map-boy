
using OsmTool;

public static class CoordUtils
{

    public static Coord[][] CoordsFromString(this string coordsStr)
    {
        if (string.IsNullOrWhiteSpace(coordsStr))
        {
            return Array.Empty<Coord[]>();
        }
        var coords = new List<Coord[]>();
        var coordsVectors = coordsStr.Split('|');
        foreach (var coordsVector in coordsVectors)
        {
            var c = coordsVector.Split(';').Select(s =>
            {
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

    public static bool AreaContainsAreas(this IEnumerable<Coord> containerArea, IEnumerable<IEnumerable<Coord>> testAreas)
    {
        var maxLat = containerArea.Max(c => c.Lat);
        var maxLon = containerArea.Max(c => c.Lon);
        var minLat = containerArea.Min(c => c.Lat);
        var minLon = containerArea.Min(c => c.Lon);

        foreach (var testArea in testAreas)
        {
            var maxTestLat = testArea.Max(c => c.Lat);
            var maxTestLon = testArea.Max(c => c.Lon);
            var minTestLat = testArea.Min(c => c.Lat);
            var minTestLon = testArea.Min(c => c.Lon);

            if (minLat <= minTestLat && maxLat >= maxTestLat &&
            minLon <= minTestLon && maxLon >= maxTestLon)
            {
                return true;
            }
        }
        return false;
    }
}