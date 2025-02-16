extends Node3D

func _ready():
    $HTTPRequest.request_completed.connect(_on_request_completed)
    print("requesting....")
    $HTTPRequest.request("http://localhost:5291/ways")

func _on_request_completed(result, response_code, headers, body):
    print("response...")
    var scene = load("res://marker.tscn")
    var ways = JSON.parse_string(body.get_string_from_utf8())
    var lat_cumm = 0.0
    var lon_cumm = 0.0
    var coord_count = 0
    for way in ways:
        var vector_list: Array[Vector3] = []
        var vector_2d_list: Array[Vector2] = []
        for c in way.coordinates:
            #if coord_count > 2000:
            #    break
            var lat = (c.lat - 50) * 1000
            var lon = c.lon * 1000
            
            vector_list.append(Vector3(lat, 0.0, lon))
            vector_2d_list.append(Vector2(lat, lon))
            
            lat_cumm += lat
            lon_cumm += lon
            coord_count += 1
        if way.closedLoop:
            if vector_2d_list.size() == 0:
                printerr("Way %s: %s, has no coordinates." % [way.id, way.name])
                continue;
            var mesh = Draw3D.triangulate_polygon(vector_2d_list)
            if mesh != null:
                $map.add_child(mesh)
        else:
            if vector_list.size() == 0:
                printerr("Way %s: %s, has no coordinates." % [way.id, way.name])
                continue;
            var draw_3d = Draw3D.new()
            $map.add_child(draw_3d)
                
    var avg_lat = lat_cumm / coord_count
    var avg_lon = lon_cumm / coord_count
    $Camera3D.position.x = avg_lat
    $Camera3D.position.y = 5.0
    $Camera3D.position.z = avg_lon
    
    $Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,0,1))
    print("response processed")

func _process(delta: float) -> void:
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
        
