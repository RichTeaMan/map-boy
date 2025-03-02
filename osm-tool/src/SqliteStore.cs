using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OsmTool.Models;

namespace OsmTool;

public class SqliteStore : ILocationSearch
{
    private readonly TileService tileService = new TileService();

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

    public async Task InitDataStore()
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
        CREATE INDEX idx_area_source ON area (source);

        CREATE TABLE IF NOT EXISTS tile_area_map (
            area_id INTEGER NOT NULL,
            tile_id INTEGER NOT NULL
        );
        CREATE INDEX idx_tile_area_map_area_id ON tile_area_map (area_id);
        CREATE INDEX idx_tile_area_map_tile_id ON tile_area_map (tile_id);
        ";

        await createTableCommand.ExecuteNonQueryAsync();
    }

    public async Task SaveNodeBatch(IEnumerable<OsmNode> nodes)
    {
        foreach (var nodeBatch in nodes.Chunk(1000))
        {
            using var connection = createConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var node in nodeBatch)
            {
                using var insertNodeCommand = connection.CreateCommand();
                insertNodeCommand.Transaction = transaction;
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
                await insertNodeCommand.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
    }

    public async Task<Dictionary<long, OsmNode>> FetchNodesByIds(long[] ids)
    {
        var result = new Dictionary<long, OsmNode>();
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        var q = string.Join(',', ids.Distinct());
        // I gave up making this parametered. nothing works
        command.CommandText = @"SELECT id, visible, version, change_set, timestamp, user, uid, lat, lon FROM node WHERE id IN ($ids);".Replace("$ids", q);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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

    public async Task SaveWayBatch(IEnumerable<OsmWay> ways)
    {
        foreach (var wayBatch in ways.Chunk(1000))
        {
            using var connection = createConnection();
            var nodeIds = wayBatch.SelectMany(w => w.NodeReferences).Distinct().ToArray();
            var nodes = await FetchNodesByIds(nodeIds);
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
                insertWayCommand.Transaction = transaction;
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
                await insertWayCommand.ExecuteNonQueryAsync();

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
                    await insertWayNodeCommand.ExecuteNonQueryAsync();
                }
            }
            await transaction.CommitAsync();
        }
    }

    public async IAsyncEnumerable<Way> FetchWays(long[]? ids = null, long[]? tileIds = null)
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

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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

    public async Task<Dictionary<long, OsmNode[]>> FetchNodesByWayIds(long[] wayIds)
    {
        var result = wayIds.Distinct().ToDictionary(id => id, _id => Array.Empty<OsmNode>());
        using var connection = createConnection();

        using var command = connection.CreateCommand();
        var q = string.Join(',', wayIds);
        command.CommandText = @"SELECT way_id, node_id, ordinal FROM way_node_map WHERE way_id IN ($way_ids);".Replace("$way_ids", q);

        var wayNodeMaps = wayIds.ToDictionary(id => id, _id => new List<Tuple<long, int>>());
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var wayId = reader.GetInt64("way_id");
            var nodeId = reader.GetInt64("node_id");
            var ordinal = reader.GetInt32("ordinal");

            wayNodeMaps[wayId].Add(new Tuple<long, int>(nodeId, ordinal));
        }
        var nodeIds = wayNodeMaps.SelectMany(kv => kv.Value.Select(e => e.Item1)).Distinct().ToArray();
        var nodes = await FetchNodesByIds(nodeIds);

        foreach (var wayId in wayIds)
        {
            var wayNodeMap = wayNodeMaps[wayId];
            var wayNodes = wayNodeMap.OrderBy(m => m.Item2).Select(m => nodes!.GetValueOrDefault(m.Item1, null)).ToArray();
            result[wayId] = wayNodes!;
        }

        return result;
    }

    public async Task SaveWayParents(Dictionary<long, long[]> relationWayMap)
    {
        using var connection = createConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var kv in relationWayMap)
        {
            var relationId = kv.Key;
            var wayIds = kv.Value;
            using var wayAreaParentCommand = connection.CreateCommand();
            wayAreaParentCommand.CommandText = @"
                    UPDATE way 
                    SET area_parent_id = $area_parent_id
                    WHERE id IN($way_ids);".Replace("$way_ids", string.Join(',', wayIds));
            wayAreaParentCommand.Parameters.AddWithValue("$area_parent_id", relationId);
            await wayAreaParentCommand.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task SaveTileAreaMap(Dictionary<string, long[]> areaTileMap)
    {
        using var connection = createConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var kv in areaTileMap)
        {
            var areaSource = kv.Key;
            var tileIds = kv.Value;
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"

                CREATE TEMP TABLE temp_id (id INTEGER);
                INSERT INTO temp_id (id) SELECT id from area WHERE source = '$source';

                $values 
                DROP TABLE temp_id;
                "
            .Replace("$source", areaSource)
            // because sqlite has the dumbest rules about SQL variables                
            .Replace("$values", String.Join("\n", tileIds.Select(t => $"INSERT INTO tile_area_map(area_id, tile_id) SELECT id, {t} FROM temp_id;")));
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task SaveAreaBatch(IEnumerable<Area> areaBatch)
    {
        using var connection = createConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var area in areaBatch)
        {
            var outerCoords = area.OuterCoordinates.AsString();
            var innerCoords = area.InnerCoordinates.AsString();
            using var insertAreaCommand = connection.CreateCommand();
            insertAreaCommand.Transaction = transaction;
            insertAreaCommand.CommandText = @"
                    INSERT INTO area (source, visible, version, change_set, timestamp, user, uid, outer_coords, inner_coords, name, suggested_colour, tile_id, layer, height, min_height, is_large)
                        VALUES($source, $visible, $version, $change_set, $timestamp, $user, $uid, $outer_coords, $inner_coords, $name, $suggested_colour, $tile_id, $layer, $height, $min_height, $is_large);
                    ";
            insertAreaCommand.Parameters.AddWithValue("$source", area.Source);
            insertAreaCommand.Parameters.AddWithValue("$visible", area.Visible as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$version", area.Version as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$change_set", area.ChangeSet);
            insertAreaCommand.Parameters.AddWithValue("$timestamp", area.Timestamp?.ToString("yyyy-MM-ddTHH:mm:ssZ") as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$user", area.User as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$uid", area.Uid as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$outer_coords", outerCoords);
            insertAreaCommand.Parameters.AddWithValue("$inner_coords", innerCoords);
            insertAreaCommand.Parameters.AddWithValue("$name", area.Name as object ?? DBNull.Value);
            insertAreaCommand.Parameters.AddWithValue("$suggested_colour", area.SuggestedColour);
            insertAreaCommand.Parameters.AddWithValue("$tile_id", area.TileId);
            insertAreaCommand.Parameters.AddWithValue("$layer", area.Layer);
            insertAreaCommand.Parameters.AddWithValue("$height", area.Height);
            insertAreaCommand.Parameters.AddWithValue("$min_height", area.MinHeight);
            insertAreaCommand.Parameters.AddWithValue("$is_large", area.IsLarge);

            await insertAreaCommand.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async IAsyncEnumerable<Area> FetchAreas(long[]? ids = null, long[]? tileIds = null)
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

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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

    public async IAsyncEnumerable<long> FetchAreaIdsByTileIds(long[] tileIds)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT area_id FROM tile_area_map WHERE tile_id IN ($tile_ids);".Replace("$tile_ids", string.Join(",", tileIds));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var areaId = reader.GetInt64(0);
            yield return areaId;
        }
    }

    public IEnumerable<SearchIndexResult> SearchAreas(string searchTerm)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, lat, lon FROM search_index WHERE name LIKE $name;";
        command.Parameters.AddWithValue("$name", $"%{searchTerm}%");

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new SearchIndexResult
            {
                Name = reader.GetString("name"),
                Lat = reader.GetDouble("lat"),
                Lon = reader.GetDouble("lon"),
                Rank = 0.5
            };
        }
    }

    public void InitIndex()
    {
        using var connection = createConnection();
        connection.Open();

        using var searchIndexTableCommand = connection.CreateCommand();
        searchIndexTableCommand.CommandText = @"
            CREATE TABLE search_index (
                name TEXT NULL,
                lat REAL NOT NULL,
                lon REAL NOT NULL
            );
            CREATE INDEX idx_search_index_name ON search_index (name);
            ";

        searchIndexTableCommand.ExecuteNonQuery();
    }

    public void UpdateIndex(IEnumerable<SearchIndexEntry> searchIndexEntries)
    {
        using var connection = createConnection();
        foreach (var searchIndexBatch in searchIndexEntries.Chunk(1000))
        {
            using var transaction = connection.BeginTransaction();
            foreach (var searchIndexTuple in searchIndexBatch)
            {
                using var searchIndexCommand = connection.CreateCommand();
                searchIndexCommand.Transaction = transaction;
                searchIndexCommand.CommandText = @"
                        INSERT INTO search_index (name, lat, lon)
                        VALUES ($name, $lat, $lon);
                    ";
                searchIndexCommand.Parameters.AddWithValue("$name", searchIndexTuple.Name);
                searchIndexCommand.Parameters.AddWithValue("$lat", searchIndexTuple.Lat);
                searchIndexCommand.Parameters.AddWithValue("$lon", searchIndexTuple.Lon);

                searchIndexCommand.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
}
