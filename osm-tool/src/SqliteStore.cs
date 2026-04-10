using System.Data;
using MapBoy.Models;
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
            uid INTEGER NULL,
            outer_coords TEXT NOT NULL,
            inner_coords TEXT NOT NULL,
            names TEXT,
            suggested_colour TEXT NOT NULL,
            tile_id INTEGER NOT NULL,
            layer INTEGER NOT NULL,
            height REAL NOT NULL,
            min_height REAL NOT NULL,
            roof_type TEXT NOT NULL,
            roof_height REAL NOT NULL,
            roof_colour TEXT NOT NULL,
            roof_orientation TEXT NOT NULL,
            is_large INTEGER NOT NULL,
            is_3d INTEGER NOT NULL
        );
        CREATE INDEX idx_area_tile_id ON area (tile_id);
        CREATE INDEX idx_area_source ON area (source);

        CREATE TABLE IF NOT EXISTS tile_area_map (
            area_id INTEGER NOT NULL,
            tile_id INTEGER NOT NULL
        );
        CREATE INDEX idx_tile_area_map_area_id ON tile_area_map (area_id);
        CREATE INDEX idx_tile_area_map_tile_id ON tile_area_map (tile_id);

        CREATE TABLE IF NOT EXISTS furniture (
            id INTEGER PRIMARY KEY,
            uid INTEGER NOT NULL,
            furniture_type INTEGER NOT NULL,
            lat REAL NOT NULL,
            lon REAL NOT NULL,
            tile_id INTEGER NOT NULL
        );
        CREATE INDEX idx_furniture_tile_id ON furniture (tile_id);
        ";

        await createTableCommand.ExecuteNonQueryAsync();
    }

    public async Task SaveNodeBatch(IEnumerable<OsmNode> nodes)
    {
        foreach (var nodeBatch in nodes.Chunk(1000))
        {
            using var connection = createConnection();
            using var transaction = connection.BeginTransaction();
            using var insertNodeCommand = connection.CreateCommand();
            insertNodeCommand.Transaction = transaction;
            insertNodeCommand.CommandText = @"
            INSERT INTO node (id, visible, uid, lat, lon, tile_id, layer)
                VALUES($id, $visible, $uid, $lat, $lon, $tile_id, $layer);
            ";

            var idParam = insertNodeCommand.Parameters.Add("$id", SqliteType.Integer);
            var visibleParam = insertNodeCommand.Parameters.Add("$visible", SqliteType.Integer);
            var uidParam = insertNodeCommand.Parameters.Add("$uid", SqliteType.Integer);
            var latParam = insertNodeCommand.Parameters.Add("$lat", SqliteType.Real);
            var lonParam = insertNodeCommand.Parameters.Add("$lon", SqliteType.Real);
            var tileIdParam = insertNodeCommand.Parameters.Add("$tile_id", SqliteType.Integer);
            var layerParam = insertNodeCommand.Parameters.Add("$layer", SqliteType.Integer);
            foreach (var node in nodeBatch)
            {
                idParam.Value = node.Id;
                visibleParam.Value = node.Visible as object ?? DBNull.Value;
                uidParam.Value = node.Uid as object ?? DBNull.Value;
                latParam.Value = node.Lat;
                lonParam.Value = node.Lon;
                tileIdParam.Value = tileService.CalcTileId(node.Lat, node.Lon);
                layerParam.Value = node.Tags.TryGetValue("layer", out string? value) ? value : 0;
                await insertNodeCommand.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
    }

    public async Task SaveFurnitureBatch(IEnumerable<Furniture> furnitures)
    {
        foreach (var furnitureBatch in furnitures.Chunk(1000))
        {
            using var connection = createConnection();
            using var transaction = connection.BeginTransaction();
            using var insertFurnitureCommand = connection.CreateCommand();
            insertFurnitureCommand.Transaction = transaction;
            insertFurnitureCommand.CommandText = @"
            INSERT INTO furniture (uid, lat, lon, tile_id, furniture_type)
                VALUES($uid, $lat, $lon, $tile_id, $furniture_type);
            ";

            var uidParam = insertFurnitureCommand.Parameters.Add("$uid", SqliteType.Integer);
            var latParam = insertFurnitureCommand.Parameters.Add("$lat", SqliteType.Real);
            var lonParam = insertFurnitureCommand.Parameters.Add("$lon", SqliteType.Real);
            var tileIdParam = insertFurnitureCommand.Parameters.Add("$tile_id", SqliteType.Integer);
            var furnitureTypeParam = insertFurnitureCommand.Parameters.Add("$furniture_type", SqliteType.Text);
            foreach (var node in furnitureBatch)
            {
                uidParam.Value = node.Uid;
                latParam.Value = node.Lat;
                lonParam.Value = node.Lon;
                tileIdParam.Value = node.TileId;
                furnitureTypeParam.Value = node.FurnitureType;
                await insertFurnitureCommand.ExecuteNonQueryAsync();
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
        command.CommandText = @"SELECT id, visible, uid, lat, lon FROM node WHERE id IN ($ids);".Replace("$ids", q);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var node = new OsmNode
            {
                Id = reader.GetInt64("id"),
                Visible = reader.GetBoolean("visible"),
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

            using var insertWayCommand = connection.CreateCommand();
            insertWayCommand.Transaction = transaction;
            insertWayCommand.CommandText = @"
            INSERT INTO way (id, visible, uid, closed_loop, tags)
                VALUES($id, $visible, $uid, $closed_loop, $tags);
            ";
            var idParam = insertWayCommand.Parameters.Add("$id", SqliteType.Integer);
            var visibleParam = insertWayCommand.Parameters.Add("$visible", SqliteType.Integer);
            var uidParam = insertWayCommand.Parameters.Add("$uid", SqliteType.Integer);
            var closedLoopParam = insertWayCommand.Parameters.Add("$closed_loop", SqliteType.Integer);
            var tagsParam = insertWayCommand.Parameters.Add("$tags", SqliteType.Text);


            using var insertWayNodeCommand = connection.CreateCommand();
            insertWayNodeCommand.CommandText = @"
                    INSERT INTO way_node_map (way_id, node_id, ordinal)
                        VALUES($way_id, $node_id, $ordinal);
                    ";
            var wayIdParam = insertWayNodeCommand.Parameters.Add("$way_id", SqliteType.Integer);
            var nodeIdParam = insertWayNodeCommand.Parameters.Add("$node_id", SqliteType.Integer);
            var ordinalParam = insertWayNodeCommand.Parameters.Add("$ordinal", SqliteType.Integer);

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

                idParam.Value = way.Id;
                visibleParam.Value = way.Visible as object ?? DBNull.Value;
                uidParam.Value = way.Uid as object ?? DBNull.Value;
                closedLoopParam.Value = closedLoop;
                tagsParam.Value = DictUtils.DictToString(way.Tags);

                await insertWayCommand.ExecuteNonQueryAsync();

                foreach (var node in wayNodes.Select((e, i) => new { e.Id, ordinal = i }))
                {
                    wayIdParam.Value = way.Id;
                    nodeIdParam.Value = node.Id;
                    ordinalParam.Value = node.ordinal;
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
        command.CommandText = @"SELECT id, visible, uid, area_parent_id, closed_loop, tags FROM way";
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

        using var insertAreaCommand = connection.CreateCommand();
        insertAreaCommand.Transaction = transaction;
        insertAreaCommand.CommandText = @"
                    INSERT INTO area (source, visible, uid, outer_coords, inner_coords, names, suggested_colour, tile_id, layer, height, min_height, roof_type, roof_height, roof_colour, roof_orientation, is_large, is_3d)
                        VALUES($source, $visible, $uid, $outer_coords, $inner_coords, $names, $suggested_colour, $tile_id, $layer, $height, $min_height, $roof_type, $roof_height, $roof_colour, $roof_orientation, $is_large, $is_3d);
                    ";

        var sourceParam = insertAreaCommand.Parameters.Add("$source", SqliteType.Text);
        var visibleParam = insertAreaCommand.Parameters.Add("$visible", SqliteType.Integer);
        var uidParam = insertAreaCommand.Parameters.Add("$uid", SqliteType.Integer);
        var outerCoordsParam = insertAreaCommand.Parameters.Add("$outer_coords", SqliteType.Text);
        var innerCoordsParam = insertAreaCommand.Parameters.Add("$inner_coords", SqliteType.Text);
        var namesParam = insertAreaCommand.Parameters.Add("$names", SqliteType.Text);
        var suggestedColourParam = insertAreaCommand.Parameters.Add("$suggested_colour", SqliteType.Text);
        var tileIdParam = insertAreaCommand.Parameters.Add("$tile_id", SqliteType.Integer);
        var layerParam = insertAreaCommand.Parameters.Add("$layer", SqliteType.Integer);
        var heightParam = insertAreaCommand.Parameters.Add("$height", SqliteType.Real);
        var minHeight = insertAreaCommand.Parameters.Add("$min_height", SqliteType.Real);
        var roofTypeParam = insertAreaCommand.Parameters.Add("$roof_type", SqliteType.Text);
        var roofHeightParam = insertAreaCommand.Parameters.Add("$roof_height", SqliteType.Text);
        var roofColourParam = insertAreaCommand.Parameters.Add("$roof_colour", SqliteType.Text);
        var roofOrientationParam = insertAreaCommand.Parameters.Add("$roof_orientation", SqliteType.Text);
        var isLargeParam = insertAreaCommand.Parameters.Add("$is_large", SqliteType.Integer);
        var is3dParam = insertAreaCommand.Parameters.Add("$is_3d", SqliteType.Integer);

        foreach (var area in areaBatch)
        {
            var outerCoords = area.OuterCoordinates.AsString();
            var innerCoords = area.InnerCoordinates.AsString();
            sourceParam.Value = area.Source;
            visibleParam.Value = area.Visible as object ?? DBNull.Value;
            uidParam.Value = area.Uid as object ?? DBNull.Value;
            outerCoordsParam.Value = outerCoords;
            innerCoordsParam.Value = innerCoords;
            namesParam.Value = area.Names.ArrayToString();
            suggestedColourParam.Value = area.SuggestedColour;
            tileIdParam.Value = area.TileId;
            layerParam.Value = area.Layer;
            heightParam.Value = area.Height;
            minHeight.Value = area.MinHeight;
            roofTypeParam.Value = area.RoofType;
            roofHeightParam.Value = area.RoofHeight;
            roofColourParam.Value = area.RoofColour;
            roofOrientationParam.Value = area.RoofOrientation;
            isLargeParam.Value = area.IsLarge;
            is3dParam.Value = area.Is3d;

            await insertAreaCommand.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
    public async IAsyncEnumerable<Area> FetchAreas(long[]? ids = null, long[]? tileIds = null)
    {
        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, source, visible, uid, outer_coords, inner_coords, names, suggested_colour, tile_id, layer, height, min_height, roof_type, roof_height, roof_colour, roof_orientation, is_large, is_3d FROM area";

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
                Uid = reader.GetInt64("uid"),
                OuterCoordinates = reader.GetString("outer_coords").CoordsFromString(),
                InnerCoordinates = reader.GetString("inner_coords").CoordsFromString(),
                Names = reader.GetString("names").StringToArray(),
                SuggestedColour = reader.GetString("suggested_colour"),
                TileId = reader.GetInt64("tile_id"),
                Layer = reader.GetInt32("layer"),
                Height = reader.GetDouble("height"),
                MinHeight = reader.GetDouble("min_height"),
                RoofType = reader.GetString("roof_type"),
                RoofHeight = reader.GetDouble("roof_height"),
                RoofColour = reader.GetString("roof_colour"),
                RoofOrientation = reader.GetString("roof_orientation"),
                IsLarge = reader.GetBoolean("is_large"),
                Is3d = reader.GetBoolean("is_3d"),
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

    public async IAsyncEnumerable<Furniture> FetchFurnitureByTileIds(long[] tileIds)
    {

        using var connection = createConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, uid, furniture_type, lat, lon, tile_id FROM furniture WHERE tile_id IN ($tile_ids);".Replace("$tile_ids", string.Join(",", tileIds));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            yield return new Furniture
            {
                Id = reader.GetInt64("id"),
                Uid = reader.GetInt64("uid"),
                FurnitureType = (FurnitureType)reader.GetInt32("furniture_type"),
                Lat = reader.GetDouble("lat"),
                Lon = reader.GetDouble("lon"),
                TileId = reader.GetInt64("tile_id"),
            };
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
            using var searchIndexCommand = connection.CreateCommand();
            searchIndexCommand.Transaction = transaction;
            searchIndexCommand.CommandText = @"
                    INSERT INTO search_index (name, lat, lon)
                    VALUES ($name, $lat, $lon);
                ";
            var nameParam = searchIndexCommand.Parameters.Add("$name", SqliteType.Text);
            var latParam = searchIndexCommand.Parameters.Add("$lat", SqliteType.Real);
            var lonParam = searchIndexCommand.Parameters.Add("$lon", SqliteType.Real);
            foreach (var searchIndexTuple in searchIndexBatch)
            {
                nameParam.Value = searchIndexTuple.Name;
                latParam.Value = searchIndexTuple.Lat;
                lonParam.Value = searchIndexTuple.Lon;

                searchIndexCommand.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public async Task UpdateAreaVisibility(bool visible, long[] areaIds)
    {

        using var connection = createConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var batchAreaIds in areaIds.Chunk(100))
        {
            using var searchIndexCommand = connection.CreateCommand();
            searchIndexCommand.Transaction = transaction;
            searchIndexCommand.CommandText = @"
                    UPDATE area
                    SET visible = $visible
                    WHERE id IN ( $ids );
                "
                .Replace("$ids", string.Join(", ", batchAreaIds));
            searchIndexCommand.Parameters.AddWithValue("$visible", visible);
            await searchIndexCommand.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public Task<long> FetchSizeBytes()
    {
        var length = new FileInfo(FilePath).Length;
        return Task.FromResult(length);
    }

    public async Task ClearNodes()
    {
        using var connection = createConnection();
        using var deleteNodeCommand = connection.CreateCommand();
        deleteNodeCommand.CommandText = @"DELETE FROM node;";
        await deleteNodeCommand.ExecuteNonQueryAsync();
    }

    public async Task ClearWays()
    {
        using var connection = createConnection();
        using var deleteWayCommand = connection.CreateCommand();
        deleteWayCommand.CommandText = @"DELETE FROM way;";
        await deleteWayCommand.ExecuteNonQueryAsync();
    }

    public async Task CompressDatabase()
    {
        using var connection = createConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"VACUUM;";
        await command.ExecuteNonQueryAsync();
    }
}
