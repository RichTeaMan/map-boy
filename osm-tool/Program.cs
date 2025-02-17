using OsmTool;

public class Program
{
    public static void Main(string[] args)
    {
        string datafile = "datasets/westminster.osm";
        if (args.Length > 0)
        {
            datafile = args[0];
        }
        else {
            Console.WriteLine("Datafile not specified.");
        }
        Console.WriteLine($"Datafile: {datafile}");
        IReader reader;
        if (datafile.EndsWith(".osm"))
        {
            Console.WriteLine("Using OsmReader");
            reader = new OsmReader()
            {
                Uri = datafile
            };
        }
        else if (datafile.EndsWith(".osm.pbf"))
        {
            Console.WriteLine("Using OsmPbfReader");
            reader = new OsmPbfReader()
            {
                Uri = datafile
            };
        }
        else {
            Console.WriteLine("Unknown file format");
            return;
        }
        Console.WriteLine("Building database...");
        var c = reader.IterateNodes().Count();
        Console.WriteLine($"Node count: {c}");

        var store = new SqliteStore();

        Console.WriteLine("Saving nodes...");
        store.SaveNodes(reader);
        Console.WriteLine("Saving ways...");
        store.SaveWays(reader);
        
        Console.WriteLine("Saving multipolygon relations...");
        store.SaveAreas(reader);
        Console.WriteLine("Complete");
    }
}
