using System.Data;
using Microsoft.Data.Sqlite;
using OsmTool.Models;

namespace OsmTool;

public record Coord
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    public double DistanceTo(Coord other)
    {
        return Math.Sqrt(Math.Pow(Lat - other.Lat, 2) + Math.Pow(Lon - other.Lon, 2));
    }

    public double DistanceSquaredTo(Coord other)
    {
        return Math.Pow(Lat - other.Lat, 2) + Math.Pow(Lon - other.Lon, 2);
    }

    public bool LocationEquals(Coord other)
    {
        return Lat == other.Lat && Lon == other.Lon;
    }
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
    public bool ClosedLoop { get; set; }
    public string SuggestedColour { get; set; }
}

public class Area
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
    public string SuggestedColour { get; set; }
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
            name TEXT NULL,
            closed_loop INTEGER NOT NULL,
            suggested_colour TEXT NOT NULL
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

    private string calcSuggestedColour(OsmBase osmBase)
    {
        if (osmBase.Tags.ContainsKey("highway"))
        {
            switch (osmBase.Tags["highway"])
            {
                case "motorway":
                case "trunk":
                case "primary":
                case "secondary":
                case "tertiary":
                case "unclassified":
                case "residential":
                case "service":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                case "living_street":
                case "pedestrian":
                case "track":
                case "bus_guideway":
                case "escape":
                case "raceway":
                case "road":
                case "footway":
                case "bridleway":
                case "steps":
                case "path":
                case "cycleway":
                case "proposed":
                case "construction":
                case "bus_stop":
                case "crossing":
                case "elevator":
                case "emergency_access_point":
                case "give_way":
                case "mini_roundabout":
                case "motorway_junction":
                case "passing_place":
                case "rest_area":
                case "speed_camera":
                case "street_lamp":
                case "services":
                case "stop":
                case "traffic_signals":
                case "turning_circle":
                case "turning_loop":
                    return "red";
            }
        }
        if (osmBase.Tags.ContainsKey("waterway"))
        {
            switch (osmBase.Tags["waterway"])
            {
                case "river":
                case "riverbank":
                case "stream":
                case "canal":
                case "drain":
                case "ditch":
                case "weir":
                case "dam":
                case "dock":
                case "boatyard":
                case "lock_gate":
                case "waterfall":
                case "water_point":
                case "water_slide":
                case "water_tap":
                case "water_well":
                case "watermill":
                case "waterhole":
                case "watering_place":
                case "water_works":
                    return "blue";
            }
        }
        if (osmBase.Tags.ContainsKey("water"))
        {
            return "blue";
        }
        if (osmBase.Tags.ContainsKey("building"))
        {
            return "grey";
        }
        if (osmBase.Tags.ContainsKey("leisure"))
        {
            switch (osmBase.Tags["leisure"])
            {
                case "park":
                case "playground":
                case "sports_centre":
                case "stadium":
                case "swimming_pool":
                case "track":
                case "water_park":
                case "wildlife_hide":
                case "fitness_centre":
                case "golf_course":
                case "miniature_golf":
                case "pitch":
                case "recreation_ground":
                case "sauna":
                case "slipway":
                case "marina":
                case "nature_reserve":
                case "garden":
                case "common":
                case "dog_park":
                case "fishing":
                case "horse_riding":
                case "ice_rink":
                    return "green";
            }
        }
        return "white";
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
            bool closedLoop = nodes[way.NodeReferences.First()].LocationEquals(nodes[way.NodeReferences.Last()]);
            wayTotal++;
            using var insertWayCommand = connection.CreateCommand();
            insertWayCommand.CommandText = @"
                    INSERT INTO way (id, visible, version, change_set, timestamp, user, uid, coords, name, closed_loop, suggested_colour)
                        VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $coords, $name, $closed_loop, $suggested_colour);
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
            insertWayCommand.Parameters.AddWithValue("$closed_loop", closedLoop);
            insertWayCommand.Parameters.AddWithValue("$suggested_colour", calcSuggestedColour(way));
            insertWayCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IEnumerable<Way> FetchWays(long[]? ids = null)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, coords, name, closed_loop, suggested_colour FROM way";
        if (ids != null)
        {
            var q = string.Join(',', ids);
            command.CommandText += $" WHERE id IN ({q})";
        }

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
                Name = reader.NullableString(8),
                ClosedLoop = reader.GetBoolean(9),
                SuggestedColour = reader.GetString(10),
            };
        }
    }

    public void SaveAreas(IReader reader)
    {
        using var connection = createConnection();
        connection.Open();

        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS area (
            id INTEGER PRIMARY KEY,
            visible INTEGER NULL,
            version INTEGER NULL,
            change_set INTEGER NULL,
            timestamp TEXT NULL,
            user TEXT NOT NULL,
            uid INTEGER NULL,
            coords TEXT NOT NULL,
            name TEXT NULL,
            suggested_colour TEXT NOT NULL
        );
        ";

        createTableCommand.ExecuteNonQuery();

        var relationBatch = new List<OsmRelation>();
        foreach (var relation in reader.IterateRelations())
        {
            if (relation.Tags.Any(t => t.Key == "type" && t.Value == "multipolygon"))
            {
                relationBatch.Add(relation);
                if (relationBatch.Count >= 100)
                {
                    SaveAreaBatch(connection, relationBatch);
                    relationBatch.Clear();
                }
            }
        }
        if (relationBatch.Any())
        {
            SaveAreaBatch(connection, relationBatch);
        }
    }

    private void SaveAreaBatch(SqliteConnection connection, List<OsmRelation> relationBatch)
    {
        using var transaction = connection.BeginTransaction();
        int wayTotal = 0;

        var wayIds = relationBatch.SelectMany(r => r.Members).Where(m => m.Type == "way").Select(m => m.Id);
        
        var waysDict = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var ways = FetchWays(chunk.ToArray());
            return ways;
        }).ToDictionary(k => k.Id, v => v);

        foreach (var relation in relationBatch)
        {

            // bravely ignoring inner polygons
            //var _innerWays = relation.Members.Where(m => m.Role == "inner").Select(m => m.Id);
            //var _innerCoords = FetchWays().Where(w => _innerWays.Contains(w.Id)).SelectMany(w => w.Coordinates);

            var outerWayIds = relation.Members.Where(m => m.Role == "outer" && m.Type == "way").Select(m => m.Id);
            var outerWaysWithNulls = outerWayIds.Select(w => waysDict.GetValueOrDefault(w)!).ToArray();
            var outerWays = outerWaysWithNulls.Where(w => w != null).ToArray();
            if (outerWays.Length == 0)
            {
                Console.WriteLine($"No outer ways found for relation {relation.Id}");
                continue;
            }
            int closedLoops = outerWays.Count(w => w.ClosedLoop);
            if (closedLoops > 0)
            {
                Console.WriteLine($"relation {relation.Id} features closed loops: {closedLoops}/{outerWays.Count()}");
                if (outerWays.Count() > 1)
                {
                    Console.WriteLine("Skipping multipolygon with multiple closed loops...");
                    continue;
                }
            }
            if (outerWaysWithNulls.Any(w => w == null))
            {
                Console.WriteLine($"relation {relation.Id} is incomplete.");
            }

            // osm polygons are closed, however we do not always have the entire polygon.
            // some fiddly code to close it ourselves
            var unusedWays = new List<Way>(outerWays);
            var orderedCoords = new List<Coord>();
            orderedCoords.AddRange(unusedWays.First().Coordinates);
            unusedWays.Remove(unusedWays.First());

            while (unusedWays.Count > 0)
            {
                // find next coord with shortest distance
                double shortest = double.MaxValue;
                Way? next = null;
                bool reversed = false;
                foreach (var way in unusedWays)
                {
                    var dist = way.Coordinates.First().DistanceSquaredTo(orderedCoords.Last());
                    if (dist < shortest)
                    {
                        shortest = dist;
                        next = way;
                        reversed = false;
                    }

                    dist = way.Coordinates.Last().DistanceSquaredTo(orderedCoords.Last());
                    if (dist < shortest)
                    {
                        shortest = dist;
                        next = way;
                        reversed = true;
                    }
                }

                if (next == null)
                {
                    Console.WriteLine($"Failed to find next way for relation {relation.Id}");
                    break;
                }

                var nextRange = reversed ? next.Coordinates.Reverse<Coord>() : next.Coordinates;
                if (shortest == 0.0)
                {
                    nextRange = nextRange.Skip(1);
                }
                orderedCoords.AddRange(nextRange);
                unusedWays.Remove(next);
            }

            if (orderedCoords.Count == 0)
            {
                Console.WriteLine($"Failed to find any coords for relation {relation.Id}");
                continue;
            }
            // ensure loop is closed
            if (!orderedCoords.First().LocationEquals(orderedCoords.Last()))
            {
                orderedCoords.Add(orderedCoords.First());
            }

            var coords = string.Join(";", orderedCoords.Select(id => $"{id.Lat},{id.Lon}"));
            wayTotal++;
            using var insertAreaCommand = connection.CreateCommand();
            insertAreaCommand.CommandText = @"
                    INSERT INTO area (id, visible, version, change_set, timestamp, user, uid, coords, name, suggested_colour)
                        VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $coords, $name, $suggested_colour);
                    ";
            insertAreaCommand.Parameters.AddWithValue("$id", relation.Id);
            insertAreaCommand.Parameters.AddWithValue("$visible", relation.Visible as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$version", relation.Version as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$change_set", relation.ChangeSet);
            insertAreaCommand.Parameters.AddWithValue("$timestamp", relation.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$user", relation.User as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$uid", relation.Uid as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$coords", coords);
            insertAreaCommand.Parameters.AddWithValue("$name", relation.Tags.ContainsKey("name") ? relation.Tags["name"] : DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$suggested_colour", calcSuggestedColour(relation));
            insertAreaCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IEnumerable<Area> FetchAreas()
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, coords, name, suggested_colour FROM area;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Area
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
                Name = reader.NullableString(8),
                SuggestedColour = reader.GetString(9),
            };
        }
    }
}