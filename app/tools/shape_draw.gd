extends Node3D


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
    pass # Replace with function body.


func _process(_delta: float):

    var zoom_factor := 0.1
    var move_factor := 0.1
    if Input.is_action_pressed("ui_up"):
        $Camera3D.position.y -= zoom_factor
    if Input.is_action_pressed("ui_down"):
        $Camera3D.position.y += zoom_factor
    if Input.is_action_pressed("left"):
        $Camera3D.position.z -= move_factor
    if Input.is_action_pressed("right"):
        $Camera3D.position.z += move_factor
    if Input.is_action_pressed("up"):
        $Camera3D.position.x += move_factor
    if Input.is_action_pressed("down"):
        $Camera3D.position.x -= move_factor


func _on_text_edit_text_changed() -> void:
    shape_draw()

func parse_coords(factor: float) -> Array[Vector2]:
    var coords: Array[Vector2] = []
    var text_edit: TextEdit = %coords_text_edit
    for line in text_edit.text.split('\n'):
        var parts = line.split(',')
        if parts.size() != 2 || line.length() == 0 || line.begins_with('#'):
            continue
        var lat_str = parts[0]
        var lon_str = parts[1]
        var lat = float(lat_str)
        var lon = float(lon_str)
        var factor_coord = Vector2(lat * factor, lon * factor)
        coords.append(factor_coord)
    return coords

func shape_draw():
    var factor = float(%factor_edit.text)
    if is_nan(factor) || factor == 0.0:
        return
    var coords := parse_coords(factor)
    var lat_cumm = 0.0
    var lon_cumm = 0.0
    
    for c in coords:
        lat_cumm += c.x
        lon_cumm += c.y
    
    if coords.size() > 0:
        $Camera3D.position.x = lat_cumm / coords.size()
        $Camera3D.position.z = lon_cumm / coords.size()
        var area = {
            "id": 0,
            "name": "test",
            "closedLoop": true,
            "suggestedColour": "yellow"
        }
        var way = {
            "id": 0,
            "name": "test",
            "closedLoop": false,
            "suggestedColour": "purple"
        }
        var area_node = WayRender.create_area_node(area, coords)
        var way_node = WayRender.create_way_node(way, coords)
        if area_node != null || way_node!= null:
            for child in %shapes.get_children():
                child.queue_free()
        if area_node != null && %area_chb.button_pressed:
            %shapes.add_child(area_node)
        if way_node != null && %lines_chb.button_pressed:
            %shapes.add_child(way_node)

func _on_factor_edit_text_changed(new_text: String) -> void:
    shape_draw()


func _on_rotate_button_pressed() -> void:
    var text_edit: TextEdit = %coords_text_edit
    var lines_packed: PackedStringArray = text_edit.text.split('\n')
    var lines: Array[String]
    lines.assign(lines_packed)
    if lines.size() == 0:
        return
    var looped = false
    if lines[0] == lines[lines.size() - 1]:
        lines.pop_back()
        looped = true
    var new_text = ""
    var first_line = ""
    lines.append(lines.pop_front())
    for line in lines:
        var stripped = line.strip_edges()
        if stripped.length() == 0:
            continue
        new_text += stripped + "\n"
        if first_line == "":
            first_line = stripped
    if looped:
        new_text += first_line
    text_edit.text = new_text


func _on_chb_toggled(toggled_on: bool) -> void:
    shape_draw()
