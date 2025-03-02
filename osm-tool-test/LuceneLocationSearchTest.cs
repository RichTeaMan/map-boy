using OsmTool;
using OsmTool.Models;

public class LuceneLocationSearchTest
{

    [Ignore]
    public void Test()
    {

        var locationSearch = new LuceneLocationSearch("index");
        var searchIndexEntries = new SearchIndexEntry[] { };

        int fails = 0;
        int entries = 0;
        int subEntries = 0;
        int subFails = 0;
        // do some search
        foreach (var entry in searchIndexEntries)
        {
            var res = locationSearch.SearchAreas(entry.Name);
            if (res.Count() == 0)
            {
                fails++;
            }
            entries++;

            var parts = entry.Name.Split(" ");
            if (parts.Length == 0)
            {
                continue;
            }
            foreach (var part in parts)
            {
                var subres = locationSearch.SearchAreas(part);
                if (subres.Count() == 0)
                {
                    subFails++;
                }
                subEntries++;
            }
        }
        Console.WriteLine($"!!!!!!!!! FAILS: {fails}/{entries}");
        Console.WriteLine($"!!!!!!!SUBFAILS: {subFails}/{subEntries}");
    }

}