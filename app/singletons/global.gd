extends Node

var coord_factor = 40_075_000 / 360

signal teleport(lat: float, lon, float)

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
    pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
    if Input.is_action_just_pressed("ui_teleport"):
        var teleport_ui = load("res://ui/modals/teleport_modal.tscn").instantiate()
        add_child(teleport_ui)

func do_teleport(lat: float, lon: float):
    teleport.emit(lat, lon)

func lat_lon_to_vector(lat: float, lon: float) -> Vector2:
    var lon_length = 40_075_000 * cos(lat / 180.0 * PI) / 360
    var r = Vector2(lat * 111320, lon * lon_length )
    #var r = Vector2(lat, lon) * coord_factor
    return r

func vector_to_lat_lon(v: Vector2) -> Vector2:
    var lat = v.x / 111320
    var lon_length = 40_075_000 * cos(lat / 180.0 * PI) / 360
    return Vector2(lat, v.y / lon_length)
    #return Vector2(v.x / Global.coord_factor, v.y / Global.coord_factor)

func lat_lon_to_vector3(lat: float, height: float, lon: float) -> Vector3:
    var v2 = lat_lon_to_vector(lat, lon)
    return Vector3(v2.x, height, v2.y)
