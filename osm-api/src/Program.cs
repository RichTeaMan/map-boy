using Microsoft.Extensions.FileProviders;
using OsmTool;

public class Program
{
    private static string FetchDatabasePath()
    {
        var dbFilePath = Environment.GetEnvironmentVariable("DB_FILE_PATH") ?? "osm.db";
        return dbFilePath;
    }

    private static SqliteStore CreateSqliteStore()
    {
        var dbFilePath = FetchDatabasePath();
        return new SqliteStore(dbFilePath);
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
        Console.WriteLine("Starting OSM API...");
        var builder = WebApplication.CreateBuilder(args);

        Console.WriteLine($"DB_FILE_PATH: {Environment.GetEnvironmentVariable("DB_FILE_PATH")}");
        Console.WriteLine($"Reading database from path '{FetchDatabasePath()}'.");
        if (!Path.Exists(FetchDatabasePath()))
        {
            Console.Error.WriteLine($"Database not accessible from path '{FetchDatabasePath()}', quitting.");
            return;
        }

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            // http://localhost:5291/openapi/v1.json
            app.MapOpenApi();
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "static-files")),
            ServeUnknownFileTypes = true,
            RequestPath = "",
        });

        if (Environment.GetEnvironmentVariable("ALLOW_HTTP")?.ToLower() != "true") {
            app.UseHttpsRedirection();
        }
        app.Use(async (context, next) =>
        {

            var startTime = DateTimeOffset.Now;
            await next(context);
            var endTime = DateTimeOffset.Now;
            var duration = endTime - startTime;
            Console.WriteLine($"[{DateTimeOffset.Now}] {context.Request.Path} - {context.Response.StatusCode} {duration.TotalMilliseconds:0.##}ms");
        });

        var apiPath = app.MapGroup("/api");

        apiPath.MapGet("/", async (httpContext) =>
        {
            await httpContext.Response.WriteAsync("OK");
        })
        .WithName("Home");

        apiPath.MapGet("/areas", async (httpContext) =>
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
                .Where(a => a.Visible)
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

        apiPath.MapGet("/areasByIds", async (httpContext) =>
        {
            var store = CreateSqliteStore();
            long[]? areaIds = null;
            if (httpContext.Request.Query.TryGetValue("ids", out var idsStr))
            {
                areaIds = idsStr.Select(id => long.Parse(idsStr.ToString())).ToArray();
            }
            var areas = await store.FetchAreas(areaIds).Where(a => a.Visible).ToArrayAsync();
            await httpContext.Response.WriteAsJsonAsync(areas);
        })
        .WithName("GetAreaByIds");

        apiPath.MapGet("/tileId/{lat:double}/{lon:double}", (double lat, double lon) =>
        {
            var tileService = new TileService();
            return new { tileId = tileService.CalcTileId(lat, lon) };
        })
        .WithName("GetTileId");

        apiPath.MapGet("/tileIdRange/{lat1:double}/{lon1:double}/{lat2:double}/{lon2:double}", (double lat1, double lon1, double lat2, double lon2) =>
        {
            var tileService = new TileService();
            return new { tiles = tileService.CalcTileIdsInRangeSpiral(lat1, lon1, lat2, lon2).Reverse().ToArray() };
        })
        .WithName("GetTileIdRange");

        apiPath.MapGet("/search/{searchTerm}", (string searchTerm) =>
        {
            var store = CreateSearchIndex();
            return store.SearchAreas(searchTerm).Take(20).ToArray();
        })
        .WithName("SearchAreas");

        app.Run();
    }
}
