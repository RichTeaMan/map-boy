using OsmTool;

public class Program
{

    private static SqliteStore createSqliteStore()
    {
        return new SqliteStore("osm.db");
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
            var store = createSqliteStore();
            long[]? tileIds = null;
            if (httpContext.Request.Query.TryGetValue("tileId", out var tileIdStr))
            {
                tileIds = new[] { long.Parse(tileIdStr) };
            }
            var areas = store.FetchAreas(null, tileIds)
                //.Where(a => a.SuggestedColour != "white")
                .Where(a => !a.IsLarge)
                .ToArray();
            var areaTileIds = store.FetchAreaIdsByTileIds(tileIds).ToArray();
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
            var store = createSqliteStore();
            long[]? areaIds = null;
            if (httpContext.Request.Query.TryGetValue("ids", out var idsStr))
            {
                areaIds = idsStr.Select(id => long.Parse(id)).ToArray();
            }
            var areas = store.FetchAreas(areaIds).ToArray();
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

        app.Run();
    }
}
