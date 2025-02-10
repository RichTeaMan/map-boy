using OsmSharp.Streams;
using OsmTool.Models;

namespace OsmTool;

public class OsmPbfReader : IReader
{

    public string Uri { get; set; }

    public IEnumerable<OsmNode> IterateNodes()
    {
        using var fileStream = File.OpenRead(Uri);
        using var source = new PBFOsmStreamSource(fileStream);

        foreach (var osmGeo in source.Where(o => o.Type == OsmSharp.OsmGeoType.Node))
        {
            var osmNode = (OsmSharp.Node)osmGeo;
            yield return new OsmNode
            {
                Id = osmGeo.Id!.Value,
                Visible = osmGeo.Visible,
                Version = osmGeo.Version,
                ChangeSet = osmGeo.ChangeSetId,
                Timestamp = osmGeo.TimeStamp,
                User = osmGeo.UserName,
                Uid = osmGeo.UserId,
                Lat = osmNode.Latitude!.Value,
                Lon = osmNode.Longitude!.Value,
                Tags = osmNode.Tags.ToDictionary(t => t.Key, t => t.Value)
            };
        }
    }

    public IEnumerable<OsmWay> IterateWays()
    {
        using var fileStream = File.OpenRead(Uri);
        using var source = new PBFOsmStreamSource(fileStream);

        foreach (var osmGeo in source.Where(o => o.Type == OsmSharp.OsmGeoType.Way))
        {
            var osmWay = (OsmSharp.Way)osmGeo;
            yield return new OsmWay
            {
                Id = osmGeo.Id!.Value,
                Visible = osmGeo.Visible,
                Version = osmGeo.Version,
                ChangeSet = osmGeo.ChangeSetId,
                Timestamp = osmGeo.TimeStamp,
                User = osmGeo.UserName,
                Uid = osmGeo.UserId,
                NodeReferences = osmWay.Nodes.AsReadOnly(),
                Tags = osmWay.Tags.ToDictionary(t => t.Key, t => t.Value)
            };
        }
    }

}