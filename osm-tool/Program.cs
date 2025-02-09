using System.Xml.Linq;
using OsmTool;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, OSM!");
        var reader = new OsmReader();

        var c = reader.IterateNodes("map.osm").Count();
        Console.WriteLine($"Node count: {c}");
        Console.WriteLine("Saving nodes...");
        reader.SaveNodes("map.osm");
        Console.WriteLine("Saving ways...");
        reader.SaveWays("map.osm");
        Console.WriteLine("Complete");

    }
}
