using OsmTool;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, OSM!");
        var reader = new OsmReader() {
            Uri = "map.osm"
        };
        //var reader = new OsmPbfReader() {
        //    Uri = "england.osm.pbf"
        //};
        var c = reader.IterateNodes().Count();
        Console.WriteLine($"Node count: {c}");

        var store = new SqliteStore(reader);
        
        Console.WriteLine("Saving nodes...");
        store.SaveNodes();
        Console.WriteLine("Saving ways...");
        store.SaveWays();
        Console.WriteLine("Complete");
    }
}
