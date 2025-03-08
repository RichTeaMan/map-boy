extends Node3D

const layer_factor = 0.1

var loaded_tiles = {}
var loaded_large_area_ids = {}

var area_queue = []
var large_area_queue: Array[int] = []
var area_pending = false
var tiles_pending = false
var last_pos_lat = null
var last_pos_long = null
var last_purge_index = 0
## Maximum number of entities to check for purging in a single tick
var purge_amount = 500

var load_window = 0.02

func _ready():
    var start = Global.lat_lon_to_vector(51.4995145764631, -0.126637687351658)
    %cameras.position.x = start.x
    %satellite_camera.position.y = 10.0
    %cameras.position.z = start.y
    
    #$Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,1,0))
    
    $areaHttpRequestPool.request_completed.connect(_on_areas_http_request_request_completed)
    $largeAreaHttpRequestPool.request_completed.connect(_on_large_areas_http_request_request_completed)
    $areasHttpRequest.request_completed.connect(_on_areas_http_request_request_completed)
    $tilesIdRangeHttpRequest.request_completed.connect(_on_tiles_http_request_request_completed)
    
    Global.teleport.connect(_on_teleport)

func _process(delta: float):
    
    # load map
    if false && !area_pending && area_queue.size() > 0:
        var tile_info = area_queue.pop_front()
        while(tile_info != null):
            if loaded_tiles.has(tile_info.tile_id):
                tile_info = area_queue.pop_front()
                continue
            #print("Requesting area for tile %s." % tile_id)
            $areasHttpRequest.request("http://127.0.0.1:5291/areas?tileId=%s" % tile_info.tile_id)
            area_pending = true
            var tile_marker = TileMarkerNode.new()
            tile_marker.tile_id = tile_info.tile_id
            tile_marker.position = tile_info.tile_position
            tile_marker.name = "tile-%s" % tile_info.tile_id
            $tile_markers.add_child(tile_marker)
            loaded_tiles[tile_info.tile_id] = true
            break
    
    while area_queue.size() > 0 && $areaHttpRequestPool.is_ready():
        var tile_info = area_queue.pop_front()
        if loaded_tiles.has(tile_info.tile_id):
            continue
        #print("Requesting area for tile %s." % tile_id)
        $areaHttpRequestPool.request_now("http://127.0.0.1:5291/areas?tileId=%s" % tile_info.tile_id)
        var tile_marker = TileMarkerNode.new()
        tile_marker.tile_id = tile_info.tile_id
        tile_marker.position = tile_info.tile_position
        tile_marker.name = "tile-%s" % tile_info.tile_id
        $tile_markers.add_child(tile_marker)
        loaded_tiles[tile_info.tile_id] = true
    
    while large_area_queue.size() > 0 && $largeAreaHttpRequestPool.is_ready():
        var large_area_id = large_area_queue.pop_front()
        if loaded_large_area_ids.has(large_area_id):
            continue
        #print("Requesting area for tile %s." % tile_id)
        $largeAreaHttpRequestPool.request_now("http://127.0.0.1:5291/areasByIds?ids=%s" % large_area_id)
        var tile_marker = TileMarkerNode.new()
        loaded_large_area_ids[large_area_id] = true
    
    # purge map
    purge_map_area_nodes()
    
    # camera movement
    if %street_camera.current:
        var player = %street_camera.get_global_transform().basis
        var forward = -player.z
        var backward = player.z
        var left = -player.x
        var right = player.x
        
        var move_factor := 25.0 * delta
        var rotate_factor := 1.0 * delta
        if Input.is_action_pressed("rotate_left"):
            %cameras.rotate_y(rotate_factor)
        if Input.is_action_pressed("rotate_right"):
            %cameras.rotate_y(-rotate_factor)
        if Input.is_action_pressed("left"):
            %cameras.position += left * move_factor
        if Input.is_action_pressed("right"):
            %cameras.position += right * move_factor
        if Input.is_action_pressed("up"):
            %cameras.position += forward * move_factor
        if Input.is_action_pressed("down"):
            %cameras.position += backward * move_factor
    else:
        var player = %satellite_camera.get_global_transform().basis
        var forward = player.y
        var backward = -player.y
        var left = -player.x
        var right = player.x
        var zoom_factor := 30.0 * delta
        var move_factor := 50.0 * delta
        var rotate_factor := 2.5 * delta
        if Input.is_action_pressed("rotate_left"):
            %cameras.rotate_y(-rotate_factor)
        if Input.is_action_pressed("rotate_right"):
            %cameras.rotate_y(rotate_factor)
        if Input.is_action_pressed("ui_up"):
            %satellite_camera.position.y -= zoom_factor
        if Input.is_action_pressed("ui_down"):
            %satellite_camera.position.y += zoom_factor
        if Input.is_action_pressed("left"):
            %cameras.position += left * move_factor
        if Input.is_action_pressed("right"):
            %cameras.position += right * move_factor
        if Input.is_action_pressed("up"):
            %cameras.position += forward * move_factor
        if Input.is_action_pressed("down"):
            %cameras.position += backward * move_factor
        
    if Input.is_action_just_pressed("camera_change"):
        var cameras = []
        for cam in %cameras.get_children():
            if cam is Camera3D:
                cameras.append(cam)
        var i = 0
        var current_cam_id = 0
        for cam in cameras:
            if cam.current:
                cam.current = false
                break
            i += 1
        var next_cam_id = (i + 1) % cameras.size()
        cameras[next_cam_id].current = true
    
    refresh_tile_queue()

func refresh_tile_queue():
    if tiles_pending:
        return
    
    # search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
    var deg_range = load_window
    #var current_lat = %cameras.position.x / Global.coord_factor
    #var current_lon = %cameras.position.z / Global.coord_factor

    var camera_coord = Global.vector_to_lat_lon(Vector2(%cameras.position.x, %cameras.position.z))
    var current_lat =  camera_coord.x
    var current_lon =  camera_coord.y
    
    if last_pos_lat == current_lat && last_pos_long == current_lon:
        return
    
    var lat1 = current_lat - deg_range
    var lon1 = current_lon - deg_range
    var lat2 = current_lat + deg_range
    var lon2 = current_lon + deg_range
    $tilesIdRangeHttpRequest.request("http://127.0.0.1:5291/tileIdRange/%s/%s/%s/%s" % [lat1, lon1, lat2, lon2])
    tiles_pending = true
    
    last_pos_lat = current_lat
    last_pos_long = current_lon

func purge_map_area_nodes():
    
    # search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
    var deg_range = load_window * Global.coord_factor * 2.0
    var current_lat = %cameras.position.x
    var current_lon = %cameras.position.z

        
    var lat1 = current_lat - deg_range
    var lon1 = current_lon - deg_range
    var lat2 = current_lat + deg_range
    var lon2 = current_lon + deg_range
    
    var map_area_nodes: Array[Node] = $map.get_children()
    var purge_limit = min(map_area_nodes.size(), last_purge_index + purge_amount)
    var i = last_purge_index
    #var min_vert: Vector2 = Vector2()
    #var max_vert: Vector2 = Vector2()
    while i < purge_limit:
        var map_area_node = map_area_nodes[i]
        if map_area_node.is_large:
            if map_area_node.max_vert.x < lat1 || map_area_node.min_vert.x > lat2 || map_area_node.max_vert.y < lon1 || map_area_node.min_vert.y > lon2:
                loaded_large_area_ids.erase(map_area_node.area_id)
                map_area_node.queue_free()
        elif map_area_node.position.x < lat1 || map_area_node.position.x > lat2 || map_area_node.position.z < lon1 || map_area_node.position.z > lon2:
            map_area_node.queue_free()
        i += 1

    last_purge_index += purge_limit
    if last_purge_index > map_area_nodes.size():
        last_purge_index = 0
    
    for tile_marker_node: Node3D in $tile_markers.get_children():
        if tile_marker_node.position.x < lat1 || tile_marker_node.position.x > lat2 || tile_marker_node.position.z < lon1 || tile_marker_node.position.z > lon2:
            tile_marker_node.queue_free()
            loaded_tiles.erase(tile_marker_node.tile_id)

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

func _on_large_areas_http_request_request_completed(_result, _response_code, _headers, body):
    var areas = JSON.parse_string(body.get_string_from_utf8())
    create_areas(areas)

func create_areas(areas):
    if areas == null:
        return
    for area in areas:
        var area_node = WayRender.create_area_node(area)
        if area_node != null:
            $map.add_child(area_node)

func _on_tiles_http_request_request_completed(_result, _response_code, _headers, body):
    #print("tile response...")
    area_queue.clear()
    $areaHttpRequestPool.clear_queue()
    var tileResponse = JSON.parse_string(body.get_string_from_utf8())
    for tile in tileResponse.tiles:
        var tile_id: int = tile.id
        if loaded_tiles.has(tile_id):
            continue
        var tile_info = { tile_id = tile_id, tile_position = Global.lat_lon_to_vector3(tile.lat, 0.0, tile.lon)}
        
        var tile_markers = $tile_markers
        var callback_fn = func():
            if loaded_tiles.has(tile_info.tile_id):
                return false
            var tile_marker = TileMarkerNode.new()
            tile_marker.tile_id = tile_info.tile_id
            tile_marker.position = tile_info.tile_position
            tile_marker.name = "tile-%s" % tile_info.tile_id
            tile_markers.add_child(tile_marker)
            loaded_tiles[tile_info.tile_id] = true
            return true
        
        area_queue.push_front(tile_info)
        #$areaHttpRequestPool.submit_front("http://127.0.0.1:5291/areas?tileId=%s" % tile_info.tile_id, callback_fn)
        

    #print("tile response processed")
    tiles_pending = false

func _on_teleport(lat: float, lon: float):
    var v2 = Global.lat_lon_to_vector(lat, lon)
    %cameras.position.x = v2.x
    %cameras.position.z = v2.y
