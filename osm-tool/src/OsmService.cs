using System.Data;
using OsmTool.Models;

namespace OsmTool;


public record Coord
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    public Coord() { }

    public Coord(double lat, double lon) : this()
    {
        Lat = lat;
        Lon = lon;
    }

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

public class OsmService
{
    private readonly TileService tileService = new TileService();

    private readonly BuildingHeightService heightService = new BuildingHeightService();

    private readonly HighwayBuilderService highwayBuilderService = new HighwayBuilderService();

    private readonly SuggestedColourService suggestedColourService = new SuggestedColourService();

    private readonly SqliteStore sqliteStore;

    public OsmService(SqliteStore sqliteStore)
    {
        this.sqliteStore = sqliteStore;
    }

    public void BuildDatabase(IReader reader)
    {
        Console.WriteLine("Building database...");
        sqliteStore.InitDataStore();
        
        Console.WriteLine("Saving nodes...");
        SaveNodes(reader);
        Console.WriteLine("Saving ways...");
        SaveWays(reader);

        Console.WriteLine("Saving multipolygon relations...");
        SaveAreas(reader);

        Console.WriteLine("Building search index...");
        sqliteStore.BuildSearchIndex();
    }

    public void SaveNodes(IReader reader)
    {
        var nodeBatch = new List<OsmNode>();
        var nodes = reader.IterateNodes();
        foreach (var node in nodes)
        {
            nodeBatch.Add(node);
            if (nodeBatch.Count() > 1000)
            {
                sqliteStore.SaveNodeBatch(nodeBatch);
                nodeBatch.Clear();
            }
        }

        if (nodeBatch.Any())
        {
            sqliteStore.SaveNodeBatch(nodeBatch);
        }
    }

    public void SaveWays(IReader reader)
    {
        var wayBatch = new List<OsmWay>();
        var ways = reader.IterateWays();
        foreach (var osmWay in ways)
        {
            wayBatch.Add(osmWay);
            if (wayBatch.Count >= 100)
            {
                sqliteStore.SaveWayBatch(wayBatch);
                wayBatch.Clear();
            }
        }

        if (wayBatch.Any())
        {
            sqliteStore.SaveWayBatch(wayBatch);
        }
    }

    public void SaveAreas(IReader reader)
    {
        {
            var relationBatch = new List<OsmRelation>();
            foreach (var relation in reader.IterateRelations())
            {
                if (relation.Tags.Any(t => t.Key == "type" && t.Value == "multipolygon"))
                {
                    relationBatch.Add(relation);
                    if (relationBatch.Count >= 100)
                    {
                        SaveAreaBatch(relationBatch);
                        relationBatch.Clear();
                    }
                }
            }
            if (relationBatch.Any())
            {
                SaveAreaBatch(relationBatch);
            }
        }

        {
            Console.WriteLine("Writing single polygon areas.");
            var wayBatch = new List<Way>();
            foreach (var way in sqliteStore.FetchWays())
            {
                if (way.AreaParentId != null || !way.ClosedLoop)
                {
                    continue;
                }
                wayBatch.Add(way);
                if (wayBatch.Count >= 100)
                {
                    SaveAreaBatch(wayBatch);
                    wayBatch.Clear();
                }
            }
            if (wayBatch.Any())
            {
                SaveAreaBatch(wayBatch);
            }
        }

        {
            Console.WriteLine("Writing highways.");
            var wayBatch = new List<Way>();
            foreach (var way in sqliteStore.FetchWays())
            {
                if (way.ClosedLoop)
                {
                    continue;
                }
                wayBatch.Add(way);
                if (wayBatch.Count >= 100)
                {
                    SaveHighwayAreaBatch(wayBatch);
                    wayBatch.Clear();
                }
            }
            if (wayBatch.Any())
            {
                SaveHighwayAreaBatch(wayBatch);
            }
        }
    }

    private void SaveAreaBatch(List<OsmRelation> relationBatch)
    {
        var databaseAreas = new List<Area>();
        var largeTilesDict = new Dictionary<string, long[]>();
        var areaWayParentDict = new Dictionary<long, long[]>();
        var wayIds = relationBatch.SelectMany(r => r.Members).Where(m => m.Type == "way").Select(m => m.Id).Distinct();

        var waysDict = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var ways = sqliteStore.FetchWays(chunk.ToArray());
            return ways;
        }).ToDictionary(k => k.Id, v => v);
        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = sqliteStore.FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

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

            string suggestedColour = suggestedColourService.CalcSuggestedColour(relation.Tags);
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

                string name = relation.Tags.GetValueOrDefault("name", "");
                int layer = 0;
                if (int.TryParse(relation.Tags.GetValueOrDefault("layer", "0"), out int _layer))
                {
                    layer = _layer;
                }
                var relationArea = new Area
                {
                    Source = $"relation-{relation.Id}",
                    Visible = relation.Visible,
                    Version = relation.Version,
                    ChangeSet = relation.ChangeSet,
                    Timestamp = relation.Timestamp,
                    User = relation.User,
                    Uid = relation.Uid,
                    OuterCoordinates = coords,
                    InnerCoordinates = innerCoords,
                    Name = name,
                    SuggestedColour = suggestedColour,
                    TileId = tileId,
                    Layer = layer,
                    Height = heightResult.Height,
                    MinHeight = heightResult.MinHeight,
                    IsLarge = largeTileResult.IsLarge
                };

                databaseAreas.Add(relationArea);
                if (largeTileResult.IsLarge)
                {
                    largeTilesDict.Add(relationArea.Source, largeTileResult.Tiles.Select(t => t.Id).ToArray());
                }
            }

            var parentedWayIds = relation.Members.Where(m => m.Type == "way").Select(m => m.Id).ToArray();
            areaWayParentDict.Add(relation.Id, parentedWayIds);
        }
        sqliteStore.SaveAreaBatch(databaseAreas);
        sqliteStore.SaveTileAreaMap(largeTilesDict);
        sqliteStore.SaveWayParents(areaWayParentDict);
    }

    private void SaveAreaBatch(List<Way> wayBatch)
    {
        var databaseAreas = new List<Area>();
        var largeTilesDict = new Dictionary<string, long[]>();
        var wayIds = wayBatch.Select(w => w.Id).ToArray();

        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = sqliteStore.FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

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

            string suggestedColour = suggestedColourService.CalcSuggestedColour(wayTags);
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

            string name = wayTags.GetValueOrDefault("name", "");
            int layer = 0;
            if (int.TryParse(wayTags.GetValueOrDefault("layer", "0"), out int _layer))
            {
                layer = _layer;
            }
            var wayArea = new Area
            {
                Source = $"way-{way.Id}",
                Visible = way.Visible,
                Version = way.Version,
                ChangeSet = way.ChangeSet,
                Timestamp = way.Timestamp,
                User = way.User,
                Uid = way.Uid,
                OuterCoordinates = new[] { orderedCoords.ToArray() },
                InnerCoordinates = Array.Empty<Coord[]>(),
                Name = name,
                SuggestedColour = suggestedColour,
                TileId = tileService.CalcTileId(orderedCoords.Average(n => n.Lat), orderedCoords.Average(n => n.Lon)),
                Layer = layer,
                Height = heightResult.Height,
                MinHeight = heightResult.MinHeight,
                IsLarge = largeTileResult.IsLarge
            };

            databaseAreas.Add(wayArea);
            if (largeTileResult.IsLarge)
            {
                largeTilesDict.Add(wayArea.Source, largeTileResult.Tiles.Select(t => t.Id).ToArray());
            }
        }

        sqliteStore.SaveAreaBatch(databaseAreas);
        sqliteStore.SaveTileAreaMap(largeTilesDict);
    }

    private void SaveHighwayAreaBatch(List<Way> wayBatch)
    {
        var databaseAreas = new List<Area>();
        var wayIds = wayBatch.Select(w => w.Id).ToArray();

        var wayNodes = wayIds.Chunk(1000).SelectMany(chunk =>
        {
            var wayNodes = sqliteStore.FetchNodesByWayIds(chunk.ToArray());
            return wayNodes;
        }).ToDictionary();

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

            string suggestedColour = suggestedColourService.CalcSuggestedColour(wayTags);
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
                .ToArray();
            var tile = tileService.CalcTileId(orderedCoords.Average(n => n.Lat), orderedCoords.Average(n => n.Lon));

            string name = wayTags.GetValueOrDefault("name", "");
            int layer = 0;
            if (int.TryParse(wayTags.GetValueOrDefault("layer", "0"), out int _layer))
            {
                layer = _layer;
            }
            var highwayArea = new Area
            {
                Source = $"way-{way.Id}",
                Visible = way.Visible,
                Version = way.Version,
                ChangeSet = way.ChangeSet,
                Timestamp = way.Timestamp,
                User = way.User,
                Uid = way.Uid,
                OuterCoordinates = new[] { highwayCoords.ToArray() },
                InnerCoordinates = Array.Empty<Coord[]>(),
                Name = name,
                SuggestedColour = suggestedColour,
                TileId = tile,
                Layer = layer,
                Height = 0.1,
                MinHeight = 0.0,
                IsLarge = false
            };

            databaseAreas.Add(highwayArea);
        }

        sqliteStore.SaveAreaBatch(databaseAreas);
    }
}
