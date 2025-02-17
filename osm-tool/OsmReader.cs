using System.Xml;
using OsmTool.Models;

namespace OsmTool;

public class OsmReader : IReader
{
    public string Uri { get; set; }

    public IEnumerable<OsmNode> IterateNodes()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
            {
                long id = long.Parse(reader.GetAttribute("id")!);
                double lat = double.Parse(reader.GetAttribute("lat")!);
                double lon = double.Parse(reader.GetAttribute("lon")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                int? version = reader.GetAttributeNullableValue<int>("version");
                long? changeSet = reader.GetAttributeNullableValue<long>("changeset");
                var timeStampValue = reader.GetAttribute("timestamp");
                DateTimeOffset? timestamp = null;
                if (timeStampValue != null)
                {
                    timestamp = DateTimeOffset.Parse(timeStampValue);
                }
                string? user = reader.GetAttribute("user");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();


                var depth = reader.Depth;
                while (reader.NodeType != XmlNodeType.EndElement && reader.Depth != depth)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                    {
                        string? key = reader.GetAttribute("k");
                        string? value = reader.GetAttribute("v");
                        if (key != null && value != null)
                        {
                            tagDict[key] = value;
                        }
                    }

                    reader.Read();
                }

                yield return new OsmNode
                {
                    Id = id,
                    Visible = visible,
                    Version = version,
                    ChangeSet = changeSet,
                    Timestamp = timestamp,
                    User = user,
                    Uid = uid,
                    Lat = lat,
                    Lon = lon,
                    Tags = tagDict
                };
            }
        }
        while (reader.Read());

    }

    public IEnumerable<OsmWay> IterateWays()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
            {
                long id = long.Parse(reader.GetAttribute("id")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                int? version = reader.GetAttributeNullableValue<int>("version");
                long? changeSet = reader.GetAttributeNullableValue<long>("changeset");
                var timeStampValue = reader.GetAttribute("timestamp");
                DateTimeOffset? timestamp = null;
                if (timeStampValue != null)
                {
                    timestamp = DateTimeOffset.Parse(timeStampValue);
                }
                string? user = reader.GetAttribute("user");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();
                var nodeReferences = new List<long>();

                var depth = reader.Depth;

                while (reader.NodeType != XmlNodeType.EndElement)// && reader.Depth != depth)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                    {
                        string? key = reader.GetAttribute("k");
                        string? value = reader.GetAttribute("v");
                        if (key != null && value != null)
                        {
                            tagDict[key] = value;
                        }
                    }
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "nd")
                    {
                        string? nodeRef = reader.GetAttribute("ref");
                        if (nodeRef != null)
                        {
                            nodeReferences.Add(long.Parse(nodeRef));
                        }

                    }
                    reader.Read();
                }
                yield return new OsmWay
                {
                    Id = id,
                    Visible = visible,
                    Version = version,
                    ChangeSet = changeSet,
                    Timestamp = timestamp,
                    User = user,
                    Uid = uid,
                    Tags = tagDict,
                    NodeReferences = nodeReferences.AsReadOnly()
                };
            }
        }
        while (reader.Read());

    }

    public IEnumerable<OsmRelation> IterateRelations()
    {
        using XmlReader reader = XmlReader.Create(Uri);

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "relation")
            {
                long id = long.Parse(reader.GetAttribute("id")!);
                bool? visible = reader.GetAttributeNullableValue<bool>("visible");
                int? version = reader.GetAttributeNullableValue<int>("version");
                long? changeSet = reader.GetAttributeNullableValue<long>("changeset");
                var timeStampValue = reader.GetAttribute("timestamp");
                DateTimeOffset? timestamp = null;
                if (timeStampValue != null)
                {
                    timestamp = DateTimeOffset.Parse(timeStampValue);
                }
                string? user = reader.GetAttribute("user");
                long? uid = reader.GetAttributeNullableValue<long>("uid");
                var tagDict = new Dictionary<string, string>();
                var members = new List<OsmRelationMember>();

                var depth = reader.Depth;

                while (reader.NodeType != XmlNodeType.EndElement)// && reader.Depth != depth)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                    {
                        string? key = reader.GetAttribute("k");
                        string? value = reader.GetAttribute("v");
                        if (key != null && value != null)
                        {
                            tagDict[key] = value;
                        }
                    }
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
                    {
                        string? nodeRef = reader.GetAttribute("ref");
                        string role = reader.GetAttribute("role")!;
                        string type = reader.GetAttribute("type")!;
                        members.Add(new OsmRelationMember
                        {
                            Id = long.Parse(nodeRef),
                            Role = role,
                            Type = type
                        });
                    }
                    reader.Read();
                }
                yield return new OsmRelation
                {
                    Id = id,
                    Visible = visible,
                    Version = version,
                    ChangeSet = changeSet,
                    Timestamp = timestamp,
                    User = user,
                    Uid = uid,
                    Tags = tagDict,
                    Members = members.AsReadOnly()
                };
            }
        }
        while (reader.Read());

    }
}
