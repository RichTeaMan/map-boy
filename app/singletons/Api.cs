
using System;
using System.Text.Json;
using Godot;


public record Config
{
    public required string api_url{get;set;}
    public required bool calculate_web_host{get;set;}
}

public partial class Api : GodotObject
{
    // cs url: http://localhost:5291/api/tileIdRange/51.479514576463096/-0.146637687351658/51.5195145764631/-0.106637687351658
    // gd url: http://localhost:5291/api/tileIdRange/51.4795145764631/-0.14663768735166/51.5195145764631/-0.10663768735166

    private string baseUrl = null;

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
    public string get_areas_by_ids(long area_id)
    {
        return $"{getBase()}/areasByIds?ids={area_id}";
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
}
