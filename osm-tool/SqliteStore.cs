using System.Data;
using System.Drawing;
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

    public static Coord[] FromNodes(IEnumerable<OsmNode> nodes)
    {
        return nodes.Select(n => new Coord { Lat = n.Lat, Lon = n.Lon }).ToArray();
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
    public bool ClosedLoop { get; set; }
    public long? AreaParentId { get; set; }
    public string Tags { get; set; } = "";

    public Dictionary<string, string> TagsToDict()
    {
        return DictUtils.StringToDict(Tags);
    }
}

public class Area
{
    public long Id { get; set; }

    public required string Source { get; set; }
    public bool? Visible { get; set; }
    public int? Version { get; set; }
    public long? ChangeSet { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string? User { get; set; }
    public long? Uid { get; set; }
    public required Coord[][] OuterCoordinates { get; set; }
    public required Coord[][] InnerCoordinates { get; set; }
    public string? Name { get; set; }
    public required string SuggestedColour { get; set; }
    public long TileId { get; set; }
    public int Layer { get; set; }
    public double Height { get; set; }

    public double MinHeight { get; set; }
    public bool IsLarge { get; set; }
}

public class SqliteStore
{
    private readonly TileService tileService = new TileService();

    private readonly BuildingHeightService heightService = new BuildingHeightService();

    private readonly HighwayBuilderService highwayBuilderService = new HighwayBuilderService();

    private string FilePath { get; init; }

    public SqliteStore(string filePath)
    {
        FilePath = filePath;
    }

    private SqliteConnection createConnection()
    {
        var connection = new SqliteConnection($"Data Source={FilePath}");
        connection.Open();
        // Enable write-ahead logging
        var walCommand = connection.CreateCommand();
        walCommand.CommandText =
        @"
            PRAGMA journal_mode = 'wal'
        ";
        walCommand.ExecuteNonQuery();
        return connection;
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
            lon REAL NOT NULL,
            tile_id INTEGER NOT NULL,
            layer INTEGER NOT NULL
        );
        CREATE INDEX idx_node_tile_id ON node (tile_id);
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

    private void SaveNodeBatch(SqliteConnection connection, List<OsmNode> nodeBatch)
    {
        using var transaction = connection.BeginTransaction();
        foreach (var node in nodeBatch)
        {
            using var insertNodeCommand = connection.CreateCommand();
            insertNodeCommand.CommandText = @"
                INSERT INTO node (id, visible, version, change_set, timestamp, user, uid, lat, lon, tile_id, layer)
                    VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $lat, $lon, $tile_id, $layer);
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
            insertNodeCommand.Parameters.AddWithValue("$tile_id", tileService.CalcTileId(node.Lat, node.Lon));
            insertNodeCommand.Parameters.AddWithValue("$layer", node.Tags.TryGetValue("layer", out string? value) ? value : 0);
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
        var q = string.Join(',', ids.Distinct());
        // I gave up making this parametered. nothing works
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, lat, lon FROM node WHERE id IN ($ids);".Replace("$ids", q);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var node = new OsmNode
            {
                Id = reader.GetInt64("id"),
                Visible = reader.GetBoolean("visible"),
                Version = reader.GetInt32("version"),
                ChangeSet = reader.GetInt64("change_set"),
                Timestamp = DateTimeOffset.Parse(reader.GetString("timestamp")),
                User = reader.GetString("user"),
                Uid = reader.GetInt64("uid"),
                Lat = reader.GetDouble("lat"),
                Lon = reader.GetDouble("lon")
            };
            result.Add(node.Id, node);
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
            closed_loop INTEGER NOT NULL,
            area_parent_id INTEGER NULL,
            tags TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS way_node_map (
            way_id INTEGER NOT NULL,
            node_id INTEGER NOT NULL,
            ordinal INTEGER NOT NULL
        );
        CREATE INDEX idx_way_node_map_way_id ON way_node_map (way_id);
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

    private string calcSuggestedColour(Dictionary<string, string> tags)
    {
        foreach (var (k, v) in tags)
        {
            if (k.Contains("note"))
            {
                Console.WriteLine($"'{k}' -> '{v}'");
            }
        }

        if (tags.TryGetValue("building:colour", out string? buildingColour))
        {
            if (buildingColour.StartsWith("#"))
            {
                return buildingColour;
            }
            var knownColour = Color.FromName(buildingColour);
            if (knownColour.ToArgb() != 0)
            {
                return "#" + knownColour.ToArgb().ToString("X6");
            }
            Console.WriteLine($"Irregular building:colour: {buildingColour}");
        }
        if (tags.TryGetValue("type", out string? areaType))
        {
            switch (areaType)
            {
                case "boundary": return "no-colour";
            }
        }
        if (tags.ContainsKey("indoor"))
        {
            return "no-colour";
        }
        if (tags.TryGetValue("highway", out string? highway))
        {
            switch (highway)
            {
                case "secondary":
                case "tertiary":
                case "service":
                case "residential":
                    return "white";
                case "primary":
                    return "yellow";
                case "motorway":
                case "trunk":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                    return "red";
                case "living_street":
                case "bus_guideway":
                case "raceway":
                case "road":
                case "proposed":
                case "mini_roundabout":
                case "motorway_junction":
                case "passing_place":
                case "services":
                case "stop":
                case "turning_circle":
                case "turning_loop":
                    return "red";
                case "pedestrian":
                case "footway":
                case "path":
                case "sidewalk":
                case "cycleway":
                    return "light-grey";
            }
        }
        if (tags.TryGetValue("area:highway", out string? areaHighway))
        {
            switch (areaHighway)
            {
                case "motorway":
                case "trunk":
                case "primary":
                case "secondary":
                case "tertiary":
                case "residential":
                case "service":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                case "living_street":
                case "bus_guideway":
                case "raceway":
                case "road":
                case "proposed":
                case "mini_roundabout":
                case "motorway_junction":
                case "passing_place":
                case "services":
                case "stop":
                case "turning_circle":
                case "turning_loop":
                    return "red";
                case "pedestrian":
                case "footway":
                case "path":
                case "sidewalk":
                case "cycleway":
                    return "light-grey";
                case "unclassified":
                case "traffic_island":
                    return "no-colour";
            }
        }
        if (tags.TryGetValue("railway", out string? _railwayValue))
        {
            return "black";
        }
        if (tags.TryGetValue("waterway", out string? waterway))
        {
            switch (waterway)
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
        if (tags.ContainsKey("water"))
        {
            return "blue";
        }
        if (tags.TryGetValue("building:part", out string? buildingPart))
        {
            if (buildingPart == "no")
            {
                return "no-colour";
            }
        }
        if (tags.ContainsKey("building") || tags.ContainsKey("building:colour") || tags.ContainsKey("building:part") || tags.ContainsKey("shop") || tags.ContainsKey("disused:shop"))
        {
            return "grey";
        }
        if (tags.ContainsKey("bridge:structure"))
        {
            return "grey";
        }
        if (tags.TryGetValue("landuse", out string? landUse))
        {
            switch (landUse)
            {
                case "farmland":
                    return "pale-yellow";
                case "grass":
                case "recreation_ground":
                    return "green";
                case "construction":
                    return "grey";
                case "retail":
                    return "light-red";
                case "railway":
                case "industrial":
                    return "light-purple";
                case "military":
                case "commercial":
                case "residential":

                default:
                    return "light-grey"; //???
            }
        }
        if (tags.TryGetValue("natural", out string? natural))
        {
            switch (natural)
            {
                case "water": return "blue";
                case "scrub": return "green";
                case "wood": return "dark-green";
            }
        }
        if (tags.TryGetValue("leisure", out string? leisure))
        {
            switch (leisure)
            {
                case "ice_rink":
                    return "grey";
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
                case "recreation_ground":
                case "nature_reserve":
                case "garden":
                case "common":
                case "dog_park":
                case "horse_riding":
                    return "green";
                case "marina":
                    return "blue";
                case "slip_way":
                    return "white";
                case "pitch":
                    return "turf-green";
            }
        }
        if (tags.TryGetValue("man_made", out string? manMade))
        {
            switch (manMade)
            {
                case "pier":
                    return "white";
                case "storage_tank":
                    return "no-colour";
                case "tower":
                case "train_station":
                    return "grey";
            }
        }
        if (tags.TryGetValue("amenity", out string? amenity))
        {
            switch (amenity)
            {
                case "parking":
                    return "light-grey";
                case "school":
                    return "light-yellow";
                case "prison":
                    return "dark-grey";
            }
        }

        return "unknown";
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
            var wayNodes = way.NodeReferences.Select(id => nodes[id]).ToArray();
            if (way.NodeReferences.Count == 0)
            {
                noCoord++;
            }
            bool closedLoop = way!.Tags!.GetValueOrDefault("area", null) == "yes"
                || nodes[way.NodeReferences.First()].LocationEquals(nodes[way.NodeReferences.Last()])
                && !way.Tags.ContainsKey("highway")
                && !way.Tags.ContainsKey("barrier");
            // ??? && !way.Tags.ContainsKey("waterway");

            wayTotal++;

            using var insertWayCommand = connection.CreateCommand();
            insertWayCommand.CommandText = @"
                    INSERT INTO way (id, visible, version, change_set, timestamp, user, uid, closed_loop, tags)
                        VALUES($id, $visible, $version, $change_set, $timestamp, $user, $uid, $closed_loop, $tags);
                    ";
            insertWayCommand.Parameters.AddWithValue("$id", way.Id);
            insertWayCommand.Parameters.AddWithValue("$visible", way.Visible as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$version", way.Version as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$change_set", way.ChangeSet);
            insertWayCommand.Parameters.AddWithValue("$timestamp", way.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$user", way.User as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$uid", way.Uid as object ?? DBNull.Value);
            insertWayCommand.Parameters.AddWithValue("$closed_loop", closedLoop);
            insertWayCommand.Parameters.AddWithValue("$tags", DictUtils.DictToString(way.Tags));
            insertWayCommand.ExecuteNonQuery();

            foreach (var node in wayNodes.Select((e, i) => new { e.Id, ordinal = i }))
            {
                using var insertWayNodeCommand = connection.CreateCommand();
                insertWayNodeCommand.CommandText = @"
                    INSERT INTO way_node_map (way_id, node_id, ordinal)
                        VALUES($way_id, $node_id, $ordinal);
                    ";
                insertWayNodeCommand.Parameters.AddWithValue("$way_id", way.Id);
                insertWayNodeCommand.Parameters.AddWithValue("$node_id", node.Id);
                insertWayNodeCommand.Parameters.AddWithValue("$ordinal", node.ordinal);
                insertWayNodeCommand.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    public IEnumerable<Way> FetchWays(long[]? ids = null, long[]? tileIds = null)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, area_parent_id, closed_loop, tags FROM way";
        var whereClauses = new List<string>();
        if (ids != null)
        {
            var q = string.Join(',', ids);
            whereClauses.Add($"id IN ({q})");
        }
        if (tileIds != null)
        {
            var q = string.Join(',', tileIds);
            whereClauses.Add($"tile_id IN ({q})");
        }

        if (whereClauses.Count > 0)
        {
            command.CommandText += " WHERE " + string.Join(" AND ", whereClauses);
        }
        command.CommandText += ";";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Way
            {
                Id = reader.GetInt64("id"),
                Visible = reader.GetBoolean("visible"),
                Version = reader.GetInt32("version"),
                ChangeSet = reader.GetInt64("change_set"),
                Timestamp = DateTimeOffset.Parse(reader.GetString("timestamp")),
                User = reader.GetString("user"),
                Uid = reader.GetInt64("uid"),
                AreaParentId = reader.GetValue("area_parent_id") as long?,
                ClosedLoop = reader.GetBoolean("closed_loop"),
                Tags = reader.GetString("tags")
            };
        }
    }

    private Dictionary<long, OsmNode[]> FetchNodesByWayIds(long[] wayIds)
    {
        var result = wayIds.Distinct().ToDictionary(id => id, _id => Array.Empty<OsmNode>());
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        var q = string.Join(',', wayIds);
        command.CommandText = @"SELECT way_id, node_id, ordinal FROM way_node_map WHERE way_id IN ($way_ids);".Replace("$way_ids", q);

        var wayNodeMaps = wayIds.ToDictionary(id => id, _id => new List<Tuple<long, int>>());
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var wayId = reader.GetInt64("way_id");
            var nodeId = reader.GetInt64("node_id");
            var ordinal = reader.GetInt32("ordinal");

            wayNodeMaps[wayId].Add(new Tuple<long, int>(nodeId, ordinal));
        }
        var nodeIds = wayNodeMaps.SelectMany(kv => kv.Value.Select(e => e.Item1)).Distinct().ToArray();
        var nodes = FetchByIds(nodeIds);

        foreach (var wayId in wayIds)
        {

            var wayNodeMap = wayNodeMaps[wayId];
            var wayNodes = wayNodeMap.OrderBy(m => m.Item2).Select(m => nodes!.GetValueOrDefault(m.Item1, null)).ToArray();
            result[wayId] = wayNodes!;
        }
        connection.Close();

        return result;
    }

    public void SaveAreas(IReader reader)
    {
        using (var connection = createConnection())
        {
            connection.Open();

            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS area (
                id INTEGER PRIMARY KEY,
                source TEXT NOT NULL,
                visible INTEGER NULL,
                version INTEGER NULL,
                change_set INTEGER NULL,
                timestamp TEXT NULL,
                user TEXT NOT NULL,
                uid INTEGER NULL,
                outer_coords TEXT NOT NULL,
                inner_coords TEXT NOT NULL,
                name TEXT NULL,
                suggested_colour TEXT NOT NULL,
                tile_id INTEGER NOT NULL,
                layer INTEGER NOT NULL,
                height REAL NOT NULL,
                min_height REAL NOT NULL,
                is_large INTEGER NOT NULL
            );
            CREATE INDEX idx_area_tile_id ON area (tile_id);

            CREATE TABLE IF NOT EXISTS tile_area_map (
                area_id INTEGER NOT NULL,
                tile_id INTEGER NOT NULL
            );
            CREATE INDEX idx_tile_area_map_area_id ON tile_area_map (area_id);
            CREATE INDEX idx_tile_area_map_tile_id ON tile_area_map (tile_id);
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

        using (var connection = createConnection())
        {
            connection.Open();
            Console.WriteLine("Writing single polygon areas.");
            var wayBatch = new List<Way>();
            foreach (var way in FetchWays())
            {
                if (way.AreaParentId != null || !way.ClosedLoop)
                {
                    continue;
                }
                wayBatch.Add(way);
                if (wayBatch.Count >= 100)
                {
                    SaveAreaBatch(connection, wayBatch);
                    wayBatch.Clear();
                }
            }
            if (wayBatch.Any())
            {
                SaveAreaBatch(connection, wayBatch);
            }
        }
        using (var connection = createConnection())
        {
            connection.Open();
            Console.WriteLine("Writing highways.");
            var wayBatch = new List<Way>();
            foreach (var way in FetchWays())
            {
                if (way.ClosedLoop)
                {
                    continue;
                }
                //if (way.Id == 413046623)
                {
                    wayBatch.Add(way);
                }
                if (wayBatch.Count >= 100)
                {
                    SaveHighwayAreaBatch(connection, wayBatch);
                    wayBatch.Clear();
                }
            }
            if (wayBatch.Any())
            {
                SaveHighwayAreaBatch(connection, wayBatch);
            }
        }
    }

    private void SaveAreaBatch(SqliteConnection connection, List<OsmRelation> relationBatch)
    {
        var wayIds = relationBatch.SelectMany(r => r.Members).Where(m => m.Type == "way").Select(m => m.Id).Distinct();

        var waysDict = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var ways = FetchWays(chunk.ToArray());
            return ways;
        }).ToDictionary(k => k.Id, v => v);
        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

        using var transaction = connection.BeginTransaction();

        foreach (var relation in relationBatch)
        {
            var innerWayIds = relation.Members.Where(m => m.Role == "inner" && m.Type == "way").Select(m => m.Id);
            // bravely ignoring inner polygons that are not closed
            var innerWays = innerWayIds.Select(w => waysDict.GetValueOrDefault(w)!).Where(w => w != null && w.ClosedLoop).ToArray();

            var outerWayIds = relation.Members.Where(m => m.Role == "outer" && m.Type == "way").Select(m => m.Id);
            var outerWaysWithNulls = outerWayIds.Select(w => waysDict.GetValueOrDefault(w)!).ToArray();
            var outerWays = outerWaysWithNulls.Where(w => w != null).ToArray();
            if (outerWays.Length == 0)
            {
                Console.WriteLine($"No outer ways found for relation {relation.Id}. Nulls found: {outerWaysWithNulls.Count(w => w == null)}");
                continue;
            }
            int closedLoops = outerWays.Count(w => w.ClosedLoop);
            if (closedLoops > 1) {
                Console.WriteLine($"!!!!! {relation.Id} closed");
            }
            if (outerWaysWithNulls.Any(w => w == null))
            {
                Console.WriteLine($"relation {relation.Id} is incomplete.");
            }

            var unusedWays = new List<Way>();
            var loopCoords = new List<Coord[]>();
            foreach (var way in outerWays)
            {
                if (way.ClosedLoop)
                {
                    loopCoords.Add(Coord.FromNodes(wayNodes[way.Id]));
                }
                else
                {
                    unusedWays.Add(way);
                }
            }

            if (unusedWays.Count > 0)
            {
                // osm polygons are closed, however we do not always have the entire polygon.
                // some fiddly code to close it ourselves
                var orderedCoords = new List<Coord>();
                orderedCoords.AddRange(Coord.FromNodes(wayNodes[unusedWays.First().Id]));
                unusedWays.Remove(unusedWays.First());

                while (unusedWays.Count > 0)
                {
                    // find next coord with shortest distance
                    double shortest = double.MaxValue;
                    Coord[]? nextCoordinates = null;
                    Way? nextWay = null;
                    bool reversed = false;
                    foreach (var way in unusedWays)
                    {
                        var wayCoords = Coord.FromNodes(wayNodes[way.Id]);
                        var dist = wayCoords.First().DistanceSquaredTo(orderedCoords.Last());
                        if (dist < shortest)
                        {
                            shortest = dist;
                            nextCoordinates = wayCoords;
                            nextWay = way;
                            reversed = false;
                        }

                        dist = wayCoords.Last().DistanceSquaredTo(orderedCoords.Last());
                        if (dist < shortest)
                        {
                            shortest = dist;
                            nextCoordinates = wayCoords;
                            nextWay = way;
                            reversed = true;
                        }
                    }

                    if (nextCoordinates == null || nextWay == null)
                    {
                        Console.WriteLine($"Failed to find next way for relation {relation.Id}");
                        break;
                    }

                    var nextRange = reversed ? nextCoordinates.Reverse<Coord>() : nextCoordinates;
                    if (shortest == 0.0)
                    {
                        nextRange = nextRange.Skip(1);
                    }
                    orderedCoords.AddRange(nextRange);
                    unusedWays.Remove(nextWay);
                }


                if (orderedCoords.Count == 0)
                {
                    Console.WriteLine($"Failed to find non-closed coords for relation {relation.Id}");
                }
                else
                {
                    // ensure loop is closed
                    if (!orderedCoords.First().LocationEquals(orderedCoords.Last()))
                    {
                        orderedCoords.Add(orderedCoords.First());
                    }
                    loopCoords.Add(orderedCoords.ToArray());
                }
            }

            string suggestedColour = calcSuggestedColour(relation.Tags);
            if (suggestedColour == "unknown")
            {
                Console.WriteLine($"Relation {relation.Id} has an unknown colour, skipping.");
            }
            else if (suggestedColour != "no-colour")
            {

                var coords = loopCoords.ToArray();
                var innerCoords = innerWays.Select(w => Coord.FromNodes(wayNodes[w.Id])).ToArray();
                var largeTileResult = tileService.CalcLargeTileRange(coords);
                var heightResult = heightService.CalcBuildingHeight(relation.Tags);
                var tileId = tileService.CalcTileId(coords);

                using var insertAreaCommand = connection.CreateCommand();
                insertAreaCommand.CommandText = @"
                    INSERT INTO area (source, visible, version, change_set, timestamp, user, uid, outer_coords, inner_coords, name, suggested_colour, tile_id, layer, height, min_height, is_large)
                        VALUES($source, $visible, $version, $change_set, $timestamp, $user, $uid, $outer_coords, $inner_coords, $name, $suggested_colour, $tile_id, $layer, $height, $min_height, $is_large)
                        RETURNING id;
                    ";
                insertAreaCommand.Parameters.AddWithValue("$source", $"relation-{relation.Id}");
                insertAreaCommand.Parameters.AddWithValue("$visible", relation.Visible as object ?? DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$version", relation.Version as object ?? DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$change_set", relation.ChangeSet);
                insertAreaCommand.Parameters.AddWithValue("$timestamp", relation.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$user", relation.User as object ?? DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$uid", relation.Uid as object ?? DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$outer_coords", coords.AsString());
                insertAreaCommand.Parameters.AddWithValue("$inner_coords", innerCoords.AsString());
                insertAreaCommand.Parameters.AddWithValue("$name", relation.Tags.TryGetValue("name", out string? nameValue) ? nameValue : DBNull.Value);
                insertAreaCommand.Parameters.AddWithValue("$suggested_colour", suggestedColour);
                insertAreaCommand.Parameters.AddWithValue("$tile_id", tileId);
                insertAreaCommand.Parameters.AddWithValue("$layer", relation.Tags.TryGetValue("layer", out string? layerValue) ? layerValue : 0);
                insertAreaCommand.Parameters.AddWithValue("$height", heightResult.Height);
                insertAreaCommand.Parameters.AddWithValue("$min_height", heightResult.MinHeight);
                insertAreaCommand.Parameters.AddWithValue("$is_large", largeTileResult.IsLarge);

                if (largeTileResult.IsLarge)
                {
                    insertAreaCommand.CommandText += @"

                    CREATE TEMP TABLE temp_id (id INTEGER);
                    INSERT INTO temp_id (id) VALUES (last_insert_rowid());

                    $values 
                    DROP TABLE temp_id;
                    "
                    // because sqlite has the dumbest rules about SQL variables
                    .Replace("$values", String.Join("\n", largeTileResult.Tiles.Select(t => $"INSERT INTO tile_area_map(area_id, tile_id) SELECT id, {t.Id} FROM temp_id;")));
                }
                insertAreaCommand.ExecuteNonQuery();
            }

            var parentedWayIds = relation.Members.Where(m => m.Type == "way").Select(m => m.Id);
            using var wayAreaParent = connection.CreateCommand();
            wayAreaParent.CommandText = @"
                    UPDATE way 
                    SET area_parent_id = $area_parent_id
                    WHERE id IN($way_ids);".Replace("$way_ids", string.Join(',', parentedWayIds));
            wayAreaParent.Parameters.AddWithValue("$area_parent_id", relation.Id);
            wayAreaParent.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private void SaveAreaBatch(SqliteConnection connection, List<Way> wayBatch)
    {
        var wayIds = wayBatch.Select(w => w.Id).ToArray();

        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

        using var transaction = connection.BeginTransaction();

        foreach (var way in wayBatch)
        {
            var orderedCoords = new List<Coord>();
            orderedCoords.AddRange(Coord.FromNodes(wayNodes[way.Id]));

            if (orderedCoords.Count == 0)
            {
                Console.WriteLine($"Failed to find any coords for way {way.Id}");
                continue;
            }
            var wayTags = way.TagsToDict();

            string suggestedColour = calcSuggestedColour(wayTags);
            if (suggestedColour == "unknown")
            {
                Console.WriteLine($"Way {way.Id} has an unknown colour, skipping.");
                continue;
            }
            else if (suggestedColour == "no-colour")
            {
                continue;
            }

            var largeTileResult = tileService.CalcLargeTileRange(orderedCoords);
            var heightResult = heightService.CalcBuildingHeight(wayTags);

            var coords = orderedCoords.ToArray().AsString();
            using var insertAreaCommand = connection.CreateCommand();
            insertAreaCommand.CommandText = @"
                    INSERT INTO area (source, visible, version, change_set, timestamp, user, uid, outer_coords, inner_coords, name, suggested_colour, tile_id, layer, height, min_height, is_large)
                        VALUES($source, $visible, $version, $change_set, $timestamp, $user, $uid, $outer_coords, $inner_coords, $name, $suggested_colour, $tile_id, $layer, $height, $min_height, $is_large);
                    ";
            insertAreaCommand.Parameters.AddWithValue("$source", $"way-{way.Id}");
            insertAreaCommand.Parameters.AddWithValue("$visible", way.Visible as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$version", way.Version as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$change_set", way.ChangeSet);
            insertAreaCommand.Parameters.AddWithValue("$timestamp", way.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$user", way.User as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$uid", way.Uid as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$outer_coords", coords);
            insertAreaCommand.Parameters.AddWithValue("$inner_coords", "");
            insertAreaCommand.Parameters.AddWithValue("$name", wayTags.TryGetValue("name", out string? nameValue) ? nameValue : DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$suggested_colour", suggestedColour);
            insertAreaCommand.Parameters.AddWithValue("$tile_id", tileService.CalcTileId(orderedCoords.Average(n => n.Lat), orderedCoords.Average(n => n.Lon)));
            insertAreaCommand.Parameters.AddWithValue("$layer", wayTags.TryGetValue("layer", out string? layerValue) ? layerValue : 0);
            insertAreaCommand.Parameters.AddWithValue("$height", heightResult.Height);
            insertAreaCommand.Parameters.AddWithValue("$min_height", heightResult.MinHeight);
            insertAreaCommand.Parameters.AddWithValue("$is_large", largeTileResult.IsLarge);

            if (largeTileResult.IsLarge)
            {
                insertAreaCommand.CommandText += @"

                CREATE TEMP TABLE temp_id (id INTEGER);
                INSERT INTO temp_id (id) VALUES (last_insert_rowid());

                $values 
                DROP TABLE temp_id;
                "
                // because sqlite has the dumbest rules about SQL variables
                .Replace("$values", String.Join("\n", largeTileResult.Tiles.Select(t => $"INSERT INTO tile_area_map(area_id, tile_id) SELECT id, {t.Id} FROM temp_id;")));
            }
            insertAreaCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private void SaveHighwayAreaBatch(SqliteConnection connection, List<Way> wayBatch)
    {
        var wayIds = wayBatch.Select(w => w.Id).ToArray();

        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

        using var transaction = connection.BeginTransaction();

        foreach (var way in wayBatch)
        {
            var orderedCoords = new List<Coord>();
            orderedCoords.AddRange(Coord.FromNodes(wayNodes[way.Id]));

            if (orderedCoords.Count == 0)
            {
                Console.WriteLine($"Failed to find any coords for way {way.Id}");
                continue;
            }
            double width = 0.0;
            var wayTags = way.TagsToDict();
            if (wayTags.TryGetValue("highway", out string? highwayValue))
            {
                switch (highwayValue)
                {
                    case "primary":
                        width = 0.00004;
                        break;
                    case "secondary":
                    case "tertiary":
                    case "service":
                    case "residential":
                        width = 0.00002;
                        break;
                    case "motorway":
                    case "trunk":
                    case "motorway_link":
                    case "trunk_link":
                    case "primary_link":
                    case "secondary_link":
                    case "tertiary_link":
                        width = 0.00008;
                        break;
                }
            }
            else if (wayTags.TryGetValue("railway", out string? railwayValue))
            {
                switch (highwayValue)
                {
                    case "rail":
                        width = 0.00002;
                        break;
                }
            }
            if (width == 0.0)
            {
                continue;
            }

            string suggestedColour = calcSuggestedColour(wayTags);
            if (suggestedColour == "unknown")
            {
                Console.WriteLine($"Way {way.Id} has an unknown colour, skipping.");
                continue;
            }
            else if (suggestedColour == "no-colour")
            {
                continue;
            }

            var highwayCoords = highwayBuilderService.CalcHighwayCoordinates(orderedCoords, width)
                //.Reverse<Coord>()
                .ToArray();
            var tile = tileService.CalcTileId(orderedCoords.Average(n => n.Lat), orderedCoords.Average(n => n.Lon));

            var coords = highwayCoords.ToArray().AsString();
            using var insertAreaCommand = connection.CreateCommand();
            insertAreaCommand.CommandText = @"
                    INSERT INTO area (source, visible, version, change_set, timestamp, user, uid, outer_coords, inner_coords, name, suggested_colour, tile_id, layer, height, min_height, is_large)
                        VALUES($source, $visible, $version, $change_set, $timestamp, $user, $uid, $outer_coords, $inner_coords, $name, $suggested_colour, $tile_id, $layer, $height, $min_height, $is_large);
                    ";
            insertAreaCommand.Parameters.AddWithValue("$source", $"way-{way.Id}");
            insertAreaCommand.Parameters.AddWithValue("$visible", way.Visible as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$version", way.Version as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$change_set", way.ChangeSet);
            insertAreaCommand.Parameters.AddWithValue("$timestamp", way.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$user", way.User as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$uid", way.Uid as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$outer_coords", coords);
            insertAreaCommand.Parameters.AddWithValue("$inner_coords", "");
            insertAreaCommand.Parameters.AddWithValue("$name", wayTags.TryGetValue("name", out string? nameValue) ? nameValue : DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$suggested_colour", suggestedColour);
            insertAreaCommand.Parameters.AddWithValue("$tile_id", tile);
            insertAreaCommand.Parameters.AddWithValue("$layer", wayTags.TryGetValue("layer", out string? layerValue) ? layerValue : 0);
            insertAreaCommand.Parameters.AddWithValue("$height", 0.1);
            insertAreaCommand.Parameters.AddWithValue("$min_height", 0.0);
            insertAreaCommand.Parameters.AddWithValue("$is_large", false);

            insertAreaCommand.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IEnumerable<Area> FetchAreas(long[]? ids = null, long[]? tileIds = null)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, source, visible, version, change_set, timestamp, user, uid, outer_coords, inner_coords, name, suggested_colour, tile_id, layer, height, min_height, is_large FROM area";

        var whereClauses = new List<string>();

        if (ids != null)
        {
            var q = string.Join(',', ids);
            whereClauses.Add($"id IN ({q})");
        }
        if (tileIds != null)
        {
            var q = string.Join(',', tileIds);
            whereClauses.Add($"tile_id IN ({q})");
        }

        if (whereClauses.Count > 0)
        {
            command.CommandText += " WHERE " + string.Join(" AND ", whereClauses);
        }
        command.CommandText += ";";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new Area
            {
                Id = reader.GetInt64("id"),
                Source = reader.GetString("source"),
                Visible = reader.GetBoolean("visible"),
                Version = reader.GetInt32("version"),
                ChangeSet = reader.GetInt64("change_set"),
                Timestamp = DateTimeOffset.Parse(reader.GetString("timestamp")),
                User = reader.GetString("user"),
                Uid = reader.GetInt64("uid"),
                OuterCoordinates = reader.GetString("outer_coords").CoordsFromString(),
                InnerCoordinates = reader.GetString("inner_coords").CoordsFromString(),
                Name = reader.NullableString("name"),
                SuggestedColour = reader.GetString("suggested_colour"),
                TileId = reader.GetInt64("tile_id"),
                Layer = reader.GetInt32("layer"),
                Height = reader.GetDouble("height"),
                MinHeight = reader.GetDouble("min_height"),
                IsLarge = reader.GetBoolean("is_large"),
            };
        }
    }

    public IEnumerable<long> FetchAreaIdsByTileIds(long[] tileIds)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT area_id FROM tile_area_map WHERE tile_id IN ($tile_ids);".Replace("$tile_ids", string.Join(",", tileIds));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var areaId = reader.GetInt64(0);
            yield return areaId;
        }
    }
}