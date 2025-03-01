namespace OsmTool.Models;

public class SearchIndexResult
{
    public required string Name { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Rank { get; set; }
}
