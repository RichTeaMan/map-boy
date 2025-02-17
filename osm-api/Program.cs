using OsmTool;

public class Program
{

    private static SqliteStore createSqliteStore()
    {
        return new SqliteStore("/home/tom/projects/map-boy/osm-api/osm.db");
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

        app.MapGet("/ways", async (httpContext) =>
        {
            var store = createSqliteStore();
            long[]? tileIds = null;
            if (httpContext.Request.Query.TryGetValue("tileId", out var tileIdStr))
            {
                tileIds = new[] { long.Parse(tileIdStr) };
            }

            var ways = store.FetchWays(null, tileIds);
            var resp = ways.Where(w => w.ClosedLoop).ToArray();
            await httpContext.Response.WriteAsJsonAsync(resp);
        })
        .WithName("GetWays");

        app.MapGet("/areas", async (httpContext) =>
        {
            var store = createSqliteStore();
            long[]? tileIds = null;
            if (httpContext.Request.Query.TryGetValue("tileId", out var tileIdStr))
            {
                tileIds = new[] { long.Parse(tileIdStr) };
            }
            var areas = store.FetchAreas(null, tileIds).ToArray();
            await httpContext.Response.WriteAsJsonAsync(areas);
        })
        .WithName("GetAreas");

        app.MapGet("/tileId/{lat:double}/{lon:double}", (double lat, double lon) =>
        {
            var tileService = new TileService();
            return new { tileId = tileService.CalcTileId(lat, lon) };
        })
        .WithName("GetTileId");

        app.MapGet("/tileIdRange/{lat1:double}/{lon1:double}/{lat2:double}/{lon2:double}", (double lat1, double lon1, double lat2, double lon2) =>
        {
            var tileService = new TileService();
            return new { tileIds = tileService.CalcTileIdsInRange(lat1, lon1, lat2, lon2).ToArray() };
        })
        .WithName("GetTileIdRange");




        app.Run();
    }
}
