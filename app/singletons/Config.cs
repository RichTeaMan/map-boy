using System;
using System.Linq;
using System.Text.Json;
using Godot;

public class Config
{
    public string ApiUrl { get; set; }
    public bool CalculateWebHost { get; set; }

    private string _baseUrl = null;
    public string BaseUrl
    {
        get
        {
            if (_baseUrl == null)
            {
                _baseUrl = ApiUrl;
                if (CalculateWebHost)
                {
                    string host = JavaScriptBridge.Eval("window.location.protocol +'//' + window.location.host").AsString();
                    _baseUrl = host + ApiUrl;
                }
            }
            return _baseUrl;
        }
    }

    /// <summary>
    /// Gets or sets the degrees around the camera position that map data will be loaded for. 1 degree is around 17km.
    /// </summary>
    public double LoadWindow { get; set; } = 0.02;

    private static Config _config = null;

    public static Config Fetch()
    {
        if (_config == null)
        {
            // the order is the priority they'll be used
            var filepaths = new string[]
            {
                "res://singletons/config.dev.json",
                "res://singletons/config.web.json"
            };
            var filepath = filepaths.FirstOrDefault(FileAccess.FileExists) ?? throw new Exception("Config not found.");
            var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
            _config = JsonSerializer.Deserialize<Config>(file.GetAsText());
        }
        return _config;
    }
}
