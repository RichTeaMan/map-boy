
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using Godot;
using MapBoy.Models;
using Http = System.Net.Http;


public record Config
{
    public required string api_url { get; set; }
    public required bool calculate_web_host { get; set; }
}

public partial class Api : GodotObject
{
    private string baseUrl = null;

    private readonly Queue<AreaContainer> areaContainerQueue = new Queue<AreaContainer>();

    private readonly Queue<TileContainer> tileContainerQueue = new Queue<TileContainer>();

    private readonly Queue<Area[]> areasQueue = new Queue<Area[]>();

    private readonly Http.HttpClient httpClient = new Http.HttpClient();

    private T DequeueOrNull<T>(Queue<T> queue) where T : class {
        if (queue.Count > 0) {
            return queue.Dequeue();
        }
        return null;
    }

    string getBase()
    {
        if (baseUrl == null)
        {
            var filepaths = new string[] {
                "res://singletons/config.dev.json",
                "res://singletons/config.web.json"
            };
            foreach (var filepath in filepaths)
            {
                if (FileAccess.FileExists(filepath))
                {
                    var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
                    var config = JsonSerializer.Deserialize<Config>(file.GetAsText());
                    baseUrl = config.api_url;
                    //print("Using API at %s" % baseUrl)
                    // TODO web version had custom logic
                    /*
                    if (config.calculate_web_host == true)
                    {
                        string host = JavaScriptBridge.eval("window.location.protocol +'//' + window.location.host");
                        baseUrl = host + baseUrl;
                    }
                    */
                    return baseUrl;
                }
                //printerr("Config not found")
            }
        }
        return baseUrl;
    }

    public void QueueGetTileIdRange(double lat1, double lon1, double lat2, double lon2)
    {
        void cb(TileContainer tileContainer)
        {
            tileContainerQueue.Enqueue(tileContainer);
        }
        queueRequest($"{getBase()}/tileIdRange/{r(lat1)}/{r(lon1)}/{r(lat2)}/{r(lon2)}", (Action<TileContainer>)cb);
    }

    public TileContainer DequeueGetTileIdRange()
    {
        return DequeueOrNull(tileContainerQueue);
    }

    public Variant DequeueGetTileIdRangeAsVariant()
    {
        var tileContainer = DequeueGetTileIdRange();
        return GodotUtils.ToGodotVariant(tileContainer);
    }

    private void queueRequest<T>(string url, Action<T> callback)
    {
        httpClient.GetAsync(url).ContinueWith(async t =>
        {
            // TODO error handling
            if (t.Result.IsSuccessStatusCode)
            {
                var content = await t.Result.Content.ReadFromJsonAsync<T>();
                callback(content);
            }
            else
            {
                GD.PrintErr($"Non 200 status '{t.Result.StatusCode}' for {url}.");
            }
        });
    }

    public void QueueGetAreaByTileId(long tileId)
    {
        void cb(AreaContainer areaContainer)
        {
            areaContainerQueue.Enqueue(areaContainer);
        }
        queueRequest($"{getBase()}/areas?tileId={tileId}", (Action<AreaContainer>)cb);
    }

    public AreaContainer DequeueGetAreaByTileId()
    {
        return DequeueOrNull(areaContainerQueue);
    }

    public Variant DequeueGetAreaByTileIdAsVariant()
    {
        var areaContainer = DequeueGetAreaByTileId();
        return GodotUtils.ToGodotVariant(areaContainer);
    }

    public void QueueGetAreaById(long areaId)
    {
        void cb(Area[] areas)
        {
            areasQueue.Enqueue(areas);
        }
        queueRequest($"{getBase()}/areasByIds?ids={areaId}", (Action<Area[]>)cb);
    }

    public Area[] DequeueGetAreaById()
    {
        return DequeueOrNull(areasQueue);
    }

    public Variant DequeueGetAreaByIdAsVariant()
    {
        var areas = DequeueGetAreaById();
        return GodotUtils.ToGodotVariant(areas);
    }

    public string search(string term)
    {
        return $"{getBase()}/search/{term}";
    }

    static string r(double value)
    {
        // c# and gdscript have different decimal places. maybe that's important?
        return Math.Round(value, 13).ToString();
    }


    protected override void Dispose(bool disposing)
    {
        // automatically release engine resources when the GC picks
        // up the managed one. this is what Dispose is intended for.
        // you don't need a separate ref counter on the engine side.
        if (disposing)
        {
            httpClient.Dispose();
        }

        base.Dispose(disposing);
    }
}
