namespace OsmTool.Models;

public class SearchIndexEntry
{
    public required string Name { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class SearchIndexResult : SearchIndexEntry
{
    public double Rank { get; set; }
}
