class_name WayRender

static var material_map = {}

static func fetch_material(colour: String):
    var material = material_map.get(colour)
    if material == null:
        material = StandardMaterial3D.new()
        material.vertex_color_use_as_albedo = true
        material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        material.albedo_color = Color.YELLOW
        match colour:
            "red":
                material.albedo_color = Color.ORANGE_RED
            "blue":
                material.albedo_color = Color.LIGHT_BLUE
            "white":
                material.albedo_color = Color.FLORAL_WHITE
            "green":
                material.albedo_color = Color.LIGHT_GREEN
            "dark-green":
                material.albedo_color = Color.OLIVE_DRAB
            "grey":
                material.albedo_color = Color.GRAY
            "light-grey":
                material.albedo_color = Color.LIGHT_GRAY
            "yellow":
                material.albedo_color = Color.YELLOW
            "purple":
                material.albedo_color = Color.PURPLE
            "light-yellow":
                material.albedo_color = Color.LIGHT_YELLOW
            "turf-green":
                material.albedo_color = Color.MEDIUM_SEA_GREEN
            "light-purple":
                material.albedo_color = Color.PLUM
            "light-red":
                material.albedo_color = Color.LIGHT_PINK
            "dark-grey":
                material.albedo_color = Color.SLATE_GRAY
            "pale-yellow":
                material.albedo_color = Color.HONEYDEW
        material_map[colour] = material
    return material

static func create_mesh_from_polygon(polygon_points: PackedVector2Array):
        
    #var relative_v := PackedVector2Array()
    #var r = polygon_points[0]
    #for v in polygon_points:
    #    var new_v = v - r
    #    relative_v.append(new_v)
    var indices = Geometry2D.triangulate_polygon(polygon_points)

    if indices.is_empty():
        printerr("Error: Triangulation failed.")
        return null

    var arrays = []
    arrays.resize(Mesh.ARRAY_MAX)

    var vertices = PackedVector3Array()
    for point in polygon_points:
        vertices.append(Vector3(point.x, 0, point.y))

    arrays[Mesh.ARRAY_VERTEX] = vertices
    arrays[Mesh.ARRAY_INDEX] = indices

    var mesh = ArrayMesh.new()
    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

    return mesh

static func create_area_node(area, position: Vector2, vertices: PackedVector2Array) -> MapAreaNode:
    if vertices.size() == 0:
        printerr("Area %s: %s, has no coordinates." % [area.id, area.name])
        return null
    if vertices.size() < 3:
        printerr("Closed loop way must have at least 3 nodes.")
        return null;
    var mesh = create_mesh_from_polygon(vertices)
    if mesh == null: # Handle the triangulation error
        return
    
    

    var mesh_instance = MeshInstance3D.new()
    mesh_instance.mesh = mesh
    mesh.surface_set_material(0, fetch_material(area.suggestedColour))
    var map_area_node = MapAreaNode.new()
    map_area_node.name = "area-%s" % area.id
    map_area_node.add_child(mesh_instance)
    map_area_node.position = Vector3(position.x, 0.0, position.y)
    map_area_node.is_large = area.isLarge
    if map_area_node.is_large:
        var min_lat = 100_000_000
        var max_lat = -100_000_000
        var min_lon = 100_000_000
        var max_lon = -100_000_000
        for c in area.coordinates:
            if c.lat > max_lat:
                max_lat = c.lat
            if c.lat < min_lat:
                min_lat = c.lat
            if c.lon > max_lon:
                max_lon = c.lon
            if c.lon < min_lon:
                min_lon = c.lon
        map_area_node.min_vert = Vector2(Global.coord_factor * min_lat, Global.coord_factor * min_lon)
        map_area_node.max_vert = Vector2(Global.coord_factor * max_lat, Global.coord_factor * max_lon)
    return map_area_node
