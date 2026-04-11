using System.Xml;
using OsmSharp.IO.Xml;
using OsmTool.Models;

namespace OsmTool;

public class OsmReader : IReader
{
    public required string Uri { get; init; }

    public IEnumerable<OsmNode> IterateNodes()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        OsmNode? previousNode = null;

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag" && previousNode != null)
            {
                string? key = reader.GetAttribute("k");
                string? value = reader.GetAttribute("v");
                if (key != null && value != null)
                {
                    previousNode.Tags[key] = value;
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
            {
                if (previousNode != null)
                {
                    yield return previousNode;
                }

                long id = long.Parse(reader.GetAttribute("id")!);
                double lat = double.Parse(reader.GetAttribute("lat")!);
                double lon = double.Parse(reader.GetAttribute("lon")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();

                previousNode = new OsmNode
                {
                    Id = id,
                    Visible = visible,
                    Uid = uid,
                    Lat = lat,
                    Lon = lon,
                    Tags = tagDict
                };
            }
        }
        while (reader.Read());

        if (previousNode != null)
        {
            yield return previousNode;
        }
    }

    public IEnumerable<OsmWay> IterateWays()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        OsmWay? previousOsmWay = null;

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag" && previousOsmWay != null)
            {
                string? key = reader.GetAttribute("k");
                string? value = reader.GetAttribute("v");
                if (key != null && value != null)
                {
                    previousOsmWay.Tags[key] = value;
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "nd" && previousOsmWay != null)
            {
                string? nodeRef = reader.GetAttribute("ref");
                if (nodeRef != null)
                {
                    previousOsmWay.NodeReferences.Add(long.Parse(nodeRef));
                }
            }

            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
            {
                if (previousOsmWay != null)
                {
                    yield return previousOsmWay;
                }
                long id = long.Parse(reader.GetAttribute("id")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();
                var nodeReferences = new List<long>();

                previousOsmWay = new OsmWay
                {
                    Id = id,
                    Visible = visible,
                    Uid = uid,
                    Tags = tagDict,
                    NodeReferences = nodeReferences
                };
            }
        }
        while (reader.Read());

        if (previousOsmWay != null)
        {
            yield return previousOsmWay;
        }
    }

    public IEnumerable<OsmRelation> IterateRelations()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        OsmRelation? previousOsmRelation = null;

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag" && previousOsmRelation != null)
            {
                string? key = reader.GetAttribute("k");
                string? value = reader.GetAttribute("v");
                if (key != null && value != null)
                {
                    previousOsmRelation.Tags[key] = value;
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "member" && previousOsmRelation != null)
            {
                string? nodeRef = reader.GetAttribute("ref");
                string? role = reader.GetAttribute("role")?.ToLower();
                string? type = reader.GetAttribute("type")?.ToLower();
                if (nodeRef == null || role == null || type == null)
                {
                    continue;
                }
                previousOsmRelation.Members.Add(new OsmRelationMember
                {
                    Id = long.Parse(nodeRef),
                    Role = role,
                    Type = type
                });
            }

            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "relation")
            {
                if (previousOsmRelation != null)
                {
                    yield return previousOsmRelation;
                }
                long id = long.Parse(reader.GetAttribute("id")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();
                var members = new List<OsmRelationMember>();

                previousOsmRelation = new OsmRelation
                {
                    Id = id,
                    Visible = visible,
                    Uid = uid,
                    Tags = tagDict,
                    Members = members
                };
            }
        }
        while (reader.Read());
        if (previousOsmRelation != null)
        {
            yield return previousOsmRelation;
        }
    }
}
