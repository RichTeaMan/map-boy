extends Node3D

const coord_factor = 1000

var loaded_tiles = []

var way_queue = []
var area_queue = []
var way_pending = false
var area_pending = false
var tiles_pending = false
var last_pos_lat = null
var last_pos_long = null

func _ready():
    
    var avg_lat = 51.4995145764631 * coord_factor
    var avg_lon = -0.126637687351658 * coord_factor
    $Camera3D.position.x = avg_lat
    $Camera3D.position.y = 5.0
    $Camera3D.position.z = avg_lon
    
    $Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,1,0))
    
    $waysHttpRequest.request_completed.connect(_on_ways_http_request_request_completed)
    $areasHttpRequest.request_completed.connect(_on_areas_http_request_request_completed)
    $tilesIdRangeHttpRequest.request_completed.connect(_on_tiles_http_request_request_completed)

func _process(_delta: float):
    
    if !way_pending && way_queue.size() > 0:
        way_pending = true
        var tile_id = way_queue.pop_front()
        print("Requesting ways for tile %s." % tile_id)
        $waysHttpRequest.request("http://127.0.0.1:5291/ways?tileId=%s" % tile_id)
        
    if !area_pending && area_queue.size() > 0:
        area_pending = true
        var tile_id = area_queue.pop_front()
        print("Requesting area for tile %s." % tile_id)
        $areasHttpRequest.request("http://127.0.0.1:5291/areas?tileId=%s" % tile_id)
    
    # camera movement
    
    var zoom_factor := 0.1
    var move_factor := 0.1
    if Input.is_action_pressed("ui_up"):
        $Camera3D.position.y -= zoom_factor
    if Input.is_action_pressed("ui_down"):
        $Camera3D.position.y += zoom_factor
    if Input.is_action_pressed("left"):
        $Camera3D.position.x += move_factor
    if Input.is_action_pressed("right"):
        $Camera3D.position.x -= move_factor
    if Input.is_action_pressed("up"):
        $Camera3D.position.z += move_factor
    if Input.is_action_pressed("down"):
        $Camera3D.position.z -= move_factor
    
    refresh_tile_queue()


func refresh_tile_queue():
    if tiles_pending:
        return
    
    # search for tiles 0.1 degrees around camera postion, which is very roughly similar to 1.7km
    var range = 0.01
    var current_lat = $Camera3D.position.x / coord_factor
    var current_lon = $Camera3D.position.z / coord_factor
    
    if last_pos_lat == current_lat && last_pos_long == current_lon:
        return
    
    var lat1 = current_lat - range
    var lon1 = current_lon - range
    var lat2 = current_lat + range
    var lon2 = current_lon + range
    $tilesIdRangeHttpRequest.request("http://127.0.0.1:5291/tileIdRange/%s/%s/%s/%s" % [lat1, lon1, lat2, lon2])
    tiles_pending = true
    
    last_pos_lat = current_lat
    last_pos_long = current_lon

func _on_ways_http_request_request_completed(result, response_code, headers, body):
    print("ways response...")
    var ways = JSON.parse_string(body.get_string_from_utf8())
    var lat_cumm = 0.0
    var lon_cumm = 0.0
    var coord_count = 0
    if ways != null:
        for way in ways:
            var vector_list: Array[Vector3] = []
            var vector_2d_list: Array[Vector2] = []
            for c in way.coordinates:
                var lat = c.lat * coord_factor
                var lon = c.lon * coord_factor
                
                vector_list.append(Vector3(lat, 0.0, lon))
                vector_2d_list.append(Vector2(lat, lon))
                
                lat_cumm += lat
                lon_cumm += lon
                coord_count += 1
            var way_node = WayRender.create_way_node(way, vector_2d_list)
            if way_node != null:
                $map.add_child(way_node)
                
    #var avg_lat = lat_cumm / coord_count
    #var avg_lon = lon_cumm / coord_count
    #$Camera3D.position.x = avg_lat
    #$Camera3D.position.y = 5.0
    #$Camera3D.position.z = avg_lon
    
    #$Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,0,1))
    print("ways response processed")
    way_pending = false

func _on_areas_http_request_request_completed(result, response_code, headers, body):
    print("area response...")
    var areas = JSON.parse_string(body.get_string_from_utf8())
    var lat_cumm = 0.0
    var lon_cumm = 0.0
    var coord_count = 0
    if areas != null:
        for area in areas:
            var vector_list: Array[Vector3] = []
            var vector_2d_list: Array[Vector2] = []
            for c in area.coordinates:
                var lat = c.lat * coord_factor
                var lon = c.lon * coord_factor
                
                vector_list.append(Vector3(lat, 0.0, lon))
                vector_2d_list.append(Vector2(lat, lon))
                
                lat_cumm += lat
                lon_cumm += lon
                coord_count += 1
            var area_node = WayRender.create_area_node(area, vector_2d_list)
            if area_node != null:
                $map.add_child(area_node)
                
    #var avg_lat = lat_cumm / coord_count
    #var avg_lon = lon_cumm / coord_count
    #$Camera3D.position.x = avg_lat
    #$Camera3D.position.y = 5.0
    #$Camera3D.position.z = avg_lon
    
    #$Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,0,1))
    print("area response processed")
    area_pending = false

func _on_tiles_http_request_request_completed(result, response_code, headers, body):
    print("tile response...")
    var tileResponse = JSON.parse_string(body.get_string_from_utf8())
    for tileId in tileResponse.tileIds:
        if loaded_tiles.has(tileId):
            continue
        way_queue.append(tileId)
        area_queue.append(tileId)
        loaded_tiles.append(tileId)
    print("tile response processed")
    tiles_pending = false
