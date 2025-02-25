
using OsmTool;

public class HighwayBuilderService
{
    public IEnumerable<Coord> CalcHighwayCoordinates(Coord start, Coord end, double width)
    {
        // calculate normal angle between start and end
        double angle = Math.Atan2(end.Lon - start.Lon, end.Lat - start.Lat);
        //Console.WriteLine($"Angle: { angle * (180 / Math.PI)}");

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
        Coord? first = null;
        var current = coords.First();

        //CalcHighwayCoordinates(start, )
        //var stepCoords = CalcHighwayCoordinates(coords[0], coords[1], width).ToArray();

        bool firstLoop = true;

        foreach (var next in coords.Skip(1))
        {
            var stepCoords = CalcHighwayCoordinates(current, next, width).ToArray();
            backCoords.Add(stepCoords[2]);
            if (firstLoop)
            {
                first = stepCoords[0];
                yield return stepCoords[0];
                backCoords.Add(stepCoords[3]);
            }
            yield return stepCoords[1];

            firstLoop = false;
            current = next;
        }

        foreach (var coord in backCoords.Reverse<Coord>())
        {
            yield return coord;
        }
        yield return first!;
    }
}
