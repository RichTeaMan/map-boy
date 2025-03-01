
using OsmTool;

public class HighwayBuilderService
{
    public IEnumerable<Coord> CalcHighwayCoordinates(Coord start, Coord end, double width)
    {
        double angle = Math.Atan2(end.Lon - start.Lon, end.Lat - start.Lat);

        var lat_diff = width * Math.Cos(angle + (Math.PI / 2.0));
        var lon_diff = width * Math.Sin(angle + (Math.PI / 2.0));

        yield return new Coord { Lat = start.Lat - lat_diff, Lon = start.Lon - lon_diff };
        yield return new Coord { Lat = end.Lat - lat_diff, Lon = end.Lon - lon_diff };

        yield return new Coord { Lat = start.Lat + lat_diff, Lon = start.Lon + lon_diff };
        yield return new Coord { Lat = end.Lat + lat_diff, Lon = end.Lon + lon_diff };
    }

    public IEnumerable<Coord> CalcHighwayCoordinates(IEnumerable<Coord> coords, double width)
    {
        if (coords.Count() < 2)
        {
            throw new ArgumentException("At least two coordinates are required to calculate a highway");
        }
        var backCoords = new List<Coord>();
        var current = coords.First();

        bool firstLoop = true;

        foreach (var next in coords.Skip(1))
        {
            var stepCoords = CalcHighwayCoordinates(current, next, width).ToArray();
            if (firstLoop)
            {
                yield return stepCoords[0];
                backCoords.Add(stepCoords[2]);
            }
            yield return stepCoords[1];
            backCoords.Add(stepCoords[3]);

            firstLoop = false;
            current = next;
        }

        foreach (var coord in backCoords.Reverse<Coord>())
        {
           yield return coord;
        }
    }
}
