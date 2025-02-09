using System.Collections.ObjectModel;
using System.Xml;
using Microsoft.Data.Sqlite;

namespace OsmTool;

public class OsmBase
{
    public long Id { get; init; }

    public bool Visible { get; init; }
    public int Version { get; init; }
    public long ChangeSet { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string User { get; init; }
    public long Uid { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

}

public class OsmNode : OsmBase
{
    public double Lat { get; set; }
    public double Lon { get; set; }

}

public class OsmWay : OsmBase
{
    public required ReadOnlyCollection<long> NodeReferences { get; init; }
}

public class OsmReader
{

    public IEnumerable<OsmNode> IterateNodes(string uri)
    {
        using XmlReader reader = XmlReader.Create(uri);

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
            {
                long id = long.Parse(reader.GetAttribute("id")!);
                Console.WriteLine(id);
                double lat = double.Parse(reader.GetAttribute("lat")!);
                double lon = double.Parse(reader.GetAttribute("lon")!);
                bool visible = bool.Parse(reader.GetAttribute("visible")!);
                int version = int.Parse(reader.GetAttribute("version")!);
                long changeSet = long.Parse(reader.GetAttribute("changeset")!);
                DateTimeOffset timestamp = DateTimeOffset.Parse(reader.GetAttribute("timestamp")!);
                string user = reader.GetAttribute("user")!;
                long uid = long.Parse(reader.GetAttribute("uid")!);
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

    public IEnumerable<OsmWay> IterateWays(string uri)
    {
        using XmlReader reader = XmlReader.Create(uri);

        reader.MoveToContent();
        do
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
            {
                long id = long.Parse(reader.GetAttribute("id")!);
                bool visible = bool.Parse(reader.GetAttribute("visible")!);
                int version = int.Parse(reader.GetAttribute("version")!);
                long changeSet = long.Parse(reader.GetAttribute("changeset")!);
                DateTimeOffset timestamp = DateTimeOffset.Parse(reader.GetAttribute("timestamp")!);
                string user = reader.GetAttribute("user")!;
                long uid = long.Parse(reader.GetAttribute("uid")!);
                var tagDict = new Dictionary<string, string>();
                var nodeReferences = new List<long>();

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
                    else if (reader.NodeType == XmlNodeType.Element && reader.Name == "nd")
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

    public void SaveNodes(string uri)
    {

        using var connection = new SqliteConnection("Data Source=osm.db");
        connection.Open();


        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS node (
            id INTEGER PRIMARY KEY,
            visible INTEGER NOT NULL,
            version INTEGER NOT NULL,
            change_set INTEGER NOT NULL,
            timestamp TEXT NOT NULL,
            user TEXT NOT NULL,
            uid INTEGER NOT NULL,
            lat REAL NOT NULL,
            lon REAL NOT NULL
        );
        ";

        createTableCommand.ExecuteNonQuery();

        var nodeBatch = new List<OsmNode>();
        var nodes = IterateNodes(uri);
        foreach (var node in nodes)
        {
            nodeBatch.Add(node);
            if (nodeBatch.Count() > 1000)
            {
                SaveNodeBatch(connection, nodeBatch);
                nodeBatch.Clear();
            }
        }

        if (nodeBatch.Any())
        {
            SaveNodeBatch(connection, nodeBatch);
        }
    }

    private static void SaveNodeBatch(SqliteConnection connection, List<OsmNode> nodeBatch)
    {
        using var transaction = connection.BeginTransaction();
        foreach (var node in nodeBatch)
        {
            using var insertNodeCommand = connection.CreateCommand();
            insertNodeCommand.CommandText = @"
                INSERT INTO node (id, visible, version, change_set, timestamp, user, uid, lat, lon)
                    VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $lat, $lon);
                ";
            insertNodeCommand.Parameters.AddWithValue("$id", node.Id);
            insertNodeCommand.Parameters.AddWithValue("$visible", node.Visible);
            insertNodeCommand.Parameters.AddWithValue("$version", node.Version);
            insertNodeCommand.Parameters.AddWithValue("$change_set", node.ChangeSet);
            insertNodeCommand.Parameters.AddWithValue("$timestamp", node.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            insertNodeCommand.Parameters.AddWithValue("$user", node.User);
            insertNodeCommand.Parameters.AddWithValue("$uid", node.Uid);
            insertNodeCommand.Parameters.AddWithValue("$lat", node.Lat);
            insertNodeCommand.Parameters.AddWithValue("$lon", node.Lon);
            insertNodeCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private Dictionary<long, OsmNode> FetchByIds(long[] ids)
    {
        var result = new Dictionary<long, OsmNode>();
        using var connection = new SqliteConnection("Data Source=osm.db");
        connection.Open();

        using var command = connection.CreateCommand();
        var q = string.Join(',', ids);
        // I gave up making this parametered. nothing works
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, lat, lon FROM node WHERE id IN ($ids);".Replace("$ids", q);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetInt64(0)] = new OsmNode
            {
                Id = reader.GetInt64(0),
                Visible = reader.GetBoolean(1),
                Version = reader.GetInt32(2),
                ChangeSet = reader.GetInt64(3),
                Timestamp = DateTimeOffset.Parse(reader.GetString(4)),
                User = reader.GetString(5),
                Uid = reader.GetInt64(6),
                Lat = reader.GetDouble(7),
                Lon = reader.GetDouble(8)
            };
        }
        return result;
    }

    public void SaveWays(string uri)
    {

        using var connection = new SqliteConnection("Data Source=osm.db");
        connection.Open();


        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS way (
            id INTEGER PRIMARY KEY,
            visible INTEGER NOT NULL,
            version INTEGER NOT NULL,
            change_set INTEGER NOT NULL,
            timestamp TEXT NOT NULL,
            user TEXT NOT NULL,
            uid INTEGER NOT NULL,
            coords TEXT NOT NULL,
            name TEXT NULL
        );
        ";

        createTableCommand.ExecuteNonQuery();

        var wayBatch = new List<OsmWay>();
        var ways = IterateWays(uri);
        foreach (var osmWay in ways)
        {
            wayBatch.Add(osmWay);
            if (wayBatch.Count >= 100)
            {
                SaveWayBatch(connection, wayBatch);
                wayBatch.Clear();
            }
        }

        if (wayBatch.Any())
        {
            SaveWayBatch(connection, wayBatch);
        }
    }

    private void SaveWayBatch(SqliteConnection connection, List<OsmWay> wayBatch)
    {
        var nodeIds = wayBatch.SelectMany(w => w.NodeReferences).Distinct().ToArray();
        var nodes = FetchByIds(nodeIds);
        using var transaction = connection.BeginTransaction();
        foreach (var way in wayBatch)
        {
            var coords = string.Join(";", way.NodeReferences.Select(id => $"{nodes[id].Lat},{nodes[id].Lon}"));

            using var insertWayCommand = connection.CreateCommand();
            insertWayCommand.CommandText = @"
                    INSERT INTO way (id, visible, version, change_set, timestamp, user, uid, coords, name)
                        VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $coords, $name);
                    ";
            insertWayCommand.Parameters.AddWithValue("$id", way.Id);
            insertWayCommand.Parameters.AddWithValue("$visible", way.Visible);
            insertWayCommand.Parameters.AddWithValue("$version", way.Version);
            insertWayCommand.Parameters.AddWithValue("$change_set", way.ChangeSet);
            insertWayCommand.Parameters.AddWithValue("$timestamp", way.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            insertWayCommand.Parameters.AddWithValue("$user", way.User);
            insertWayCommand.Parameters.AddWithValue("$uid", way.Uid);
            insertWayCommand.Parameters.AddWithValue("$coords", coords);
            insertWayCommand.Parameters.AddWithValue("$name", way.Tags.ContainsKey("name") ? way.Tags["name"] : DBNull.Value);
            insertWayCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}