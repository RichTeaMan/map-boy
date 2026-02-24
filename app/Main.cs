using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MapBoy.Models;

public partial class Main : Node3D
{

    double layer_factor = 0.1;

    ControlScheme[] control_schemes = [];
    ControlScheme current_control_scheme;

    private HashSet<long> loaded_tiles = new HashSet<long>();
    private HashSet<long> loaded_large_area_ids = new HashSet<long>();

    private Queue<long> area_queue = new Queue<long>();
    private Queue<long> large_area_queue = new Queue<long>();
    bool area_pending = false;
    bool tiles_pending = false;
    double? last_pos_lat = null;
    double? last_pos_long = null;
    int last_purge_index = 0;

    /// <summary>
    /// Maximum number of entities to check for purging in a single tick
    /// </summary>
    int purge_amount = 5;

    double load_window = 0.02;

    private Global global;

    private Node3D TileMarkers => GetNode<Node3D>("tile_markers");

    private Node3D Cameras => GetNode<Node3D>("%cameras");

    private Node3D Map => GetNode<Node3D>("map");

    private Api api = new Api();

    private WayRender wayRender = new WayRender();

    public override void _Ready()
    {
        global = GetNode<Global>("/root/Global");

        var street_scheme = new ControlScheme();
        street_scheme.Camera = GetNode<Camera3D>("%street_camera");
        street_scheme.Controllers = [
            new StreetKeyboardController(),
            new StreetMouseController()
        ];
        street_scheme.LocksMouse = true;
        var satellite_scheme = new ControlScheme();
        satellite_scheme.Camera = GetNode<Camera3D>("%satellite_camera");
        satellite_scheme.Controllers = [
            new SatelliteKeyboardController(),
            new SatelliteMouseController(),
        ];
        satellite_scheme.LocksMouse = false;
        control_schemes = [
            street_scheme,
            satellite_scheme
        ];
        switch_to_control_scheme(street_scheme);

        var start = Global.lat_lon_to_vector(51.4995145764631, -0.126637687351658);
        GetNode<Node3D>("%cameras").Position = new Vector3(start.X, 0.0, start.Y);
        var statCamPos = satellite_scheme.Camera.Position;
        satellite_scheme.Camera.Position = new Vector3(statCamPos.X, 10.0, statCamPos.Z);
        //%cameras.position.x = start.x
        //%satellite_camera.position.y = 10.0
        //%cameras.position.z = start.y

        //$Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,1,0))

        //$areaHttpRequestPool.request_completed.connect(_on_areas_http_request_request_completed)
        //$largeAreaHttpRequestPool.request_completed.connect(_on_large_areas_http_request_request_completed)
        //$areasHttpRequest.request_completed.connect(_on_areas_http_request_request_completed)
        //$tilesIdRangeHttpRequest.request_completed.connect(_on_tiles_http_request_request_completed)


        global.ConnectTeleport(new Callable(this, MethodName._on_teleport));
    }

    public override void _Process(double delta)
    {

        // load map
        while (area_queue.Count > 0)
        { // : # && $areaHttpRequestPool.is_ready()) {
            var tileId = area_queue.Dequeue();
            if (loaded_tiles.Contains(tileId))
            {
                continue;
            }
            //print("Requesting area for tile %s." % tile_id)
            //$areaHttpRequestPool.request_now(api.get_areas_by_tile_id(tile_info.tile_id))
            api.QueueGetAreaByTileId(tileId);


            //var tile_marker = new TileMarkerNode();
            //tile_marker.tile_id = tile_info.tile_id;
            //tile_marker.position = tile_info.tile_position;
            //tile_marker.name = $"tile-{tile_info.tile_id}";
            //TileMarkers.AddChild(tile_marker);
            loaded_tiles.Add(tileId);
        }
        while (large_area_queue.Count > 0)
        { //: # && $largeAreaHttpRequestPool.is_ready():
            var large_area_id = large_area_queue.Dequeue();
            if (loaded_large_area_ids.Contains(large_area_id))
            {
                continue;
            }
            //print("Requesting area for tile %s." % tile_id)
            //$largeAreaHttpRequestPool.request_now(api.get_areas_by_ids(large_area_id))
            //var tile_marker = TileMarkerNode.new()
            api.QueueGetAreaById(large_area_id);
            loaded_large_area_ids.Add(large_area_id);
        }

        while (true)
        {
            var response = api.DequeueGetAreaByTileId();
            if (response == null)
            {
                break;
            }
            _on_areas_completed(response);
        }

        while (true)
        {
            var response = api.DequeueGetAreaById();
            if (response == null)
            {
                break;
            }
            create_areas(response);
        }

        while (true)
        {
            var response = api.DequeueGetTileIdRange();
            if (response == null)
            {
                break;
            }
            _on_tiles_http_request_request_completed(response);
        }

        // purge map
        purge_map_area_nodes();

        // camera movement
        foreach (var controller in current_control_scheme.Controllers)
        {
            controller.Control(current_control_scheme.Camera, Cameras, delta, GetViewport());
        }

        if (Input.IsActionJustPressed("camera_change"))
        {
            var current_scheme_id = control_schemes.Index().First(c => c.Item == current_control_scheme).Index;
            var next_scheme_id = (current_scheme_id + 1) % control_schemes.Count();
            var next_scheme = control_schemes[next_scheme_id];
            switch_to_control_scheme(next_scheme);
        }
        refresh_tile_queue();
    }

    private void switch_to_control_scheme(ControlScheme new_control_scheme)
    {
        foreach (var control_scheme in control_schemes)
        {
            control_scheme.Camera.Current = false;
        }
        new_control_scheme.Camera.Current = true;
        if (new_control_scheme.LocksMouse)
        {
            global.capture_mouse();
        }
        else
        {
            global.release_mouse();
        }
        current_control_scheme = new_control_scheme;
    }

    public override void _Input(InputEvent inputEvent)
    {
        base._Input(inputEvent);
        //var is_street_mode = GetNode<Camera3D>("%street_camera").Current;
        //var is_satellite_mode = GetNode<Camera3D>("%satellite_camera").Current;
        foreach (var controller in current_control_scheme.Controllers)
        {
            controller.HandleInput(inputEvent);
        }
    }

    private void refresh_tile_queue()
    {
        if (tiles_pending)
        {
            return;
        }

        // search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
        var deg_range = load_window;
        //var current_lat = %cameras.position.x / Global.coord_factor
        //var current_lon = %cameras.position.z / Global.coord_factor

        var camera_coord = global.vector_to_lat_lon(new Vector2(Cameras.Position.X, Cameras.Position.Z));
        var current_lat = camera_coord.X;
        var current_lon = camera_coord.Y;

        if (last_pos_lat == current_lat && last_pos_long == current_lon)
        {
            return;
        }

        var lat1 = current_lat - deg_range;
        var lon1 = current_lon - deg_range;
        var lat2 = current_lat + deg_range;
        var lon2 = current_lon + deg_range;
        //$tilesIdRangeHttpRequest.request(api.get_tile_id_range(lat1, lon1, lat2, lon2))
        api.QueueGetTileIdRange(lat1, lon1, lat2, lon2);
        tiles_pending = true;

        last_pos_lat = current_lat;
        last_pos_long = current_lon;
    }

    private void purge_map_area_nodes()
    {
        // search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
        var deg_range = load_window * global.coord_factor * 2.0;
        var current_lat = Cameras.Position.X;
        var current_lon = Cameras.Position.Z;


        var lat1 = current_lat - deg_range;
        var lon1 = current_lon - deg_range;
        var lat2 = current_lat + deg_range;
        var lon2 = current_lon + deg_range;

        var map_area_nodes = Map.GetChildren().Cast<MapAreaNode>().ToArray();
        var purge_limit = Mathf.Min(map_area_nodes.Count(), last_purge_index + purge_amount);
        var i = last_purge_index;
        //var min_vert: Vector2 = Vector2()
        //var max_vert: Vector2 = Vector2()
        while (i < purge_limit)
        {
            var map_area_node = map_area_nodes[i];
            if (map_area_node.is_large)
            {
                if (map_area_node.max_vert.X < lat1 || map_area_node.min_vert.X > lat2 || map_area_node.max_vert.Y < lon1 || map_area_node.min_vert.Y > lon2)
                {
                    loaded_large_area_ids.Remove(map_area_node.area_id);
                    map_area_node.QueueFree();
                }
            }
            else if (map_area_node.Position.X < lat1 || map_area_node.Position.X > lat2 || map_area_node.Position.Z < lon1 || map_area_node.Position.Z > lon2)
            {
                map_area_node.QueueFree();
            }
            i += 1;
        }

        last_purge_index += purge_limit;
        if (last_purge_index > map_area_nodes.Count())
        {
            last_purge_index = 0;
        }
        foreach (var tile_marker_node in TileMarkers.GetChildren().Cast<TileMarkerNode>())
        {
            if (tile_marker_node.Position.X < lat1 || tile_marker_node.Position.X > lat2 || tile_marker_node.Position.Z < lon1 || tile_marker_node.Position.Z > lon2)
            {
                tile_marker_node.QueueFree();
                loaded_tiles.Remove(tile_marker_node.TileId);
            }
        }
    }
    /*
    func _on_areas_http_request_request_completed(_result, _response_code, _headers, body):
        #print("area response...")
        var area_response = JSON.parse_string(body.get_string_from_utf8())
        var areas = area_response.areas
        var large_area_ids = area_response.largeAreaIds
        for large_area_id: int in large_area_ids:
            if !loaded_large_area_ids.has(large_area_id):
                large_area_queue.append(large_area_id)
        create_areas(areas)

        #print("area response processed")
        area_pending = false
    */

    private void _on_areas_completed(AreaContainer area_response)
    {
        //print("area response...")
        var areas = area_response.Areas;
        var large_area_ids = area_response.LargeAreaIds;
        foreach (var large_area_id in large_area_ids)
        {
            if (!loaded_large_area_ids.Contains(large_area_id))
            {
                large_area_queue.Enqueue(large_area_id);
            }
        }
        create_areas(areas);

        //print("area response processed")
        area_pending = false;
    }

    /*
    func _on_large_areas_http_request_request_completed(_result, _response_code, _headers, body):
        var areas = JSON.parse_string(body.get_string_from_utf8())
        create_areas(areas)
    */

    private void create_areas(Area[] areas)
    {
        if (areas == null)
        {
            return;
        }
        foreach (var area in areas)
        {
            MapAreaNode area_node = wayRender.create_area_node(area);
            if (area_node != null)
            {
                Map.AddChild(area_node);
            }
        }
    }


    private void _on_tiles_http_request_request_completed(TileContainer tileResponse)
    {
        //print("tile response...")
        area_queue.Clear();
        //$areaHttpRequestPool.clear_queue()
        foreach (var tile in tileResponse.Tiles)
        {
            var tile_id = tile.Id;
            if (loaded_tiles.Contains(tile_id))
            {
                continue;
            }
            area_queue.Enqueue(tile_id);
        }
        //print("tile response processed")
        tiles_pending = false;
    }

    private void _on_teleport(double lat, double lon)
    {
        var v2 = Global.lat_lon_to_vector(lat, lon);
        var position = Cameras.Position;
        Cameras.Position = new Vector3(v2.X, position.Y, v2.Y);
    }
}