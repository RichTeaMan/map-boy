class_name SatelliteKeyboardController
extends Controller

func control(camera: Camera3D, camera_collection_node: Node3D, delta: float, viewport: Viewport) -> void:
    var player = camera.get_global_transform().basis
    var forward = player.y
    var backward = -player.y
    var left = -player.x
    var right = player.x
    var zoom_factor := 30.0 * delta
    var move_factor := 50.0 * delta
    var rotate_factor := 2.5 * delta
    if Input.is_action_pressed("rotate_left"):
        camera_collection_node.rotate_y(-rotate_factor)
    if Input.is_action_pressed("rotate_right"):
        camera_collection_node.rotate_y(rotate_factor)
    if Input.is_action_pressed("ui_up"):
        camera.position.y -= zoom_factor
    if Input.is_action_pressed("ui_down"):
        camera.position.y += zoom_factor
    if Input.is_action_pressed("left"):
        camera_collection_node.position += left * move_factor
    if Input.is_action_pressed("right"):
        camera_collection_node.position += right * move_factor
    if Input.is_action_pressed("up"):
        camera_collection_node.position += forward * move_factor
    if Input.is_action_pressed("down"):
        camera_collection_node.position += backward * move_factor

func is_satellite_controller() -> bool:
    return true
