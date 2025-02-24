extends Window


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
    %btn_teleport.pressed.connect(_on_teleport_pressed)
    %btn_cancel.pressed.connect(_on_cancel_pressed)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
    pass

func _on_teleport_pressed():
    var text: String = %input_teleport.text
    var parts = text.split(",")
    if parts.size() >= 2:
        var lat_str = parts[0].strip_edges()
        var lon_str = parts[1].strip_edges()
        if lat_str.is_valid_float() && lon_str.is_valid_float():
            var lat = lat_str.to_float()
            var lon = lon_str.to_float()
            Global.do_teleport(lat, lon)
            queue_free()

func _on_cancel_pressed():
    queue_free()
