using OsmTool;

public class Program
{

    private static SqliteStore CreateSqliteStore()
    {
        return new SqliteStore("osm.db");
    }

    private static ILocationSearch? locationSearch;
    private static ILocationSearch CreateSearchIndex()
    {
        if (locationSearch == null)
        {
            //locationSearch = new LuceneLocationSearch("osm.index");
            locationSearch = CreateSqliteStore();
        }
        return locationSearch;
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.MapGet("/areas", async (httpContext) =>
        {
            var store = CreateSqliteStore();
            long[]? tileIds = null;
            if (httpContext.Request.Query.TryGetValue("tileId", out var tileIdStr))
            {
                tileIds = new[] { long.Parse(tileIdStr.ToString()) };
            }
            if (tileIds == null)
            {
                httpContext.Response.StatusCode = 400;
                await httpContext.Response.WriteAsJsonAsync(new { message = "tileId must be provided." });
                return;
            }
            var areas = await store.FetchAreas(null, tileIds)
                .Where(a => !a.IsLarge)
                .ToArrayAsync();
            var areaTileIds = await store.FetchAreaIdsByTileIds(tileIds).ToArrayAsync();
            var container = new AreaContainer
            {
                Areas = areas,
                LargeAreaIds = areaTileIds
            };
            await httpContext.Response.WriteAsJsonAsync(container);
        })
        .WithName("GetAreas");

        app.MapGet("/areasByIds", async (httpContext) =>
        {
            var store = CreateSqliteStore();
            long[]? areaIds = null;
            if (httpContext.Request.Query.TryGetValue("ids", out var idsStr))
            {
                areaIds = idsStr.Select(id => long.Parse(idsStr.ToString())).ToArray();
            }
            var areas = await store.FetchAreas(areaIds).ToArrayAsync();
            await httpContext.Response.WriteAsJsonAsync(areas);
        })
        .WithName("GetAreaByIds");

        app.MapGet("/tileId/{lat:double}/{lon:double}", (double lat, double lon) =>
        {
            var tileService = new TileService();
            return new { tileId = tileService.CalcTileId(lat, lon) };
        })
        .WithName("GetTileId");

        app.MapGet("/tileIdRange/{lat1:double}/{lon1:double}/{lat2:double}/{lon2:double}", (double lat1, double lon1, double lat2, double lon2) =>
        {
            var tileService = new TileService();
            return new { tiles = tileService.CalcTileIdsInRangeSpiral(lat1, lon1, lat2, lon2).Reverse().ToArray() };
        })
        .WithName("GetTileIdRange");

        app.MapGet("/search/{searchTerm}", (string searchTerm) =>
        {
            var store = CreateSearchIndex();
            return store.SearchAreas(searchTerm).Take(20).ToArray();
        })
        .WithName("SearchAreas");

        app.Run();
    }
}
