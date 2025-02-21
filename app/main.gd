extends Node3D

const layer_factor = 0.1

var loaded_tiles = {}

var area_queue = []
var area_pending = false
var tiles_pending = false
var last_pos_lat = null
var last_pos_long = null
var last_purge_index = 0
## Maximum number of entities to check for purging in a single tick
var purge_amount = 500

var load_window = 0.02

func _ready():
    
    var avg_lat = 51.4995145764631 * Global.coord_factor
    var avg_lon = -0.126637687351658 * Global.coord_factor
    $Camera3D.position.x = avg_lat
    $Camera3D.position.y = 10.0
    $Camera3D.position.z = avg_lon
    
    #$Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,1,0))
    
    $areasHttpRequest.request_completed.connect(_on_areas_http_request_request_completed)
    $tilesIdRangeHttpRequest.request_completed.connect(_on_tiles_http_request_request_completed)

func _process(delta: float):
    
    # load map
    if !area_pending && area_queue.size() > 0:
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
    
    # purge map
    purge_map_area_nodes()
    
    # camera movement
    
    var zoom_factor := 30.0 * delta
    var move_factor := 50.0 * delta
    if Input.is_action_pressed("ui_up"):
        $Camera3D.position.y -= zoom_factor
    if Input.is_action_pressed("ui_down"):
        $Camera3D.position.y += zoom_factor
    if Input.is_action_pressed("left"):
        $Camera3D.position.z -= move_factor
    if Input.is_action_pressed("right"):
        $Camera3D.position.z += move_factor
    if Input.is_action_pressed("up"):
        $Camera3D.position.x += move_factor
    if Input.is_action_pressed("down"):
        $Camera3D.position.x -= move_factor
    
    refresh_tile_queue()

func refresh_tile_queue():
    if tiles_pending:
        return
    
    # search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
    var deg_range = load_window
    var current_lat = $Camera3D.position.x / Global.coord_factor
    var current_lon = $Camera3D.position.z / Global.coord_factor
    
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
    var current_lat = $Camera3D.position.x
    var current_lon = $Camera3D.position.z
    
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
    var areas = JSON.parse_string(body.get_string_from_utf8())
    if areas != null:
        for area in areas:
            #if area.suggestedColour == "white" && area.layer >= 0:
            #    print("Area is white: %s" % area.id)
            #    continue
            var vector_2d_list := PackedVector2Array()
            var r = Vector2(area.coordinates[0].lat * Global.coord_factor, area.coordinates[0].lon * Global.coord_factor)
            for c in area.coordinates:
                var coord_vector = Vector2(c.lat * Global.coord_factor, c.lon * Global.coord_factor)
                vector_2d_list.append(coord_vector - r)
            var area_node = WayRender.create_area_node(area, r, vector_2d_list)
            if area_node != null:
                $map.add_child(area_node)
                #if area.suggestedColour == "red" || area.suggestedColour == "dark-green" || area.suggestedColour == "grey" || area.suggestedColour == "light-grey":
                #    area_node.position.y += 0.05
                area_node.position.y = area.height

    #print("area response processed")
    area_pending = false

func _on_tiles_http_request_request_completed(_result, _response_code, _headers, body):
    #print("tile response...")
    area_queue.clear()
    var tileResponse = JSON.parse_string(body.get_string_from_utf8())
    for tile in tileResponse.tiles:
        var tile_id: int = tile.id
        if loaded_tiles.has(tile_id):
            continue
        var tile_info = { tile_id = tile_id, tile_position = Vector3(tile.lat * Global.coord_factor, 0.0, tile.lon * Global.coord_factor)}
        area_queue.push_front(tile_info)

    #print("tile response processed")
    tiles_pending = false
