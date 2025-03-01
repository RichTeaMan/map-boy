using OsmTool;

public class Program
{
    public static void Main(string[] args)
    {
        var startTime = DateTimeOffset.Now;
        string datafile = "datasets/westminster.osm";
        if (args.Length > 0)
        {
            datafile = args[0];
        }
        else
        {
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
        else
        {
            Console.WriteLine("Unknown file format");
            return;
        }
        string dbPath = (datafile + ".db").Split("/").Last();
        File.Delete(dbPath);
        var service = new OsmService(new SqliteStore(dbPath));
        service.BuildDatabase(reader);

        var endTime = DateTimeOffset.Now;
        var duration = endTime - startTime;
        Console.WriteLine($"Complete in {duration.TotalMinutes} minutes.");
    }
}
