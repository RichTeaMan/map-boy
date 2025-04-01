class_name SatelliteMouseController
extends Controller

var drag_enabled := false
var drag_point := Vector3.ZERO

func control(camera: Camera3D, camera_collection_node: Node3D, delta: float, viewport: Viewport) -> void:
    
    var zoom_factor := 30.0 * delta
    if Input.is_action_just_released("mouse_wheel_up"):
        camera.position.y -= zoom_factor
    if Input.is_action_just_released("mouse_wheel_down"):
        camera.position.y += zoom_factor

    if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
        var mouse_position = viewport.get_mouse_position()
        var mouse_position_3d = camera.project_position(mouse_position, camera.global_position.y)

        if drag_enabled:
            var drag_delta = mouse_position_3d - drag_point
            camera_collection_node.position -= drag_delta
        else:
            drag_enabled = true
            drag_point = mouse_position_3d
    else:
        drag_enabled = false

func is_satellite_controller() -> bool:
    return true
