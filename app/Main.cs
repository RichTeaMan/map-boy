using System.Collections.Generic;
using System.Linq;
using Godot;
using MapBoy.Models;

public partial class Main : Node3D
{
    private double layerFactor = 0.1;

    private ControlScheme[] controlSchemes = [];
    private ControlScheme currentControlScheme;

    private readonly HashSet<long> loadedTiles = new HashSet<long>();
    private readonly HashSet<long> loadedLargeAreaIds = new HashSet<long>();

    private Queue<long> areaQueue = new Queue<long>();
    private Queue<long> largeAreaQueue = new Queue<long>();
    private bool areaPending = false;
    private bool tilesPending = false;
    private double? lastPosLat = null;
    private double? lastPosLong = null;
    private int lastPurgeIndex = 0;

    /// <summary>
    /// Maximum number of entities to check for purging in a single tick
    /// </summary>
    private int purgeAmount = 5;

    private Global global;

    private Node3D TileMarkers => GetNode<Node3D>("tile_markers");

    private Node3D Cameras => GetNode<Node3D>("%cameras");

    private Node3D Map => GetNode<Node3D>("map");

    private Node3D Furniture => GetNode<Node3D>("furniture");

    private Api api = new Api();

    private WayRender wayRender = new WayRender();

    private FurnitureFactory furnitureFactory = new FurnitureFactory();

    public override void _Ready()
    {
        global = GetNode<Global>("/root/Global");

        var streetScheme = new ControlScheme();
        streetScheme.Camera = GetNode<Camera3D>("%street_camera");
        streetScheme.Controllers = [
            new StreetKeyboardController(),
            new StreetMouseController()
        ];
        streetScheme.LocksMouse = true;
        var satelliteScheme = new ControlScheme();
        satelliteScheme.Camera = GetNode<Camera3D>("%satellite_camera");
        satelliteScheme.Controllers = [
            new SatelliteKeyboardController(),
            new SatelliteMouseController(),
        ];
        satelliteScheme.LocksMouse = false;
        controlSchemes = [
            streetScheme,
            satelliteScheme
        ];
        SwitchToControlScheme(streetScheme);

        var start = Global.LatLonToVector(51.4995145764631, -0.126637687351658);
        GetNode<Node3D>("%cameras").Position = new Vector3(start.X, 0.0, start.Y);
        var statCamPos = satelliteScheme.Camera.Position;
        satelliteScheme.Camera.Position = new Vector3(statCamPos.X, 10.0, statCamPos.Z);

        global.ConnectTeleport(new Callable(this, MethodName.OnTeleport));
    }

    public override void _Process(double delta)
    {
        // load map
        while (areaQueue.Count > 0)
        {
            var tileId = areaQueue.Dequeue();
            if (loadedTiles.Contains(tileId))
            {
                continue;
            }
            api.QueueGetAreaByTileId(tileId);
            loadedTiles.Add(tileId);
        }
        while (largeAreaQueue.Count > 0)
        {
            var largeAreaId = largeAreaQueue.Dequeue();
            if (loadedLargeAreaIds.Contains(largeAreaId))
            {
                continue;
            }
            api.QueueGetAreaById(largeAreaId);
            loadedLargeAreaIds.Add(largeAreaId);
        }

        while (true)
        {
            var response = api.DequeueGetAreaByTileId();
            if (response == null)
            {
                break;
            }
            OnAreasCompleted(response);
        }

        while (true)
        {
            var response = api.DequeueGetAreaById();
            if (response == null)
            {
                break;
            }
            CreateAreas(response);
        }

        while (true)
        {
            var response = api.DequeueGetTileIdRange();
            if (response == null)
            {
                break;
            }
            OnTilesHttpRequestCompleted(response);
        }

        while (true)
        {
            var response = api.DequeueGetFurnitureByTileId();
            if (response == null)
            {
                break;
            }
            OnFurnitureHttpRequestCompleted(response);
        }

        // purge map
        PurgeMapAreaNodes();

        if (!global.IsTextFocused) {
            // camera movement
            foreach (var controller in currentControlScheme.Controllers)
            {
                controller.Control(currentControlScheme.Camera, Cameras, delta, GetViewport());
            }

            if (Input.IsActionJustPressed("camera_change"))
            {
                var currentSchemeId = controlSchemes.Index().First(c => c.Item == currentControlScheme).Index;
                var nextSchemeId = (currentSchemeId + 1) % controlSchemes.Count();
                var nextScheme = controlSchemes[nextSchemeId];
                SwitchToControlScheme(nextScheme);
            }
        }
        RefreshTileQueue();
    }

    private void SwitchToControlScheme(ControlScheme newControlScheme)
    {
        foreach (var controlScheme in controlSchemes)
        {
            controlScheme.Camera.Current = false;
        }
        newControlScheme.Camera.Current = true;
        if (newControlScheme.LocksMouse)
        {
            global.CaptureMouse();
        }
        else
        {
            global.ReleaseMouse();
        }
        currentControlScheme = newControlScheme;
    }

    public override void _Input(InputEvent inputEvent)
    {
        base._Input(inputEvent);
        foreach (var controller in currentControlScheme.Controllers)
        {
            controller.HandleInput(inputEvent);
        }
    }

    private void RefreshTileQueue()
    {
        if (tilesPending)
        {
            return;
        }

        
        var degRange = Config.Fetch().LoadWindow;

        var cameraCoord = global.VectorToLatLon(new Vector2(Cameras.Position.X, Cameras.Position.Z));
        var currentLat = cameraCoord.X;
        var currentLon = cameraCoord.Y;

        if (lastPosLat == currentLat && lastPosLong == currentLon)
        {
            return;
        }

        var lat1 = currentLat - degRange;
        var lon1 = currentLon - degRange;
        var lat2 = currentLat + degRange;
        var lon2 = currentLon + degRange;
        api.QueueGetTileIdRange(lat1, lon1, lat2, lon2);
        tilesPending = true;

        lastPosLat = currentLat;
        lastPosLong = currentLon;
    }

    private void PurgeMapAreaNodes()
    {
        // search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
        var degRange = Config.Fetch().LoadWindow * global.coordFactor * 2.0;
        var current_lat = Cameras.Position.X;
        var current_lon = Cameras.Position.Z;


        var lat1 = current_lat - degRange;
        var lon1 = current_lon - degRange;
        var lat2 = current_lat + degRange;
        var lon2 = current_lon + degRange;

        var mapAreaNodes = Map.GetChildren().Select(c => c as MapAreaNode).Where(c => c != null).ToArray();
        var purgeLimit = Mathf.Min(mapAreaNodes.Count(), lastPurgeIndex + purgeAmount);
        var i = lastPurgeIndex;
        while (i < purgeLimit)
        {
            var mapAreaNode = mapAreaNodes[i];
            if (mapAreaNode.IsLarge)
            {
                if (mapAreaNode.MaxVert.X < lat1 || mapAreaNode.MinVert.X > lat2 || mapAreaNode.MaxVert.Y < lon1 || mapAreaNode.MinVert.Y > lon2)
                {
                    loadedLargeAreaIds.Remove(mapAreaNode.AreaId);
                    mapAreaNode.QueueFree();
                }
            }
            else if (mapAreaNode.Position.X < lat1 || mapAreaNode.Position.X > lat2 || mapAreaNode.Position.Z < lon1 || mapAreaNode.Position.Z > lon2)
            {
                mapAreaNode.QueueFree();
            }
            i += 1;
        }

        lastPurgeIndex += purgeLimit;
        if (lastPurgeIndex > mapAreaNodes.Length)
        {
            lastPurgeIndex = 0;
        }
        foreach (var tileMarkerNode in TileMarkers.GetChildren().Cast<TileMarkerNode>())
        {
            if (tileMarkerNode.Position.X < lat1 || tileMarkerNode.Position.X > lat2 || tileMarkerNode.Position.Z < lon1 || tileMarkerNode.Position.Z > lon2)
            {
                tileMarkerNode.QueueFree();
                loadedTiles.Remove(tileMarkerNode.TileId);
            }
        }
    }

    private void OnAreasCompleted(AreaContainer areaResponse)
    {
        var areas = areaResponse.Areas;
        foreach (var largeAreaId in areaResponse.LargeAreaIds)
        {
            if (!loadedLargeAreaIds.Contains(largeAreaId))
            {
                largeAreaQueue.Enqueue(largeAreaId);
            }
        }
        CreateAreas(areas);
        areaPending = false;
    }

    private void CreateAreas(Area[] areas)
    {
        if (areas == null)
        {
            return;
        }
        foreach (var area in areas)
        {
            MapAreaNode areaNode = wayRender.CreateAreaNode(area);
            if (areaNode != null)
            {
                Map.AddChild(areaNode);
            }
        }
    }

    private void OnTilesHttpRequestCompleted(TileContainer tileResponse)
    {
        areaQueue.Clear();
        foreach (var tile in tileResponse.Tiles)
        {
            if (loadedTiles.Contains(tile.Id))
            {
                continue;
            }
            areaQueue.Enqueue(tile.Id);
            api.QueueGetFurnitureByTileId(tile.Id);
        }
        //print("tile response processed")
        tilesPending = false;
    }

    private void OnFurnitureHttpRequestCompleted(Furniture[] furnitures)
    {
        foreach (var furniture in furnitures)
        {
            if (!loadedTiles.Contains(furniture.TileId))
            {
                continue;
            }
            var furnitureNode = furnitureFactory.CreateFurniture(furniture, global);
            if (furnitureNode != null)
            {
                Furniture.AddChild(furnitureNode);
            }
        }
    }

    private void OnTeleport(double lat, double lon)
    {
        var v2 = Global.LatLonToVector(lat, lon);
        var position = Cameras.Position;
        Cameras.Position = new Vector3(v2.X, position.Y, v2.Y);
    }
}
