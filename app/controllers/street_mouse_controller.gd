class_name StreetMouseController
extends Controller

var look_direction_changed := false
var look_direction := Vector2.ZERO

var camera_sens := 1.0
var sens_mod := 1.0

func control(camera: Camera3D, camera_collection_node: Node3D, delta: float, viewport: Viewport) -> void:
    
    if look_direction_changed:
        camera.rotation.y -= look_direction.x * camera_sens * sens_mod
        camera.rotation.x = clamp(camera.rotation.x - look_direction.y * camera_sens * sens_mod, -1.5, 1.5)
        look_direction_changed = false

func handle_input(event: InputEvent) -> void:
    if event is InputEventMouseMotion:
        look_direction = event.relative * 0.001
        look_direction_changed = true
        #if mouse_captured: _rotate_camera()

func is_street_controller() -> bool:
    return true
