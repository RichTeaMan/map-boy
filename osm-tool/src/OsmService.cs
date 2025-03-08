using System.Collections.Concurrent;
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
    public bool Visible { get; set; }
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
    public double RoofHeight { get; set; }
    public required string RoofType { get; set; }
    public required string RoofColour { get; set; }
    public bool IsLarge { get; set; }

    public bool Is3d { get; set; }

}

public class OsmService
{
    private readonly TileService tileService = new TileService();

    private readonly BuildingHeightService heightService = new BuildingHeightService();

    private readonly HighwayBuilderService highwayBuilderService = new HighwayBuilderService();

    private readonly SuggestedColourService suggestedColourService = new SuggestedColourService();

    private readonly SqliteStore sqliteStore;

    private readonly ILocationSearch locationSearch;

    public OsmService(SqliteStore sqliteStore, ILocationSearch locationSearch)
    {
        this.sqliteStore = sqliteStore;
        this.locationSearch = locationSearch;
    }

    public async Task BuildDatabase(IReader reader)
    {
        Console.WriteLine("Building database...");
        await sqliteStore.InitDataStore();

        Console.WriteLine("Saving nodes...");
        await SaveNodes(reader);
        Console.WriteLine("Saving ways...");
        await SaveWays(reader);

        Console.WriteLine("Saving multipolygon relations...");
        await SaveAreas(reader);

        Console.WriteLine("Building search index...");
        BuildSearchIndex();

        Console.WriteLine("Deduplicating 3D and 2D buildings...");
        await Deduplicate3dBuildings();
    }

    private void BuildSearchIndex()
    {

        locationSearch.InitIndex();

        var searchIndexEntries = sqliteStore.FetchAreas().Where(a => !string.IsNullOrWhiteSpace(a?.Name) && a?.OuterCoordinates?.FirstOrDefault()?.FirstOrDefault() != null)
        .Select(area =>
        {
            string areaName = area.Name!;
            var coord = area?.OuterCoordinates?.FirstOrDefault()?.FirstOrDefault()!;
            return new SearchIndexEntry { Name = areaName, Lat = coord.Lat, Lon = coord.Lon };
        });
        locationSearch.UpdateIndex(searchIndexEntries.ToEnumerable());
    }

    public async Task SaveNodes(IReader reader)
    {
        var nodes = reader.IterateNodes();
        await sqliteStore.SaveNodeBatch(nodes);
    }

    public async Task SaveWays(IReader reader)
    {
        var ways = reader.IterateWays();
        await sqliteStore.SaveWayBatch(ways);
    }

    public async Task SaveAreas(IReader reader)
    {
        Console.WriteLine("Writing multi-polygon areas.");
        await SaveRelationAreas(reader.IterateRelations().Where(r => r.Tags.Any(t => t.Key == "type" && t.Value == "multipolygon")));

        Console.WriteLine("Writing single polygon areas.");
        await SaveAreaBatch(sqliteStore.FetchWays().Where(w => w.AreaParentId == null && w.ClosedLoop).ToEnumerable());

        Console.WriteLine("Writing highways.");
        await SaveHighwayAreaBatch(sqliteStore.FetchWays().Where(w => !w.ClosedLoop).ToEnumerable());
    }

    private async Task SaveRelationAreas(IEnumerable<OsmRelation> relations)
    {
        foreach (var relationBatch in relations.Chunk(1000))
        {
            var databaseAreas = new List<Area>();
            var largeTilesDict = new Dictionary<string, long[]>();
            var areaWayParentDict = new Dictionary<long, long[]>();
            var wayIds = relationBatch.SelectMany(r => r.Members).Where(m => m.Type == "way").Select(m => m.Id).Distinct();

            var waysDict = wayIds.Chunk(1000).Select(async chunk =>
            {
                var ways = await sqliteStore.FetchWays(chunk.ToArray()).ToArrayAsync();
                return ways;
            })
            .SelectMany(c => c.Result)
            .ToDictionary(k => k.Id, v => v);
            var wayNodes = wayIds.Chunk(1000).Select(async chunk =>
            {
                var wayNodes = await sqliteStore.FetchNodesByWayIds(chunk.ToArray());
                return wayNodes;
            })
            .SelectMany(c => c.Result)
            .ToDictionary();

            foreach (var relation in relationBatch)
            {
                areaWayParentDict.Add(relation.Id, relation.Members.Where(m => m.Type == "way").Select(m => m.Id).ToArray());

                var innerWayIds = relation.Members.Where(m => m.Role == "inner" && m.Type == "way").Select(m => m.Id);
                // bravely ignoring inner polygons that are not closed
                var innerWays = innerWayIds.Select(w => waysDict.GetValueOrDefault(w)!).Where(w => w != null && w.ClosedLoop).ToArray();

                // empty role is implied outer?
                var outerWayIds = relation.Members.Where(m => (m.Role == "outer" || m.Role == "") && m.Type == "way").Select(m => m.Id);
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
                else if (suggestedColour != SuggestedColourService.NO_COLOUR)
                {
                    var coords = loopCoords.ToArray();
                    var innerCoords = innerWays.Select(w => Coord.FromNodes(wayNodes[w.Id])).ToArray();
                    var largeTileResult = tileService.CalcLargeTileRange(coords);
                    var heightResult = heightService.CalcBuildingHeight(relation.Tags);
                    var tileId = tileService.CalcTileId(coords);
                    var is3d = heightService.Is3dBuilding(relation.Tags);
                    var roofInfo = RoofInfo.Default();

                    string name = relation.Tags.GetValueOrDefault("name", "");
                    int layer = 0;
                    if (int.TryParse(relation.Tags.GetValueOrDefault("layer", "0"), out int _layer))
                    {
                        layer = _layer;
                    }
                    var relationArea = new Area
                    {
                        Source = $"relation-{relation.Id}",
                        Visible = relation.Visible ?? true,
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
                        RoofColour = roofInfo.RoofColour,
                        RoofType = roofInfo.RoofType,
                        RoofHeight = roofInfo.RoofHeight,
                        IsLarge = largeTileResult.IsLarge,
                        Is3d = is3d,
                    };

                    databaseAreas.Add(relationArea);
                    if (largeTileResult.IsLarge)
                    {
                        largeTilesDict.Add(relationArea.Source, largeTileResult.Tiles.Select(t => t.Id).ToArray());
                    }
                }
            }
            await sqliteStore.SaveAreaBatch(databaseAreas);
            await sqliteStore.SaveTileAreaMap(largeTilesDict);
            await sqliteStore.SaveWayParents(areaWayParentDict);
        }
    }

    private async Task Deduplicate3dBuildings()
    {
        // TODO fix https://www.openstreetmap.org/way/172645316
        // That refers to London Hippodrome, an ordinary flat building.
        // Unforunately, it is partially intersected by https://www.openstreetmap.org/way/995954637,
        // which has a different colour. If the flat building is removed, a large chunk of the building
        // is not represented. If it stays, texture fighting happens in the app. THe polygon probably needs
        // bisecting, but that seems like a lot of work.


        var runBatch = async (IEnumerable<Area> area3dsBatch) =>
        {
            if (area3dsBatch.Count() == 0)
            {
                return 0;
            }
            var tileIds = area3dsBatch.SelectMany(a => new[] { a.TileId }.Concat(tileService.CalcAllTileIds(a.OuterCoordinates))).Distinct().OrderBy(t => t).ToArray();

            // tile id arg only referes to average tile for a building, maybe inaccuate?
            var flatAreas = await sqliteStore.FetchAreas(null, tileIds)
            .Where(a => !a.Is3d && a.Height > 0.2)
            .ToArrayAsync();

            var overlaps = new ConcurrentBag<long>();
            Parallel.ForEach(flatAreas, flatArea =>
            {
                // is 3d area contained?
                if (flatArea.OuterCoordinates[0].AreaContainsAreas(area3dsBatch.Select(a => a.OuterCoordinates[0])))
                {
                    overlaps.Add(flatArea.Id);
                    //Console.WriteLine($"    dedup {flatArea.Source}");
                }
            });
            await sqliteStore.UpdateAreaVisibility(false, overlaps.ToArray());
            return overlaps.Count;
        };

        int totalDeduplications = 0;
        var area3dsBatch = new List<Area>();
        await foreach (var area in sqliteStore.FetchAreas().Where(a => a.Is3d))
        {
            area3dsBatch.Add(area);

            if (area3dsBatch.Count > 1000)
            {
                totalDeduplications += await runBatch(area3dsBatch);
                area3dsBatch.Clear();
            }
        }
        totalDeduplications += await runBatch(area3dsBatch);
        Console.WriteLine($"Found {totalDeduplications} duplications.");
    }

    private async Task SaveAreaBatch(IEnumerable<Way> ways)
    {
        foreach (var wayBatch in ways.Chunk(1000))
        {
            var databaseAreas = new List<Area>();
            var largeTilesDict = new Dictionary<string, long[]>();
            var wayIds = wayBatch.Select(w => w.Id).ToArray();

            var wayNodes = wayIds.Chunk(1000).Select(async chunk =>
            {
                var wayNodes = await sqliteStore.FetchNodesByWayIds(chunk.ToArray());
                return wayNodes;
            })
            .SelectMany(c => c.Result)
            .ToDictionary();

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
                if (suggestedColour == SuggestedColourService.UNKNOWN_COLOUR)
                {
                    Console.WriteLine($"Way {way.Id} has an unknown colour, skipping.");
                    continue;
                }
                else if (suggestedColour == SuggestedColourService.NO_COLOUR)
                {
                    continue;
                }

                var largeTileResult = tileService.CalcLargeTileRange(orderedCoords);
                var heightResult = heightService.CalcBuildingHeight(wayTags);
                var roofInfo = heightService.FetchRoofInfo(wayTags);
                var is3d = heightService.Is3dBuilding(wayTags);

                string name = wayTags.GetValueOrDefault("name", "");
                int layer = 0;
                if (int.TryParse(wayTags.GetValueOrDefault("layer", "0"), out int _layer))
                {
                    layer = _layer;
                }
                var wayArea = new Area
                {
                    Source = $"way-{way.Id}",
                    Visible = way.Visible ?? true,
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
                    RoofColour = roofInfo.RoofColour,
                    RoofType = roofInfo.RoofType,
                    RoofHeight = roofInfo.RoofHeight,
                    IsLarge = largeTileResult.IsLarge,
                    Is3d = is3d
                };

                databaseAreas.Add(wayArea);
                if (largeTileResult.IsLarge)
                {
                    largeTilesDict.Add(wayArea.Source, largeTileResult.Tiles.Select(t => t.Id).ToArray());
                }
            }

            await sqliteStore.SaveAreaBatch(databaseAreas);
            await sqliteStore.SaveTileAreaMap(largeTilesDict);
        }
    }

    private async Task SaveHighwayAreaBatch(IEnumerable<Way> ways)
    {
        foreach (var wayBatch in ways.Chunk(1000))
        {
            var databaseAreas = new List<Area>();
            var wayIds = wayBatch.Select(w => w.Id).ToArray();

            var wayNodes = wayIds.Chunk(1000).Select(async chunk =>
            {
                var wayNodes = await sqliteStore.FetchNodesByWayIds(chunk.ToArray());
                return wayNodes;
            })
            .SelectMany(c => c.Result)
            .ToDictionary();

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
                if (suggestedColour == SuggestedColourService.UNKNOWN_COLOUR)
                {
                    Console.WriteLine($"Way {way.Id} has an unknown colour, skipping.");
                    continue;
                }
                else if (suggestedColour == SuggestedColourService.NO_COLOUR)
                {
                    continue;
                }

                var highwayCoords = highwayBuilderService.CalcHighwayCoordinates(orderedCoords, width)
                    .ToArray();
                var tile = tileService.CalcTileId(orderedCoords.Average(n => n.Lat), orderedCoords.Average(n => n.Lon));
                var roofInfo = RoofInfo.Default();

                string name = wayTags.GetValueOrDefault("name", "");
                int layer = 0;
                if (int.TryParse(wayTags.GetValueOrDefault("layer", "0"), out int _layer))
                {
                    layer = _layer;
                }
                var highwayArea = new Area
                {
                    Source = $"way-{way.Id}",
                    Visible = way.Visible ?? true,
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
                    RoofColour = roofInfo.RoofColour,
                    RoofType = roofInfo.RoofType,
                    RoofHeight = roofInfo.RoofHeight,
                    IsLarge = false,
                    Is3d = false
                };

                databaseAreas.Add(highwayArea);
            }

            await sqliteStore.SaveAreaBatch(databaseAreas);
        }
    }
}
