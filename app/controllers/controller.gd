class_name Controller

func control(camera: Camera3D, camera_collection_node: Node3D, delta: float, viewport: Viewport) -> void:
    pass

func handle_input(event: InputEvent) -> void:
    pass

func is_street_controller() -> bool:
    return false

func is_satellite_controller() -> bool:
    return false
