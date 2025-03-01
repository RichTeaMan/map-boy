extends Window

var search_pending = false
var new_search_required = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
    close_requested.connect(_on_cancel_pressed)
    %btn_cancel.pressed.connect(_on_cancel_pressed)
    %input_teleport.text_changed.connect(_on_input_changed)
    %http_request.request_completed.connect(_on_search_completed)
    %input_teleport.grab_focus()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
    
    if !search_pending && new_search_required:
        new_search_required = false
        search_pending = true
        %http_request.request("http://127.0.0.1:5291/search/%s" % %input_teleport.text.strip_edges())

func _on_input_changed(_new_text) -> void:
    if %input_teleport.text.strip_edges() == "":
        return
    new_search_required = true

func parse_coord() -> Vector2:
    var text: String = %input_teleport.text
    var parts = text.split(",")
    if parts.size() >= 2:
        var lat_str = parts[0].strip_edges()
        var lon_str = parts[1].strip_edges()
        if lat_str.is_valid_float() && lon_str.is_valid_float():
            return Vector2(lat_str.to_float(), lon_str.to_float())
    return Vector2.INF

func _on_cancel_pressed():
    queue_free()

func _on_search_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray):
    search_pending = false
    var search_response = JSON.parse_string(body.get_string_from_utf8())
    for row in %result_rows.get_children():
        row.queue_free()
    var coord = parse_coord()
    if coord != Vector2.INF:
        add_row("GPS: %s, %s" % [coord.x, coord.y], coord.x, coord.y)
    if search_response == null:
        return
    for search_row in search_response:
        var name = search_row.name
        var lat = search_row.lat
        var lon = search_row.lon
        add_row(name, lat, lon)

func add_row(name: String, lat: float, lon: float):
    var ui_row = VBoxContainer.new()
    ui_row.custom_minimum_size = Vector2(0, 60)
    var ui_name = Label.new()
    ui_name.text = name
    ui_name.size_flags_horizontal = Control.SIZE_EXPAND
    ui_row.add_child(ui_name)
    var ui_button = Button.new()
    ui_row.add_child(ui_button)
    ui_button.text = "Teleport"
    var button_callback = func():
        Global.do_teleport(lat, lon)
        queue_free()
    ui_button.pressed.connect(button_callback)
    %result_rows.add_child(ui_row)
