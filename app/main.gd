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
        for c in way.coordinates:
            if coord_count > 2000:
                break
            var lat = (c.lat - 50) * 1000
            var lon = c.lon * 1000
            
            lat_cumm += lat
            lon_cumm += lon
            coord_count += 1
            
            var instance: Node3D = scene.instantiate()
            $map.add_child(instance)
            instance.position.x = lat
            instance.position.z = lon
    var avg_lat = lat_cumm / coord_count
    var avg_lon = lon_cumm / coord_count
    $Camera3D.position.x = avg_lat
    $Camera3D.position.y = 5.0
    $Camera3D.position.z = avg_lon
    
    $Camera3D.look_at(Vector3(avg_lat, 0.0, avg_lon), Vector3(0,0,1))
    print("response processed")

func _process(delta: float) -> void:
    if Input.is_action_just_pressed("ui_up"):
        $Camera3D.position.y += 1.0
    if Input.is_action_just_pressed("ui_down"):
        $Camera3D.position.y -= 1.0
        
