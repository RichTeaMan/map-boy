using System.Collections.ObjectModel;

namespace OsmTool.Models;

public class OsmBase
{
    public long Id { get; init; }

    public bool? Visible { get; init; }
    public int? Version { get; init; }
    public long? ChangeSet { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? User { get; init; }
    public long? Uid { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

}

/// <summary>
/// https://wiki.openstreetmap.org/wiki/Node
/// </summary>
public class OsmNode : OsmBase
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    public bool LocationEquals(OsmNode other){
        return Lat == other.Lat && Lon == other.Lon;
    }

}

public class OsmWay : OsmBase
{
    public required ReadOnlyCollection<long> NodeReferences { get; init; }

    public bool ClosedLoop { get; init; }
}
