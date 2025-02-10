using System.Data;
using Microsoft.Data.Sqlite;
using OsmTool.Models;

namespace OsmTool;

public record Coord
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class Way
{
    public long Id { get; set; }
    public bool? Visible { get; set; }
    public int? Version { get; set; }
    public long? ChangeSet { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string? User { get; set; }
    public long? Uid { get; set; }
    public List<Coord> Coordinates { get; set; }
    public string? Name { get; set; }
}

public class SqliteStore
{
    private SqliteConnection createConnection()
    {
        return new SqliteConnection("Data Source=/home/tom/projects/map-boy/osm-tool/osm.db");
    }

    public void SaveNodes(IReader reader)
    {

        using var connection = createConnection();
        connection.Open();


        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS node (
            id INTEGER PRIMARY KEY,
            visible INTEGER NULL,
            version INTEGER NULL,
            change_set INTEGER NULL,
            timestamp TEXT NULL,
            user TEXT NULL,
            uid INTEGER NULL,
            lat REAL NOT NULL,
            lon REAL NOT NULL
        );
        ";

        createTableCommand.ExecuteNonQuery();

        var nodeBatch = new List<OsmNode>();
        var nodes = reader.IterateNodes();
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
            insertNodeCommand.Parameters.AddWithValue("$visible", node.Visible as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$version", node.Version as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$change_set", node.ChangeSet as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$timestamp", node.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$user", node.User as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$uid", node.Uid as object ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("$lat", node.Lat);
            insertNodeCommand.Parameters.AddWithValue("$lon", node.Lon);
            insertNodeCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private Dictionary<long, OsmNode> FetchByIds(long[] ids)
    {
        var result = new Dictionary<long, OsmNode>();
        using var connection = createConnection();
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

    public void SaveWays(IReader reader)
    {
        using var connection = createConnection();
        connection.Open();

        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS way (
            id INTEGER PRIMARY KEY,
            visible INTEGER NULL,
            version INTEGER NULL,
            change_set INTEGER NULL,
            timestamp TEXT NULL,
            user TEXT NOT NULL,
            uid INTEGER NULL,
            coords TEXT NOT NULL,
            name TEXT NULL
        );
        ";

        createTableCommand.ExecuteNonQuery();

        var wayBatch = new List<OsmWay>();
        var ways = reader.IterateWays();
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
        int noCoord = 0;
        int wayTotal = 0;
        foreach (var way in wayBatch)
        {
            var coords = string.Join(";", way.NodeReferences.Select(id => $"{nodes[id].Lat},{nodes[id].Lon}"));
            if (way.NodeReferences.Count == 0)
            {
                noCoord++;
            }
            wayTotal++;
            using var insertWayCommand = connection.CreateCommand();
            insertWayCommand.CommandText = @"
                    INSERT INTO way (id, visible, version, change_set, timestamp, user, uid, coords, name)
                        VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $coords, $name);
                    ";
            insertWayCommand.Parameters.AddWithValue("$id", way.Id);
            insertWayCommand.Parameters.AddWithValue("$visible", way.Visible as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$version", way.Version as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$change_set", way.ChangeSet);
            insertWayCommand.Parameters.AddWithValue("$timestamp", way.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$user", way.User as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$uid", way.Uid as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$coords", coords);
            insertWayCommand.Parameters.AddWithValue("$name", way.Tags.ContainsKey("name") ? way.Tags["name"] : DBNull.Value);
            insertWayCommand.ExecuteNonQuery();
        }
        transaction.Commit();
        Console.WriteLine($"{noCoord}/{wayTotal}");
    }

    public IEnumerable<Way> FetchWays()
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, coords, name FROM way;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Way
            {
                Id = reader.GetInt64(0),
                Visible = reader.GetBoolean(1),
                Version = reader.GetInt32(2),
                ChangeSet = reader.GetInt64(3),
                Timestamp = DateTimeOffset.Parse(reader.GetString(4)),
                User = reader.GetString(5),
                Uid = reader.GetInt64(6),
                Coordinates = reader.GetString(7).Split(';').Select(s =>
                {
                    var coords = s.Split(',');
                    return new Coord { Lat = double.Parse(coords[0]), Lon = double.Parse(coords[1]) };
                }).ToList(),
                Name = reader.NullableString(8)
            };
        }
    }

}