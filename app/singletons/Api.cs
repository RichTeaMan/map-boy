
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

    private Queue<AreaContainer> areaContainerQueue = new Queue<AreaContainer>();

    private Queue<Area[]> areasQueue = new Queue<Area[]>();

    private Http.HttpClient httpClient = new Http.HttpClient();

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


    public string get_tile_id_range(double lat1, double lon1, double lat2, double lon2)
    {
        return $"{getBase()}/tileIdRange/{r(lat1)}/{r(lon1)}/{r(lat2)}/{r(lon2)}";
    }

    public string get_areas_by_tile_id(long tile_id)
    {
        return $"{getBase()}/areas?tileId={tile_id}";
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
        var cb = (AreaContainer areaContainer) =>
        {
            areaContainerQueue.Enqueue(areaContainer);
        };
        queueRequest(get_areas_by_tile_id(tileId), cb);
    }

    public AreaContainer DequeueGetAreaByTileId()
    {
        if (areaContainerQueue.Count > 0)
        {
            return areaContainerQueue.Dequeue();
        }
        return null;
    }

    public Variant DequeueGetAreaByTileIdAsVariant()
    {
        var areaContainer = DequeueGetAreaByTileId();
        return GodotUtils.ToGodotVariant(areaContainer);
    }


    public string get_areas_by_ids(long area_id)
    {
        return $"{getBase()}/areasByIds?ids={area_id}";
    }

    public void QueueGetAreaById(long tileId)
    {
        var cb = (Area[] areas) =>
        {
            areasQueue.Enqueue(areas);
        };
        queueRequest(get_areas_by_ids(tileId), cb);
    }

    public Area[] DequeueGetAreaById()
    {
        if (areasQueue.Count > 0)
        {
            return areasQueue.Dequeue();
        }
        return null;
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
