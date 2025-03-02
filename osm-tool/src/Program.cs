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
        string basePath = datafile.Split("/").Last();
        string dbPath = basePath + ".db";
        File.Delete(dbPath);
        var store = new SqliteStore(dbPath);
        // string searchIndexPath = basePath + ".index";
        // ILocationSearch locationSearch = new LuceneLocationSearch(searchIndexPath);
        ILocationSearch locationSearch = store;
        var service = new OsmService(store, locationSearch);
        service.BuildDatabase(reader);

        if (locationSearch is IDisposable disposable) {
            disposable?.Dispose();
        }

        var endTime = DateTimeOffset.Now;
        var duration = endTime - startTime;
        Console.WriteLine($"Complete in {duration.TotalMinutes} minutes.");
    }
}
