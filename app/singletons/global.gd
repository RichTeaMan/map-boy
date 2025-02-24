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
