class_name WayRender

static var material_map = {}

static func fetch_outline_material():
    var shader_material = material_map.get("outline-mat")
    if shader_material == null:
        shader_material = ShaderMaterial.new()
        var shader = load("res://shaders/outline.gdshader")
        shader_material.shader = shader
        material_map["outline-mat"] = shader_material
    return shader_material

static func fetch_material(colour: String):
    var material = material_map.get(colour)
    if material == null:
        material = StandardMaterial3D.new()
        material.vertex_color_use_as_albedo = true
        material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        material.albedo_color = Color.BLACK
        var cleaned_colour = colour
        if colour.begins_with("polygon-"):
            pass
            cleaned_colour = colour.trim_prefix("polygon-")
            material.next_pass = fetch_outline_material()
        
        match cleaned_colour:
            "black":
                material.albedo_color = Color.BLACK
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
            _:
                material.albedo_color = Color.html(cleaned_colour)
        material_map[colour] = material
    return material

static func create_2d_mesh_from_polygon(polygon_points: PackedVector2Array, inner_zones: Array[PackedVector2Array]) -> Array[ArrayMesh]:

    var excluded_points_collection: Array[PackedVector2Array] = [ polygon_points ]
    
    for inner_zone in inner_zones:
        var exclude_results: Array[PackedVector2Array] = []
        for excluded_points in excluded_points_collection:
            var exclude_result = Geometry2D.exclude_polygons(excluded_points, inner_zone)
            exclude_results.append_array(exclude_result)
        excluded_points_collection = exclude_results
    
    var meshes: Array[ArrayMesh] = []
    
    for excluded_points in excluded_points_collection:
        var indices = Geometry2D.triangulate_polygon(polygon_points)

        if indices.is_empty():
            printerr("Error: Triangulation failed.")
            #for p in polygon_points:
            #    print("%s, %s" % [p.x, p.y])
            continue

        var arrays = []
        arrays.resize(Mesh.ARRAY_MAX)

        var vertices = PackedVector3Array()
        for point in polygon_points:
            vertices.append(Vector3(point.x, 0, point.y))

        arrays[Mesh.ARRAY_VERTEX] = vertices
        arrays[Mesh.ARRAY_INDEX] = indices

        var mesh = ArrayMesh.new()
        mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
        meshes.append(mesh)

    return meshes

static func create_3d_mesh_from_polygon(polygon_points: PackedVector2Array, height: float) -> ArrayMesh:

    var indices = Geometry2D.triangulate_polygon(polygon_points)

    if indices.is_empty():
        printerr("Error: Triangulation failed.")
        return null

    var arrays = []
    arrays.resize(Mesh.ARRAY_MAX)

    return _generate_extruded_mesh(polygon_points, indices, height)

static func _generate_extruded_mesh(points: PackedVector2Array, triangle_indices: PackedInt32Array, extrusion_height: float) -> ArrayMesh:
    var arrays : Array = []
    arrays.resize(Mesh.ARRAY_MAX)

    var vertices : PackedVector3Array = PackedVector3Array()
    var indices : PackedInt32Array = PackedInt32Array()

    # Top face
    var top_face_start_index = vertices.size()
    for point in points:
        vertices.append(Vector3(point.x, extrusion_height, point.y))

    # Bottom face
    var bottom_face_start_index = vertices.size()
    for point in points:
        vertices.append(Vector3(point.x, 0, point.y))
    
    # Generate top face indices (clockwise)
    for tr in triangle_indices:
        indices.append(tr)

    # Generate bottom face indices (counter-clockwise)
    var rev_triangle_indices = triangle_indices.duplicate()
    rev_triangle_indices.reverse()
    for tr in rev_triangle_indices:
        indices.append(tr + bottom_face_start_index)

    # inverted triangles?
    # this seems bizarrely accuate
    var is_inverted = indices[0] > indices[1]
    
    # Generate side faces
    for i in range(points.size()):
        #break
        var next_index = (i + 1) % points.size()

        if is_inverted:
            # Side face 1
            indices.append(top_face_start_index + i)
            indices.append(bottom_face_start_index + next_index)
            indices.append(bottom_face_start_index + i)

            # Side face 2
            indices.append(top_face_start_index + i)
            indices.append(top_face_start_index + next_index)
            indices.append(bottom_face_start_index + next_index)
        else:
            # Side face 1
            indices.append(top_face_start_index + i)
            indices.append(bottom_face_start_index + i)
            indices.append(bottom_face_start_index + next_index)

            # Side face 2
            indices.append(top_face_start_index + i)
            indices.append(bottom_face_start_index + next_index)
            indices.append(top_face_start_index + next_index)

    arrays[Mesh.ARRAY_VERTEX] = vertices
    arrays[Mesh.ARRAY_INDEX] = indices

    var mesh = ArrayMesh.new()
    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
    return mesh

static func create_area_node(area) -> MapAreaNode:

    if area.outerCoordinates.size() == 0:
        printerr("Closed loop way must have at least 3 nodes.")
        return null
    if area.outerCoordinates[0].size() == 0:
        printerr("Closed loop way must have at least 3 nodes.")
        return null
    var y_position = area.height
    var area_colour = area.suggestedColour
    if area.height > 0.5:
        area_colour = "polygon-%s" % area_colour
        y_position = area.minHeight
    var position = Vector2(area.outerCoordinates[0][0].lat * Global.coord_factor, area.outerCoordinates[0][0].lon * Global.coord_factor)
    
    var map_area_node = MapAreaNode.new()
    map_area_node.name = "area-%s" % area.id
    
    var inner_zones: Array[PackedVector2Array] = []
    for coordinates in area.innerCoordinates:

        if coordinates.size() < 3:
            printerr("Closed loop inner zone must have at least 3 nodes.")
            continue
        var vertices := PackedVector2Array()
        for c in coordinates:
            var coord_vector = Vector2(c.lat * Global.coord_factor, c.lon * Global.coord_factor)
            vertices.append(coord_vector - position)
        inner_zones.append(vertices)
        if inner_zones.size() > 10:
            print("Area [%s: %s] has an excessive number of inner zones. Ignoring all of them." % [area.id, area.source])
            inner_zones.clear()
            break
    
    for coordinates in area.outerCoordinates:

        if coordinates.size() < 3:
            printerr("Closed loop way must have at least 3 nodes.")
            continue
        var vertices := PackedVector2Array()
        
        for c in coordinates:
            var coord_vector = Vector2(c.lat * Global.coord_factor, c.lon * Global.coord_factor)
            vertices.append(coord_vector - position)

        var meshes: Array[ArrayMesh] = []
        if area.height > 0.5:
            var mesh = create_3d_mesh_from_polygon(vertices, area.height - area.minHeight)
            if mesh != null:
                meshes.append(mesh)
        else:
            meshes.append_array(create_2d_mesh_from_polygon(vertices, inner_zones))

        for mesh in meshes:
            var mesh_instance = MeshInstance3D.new()
            mesh_instance.mesh = mesh
            mesh.surface_set_material(0, fetch_material(area_colour))
            map_area_node.add_child(mesh_instance)
    
    if map_area_node.get_child_count() == 0:
        return null
    
    map_area_node.area_id = area.id
    map_area_node.position = Vector3(position.x, y_position, position.y)
    map_area_node.is_large = area.isLarge
    if map_area_node.is_large:
        var min_lat = 100_000_000
        var max_lat = -100_000_000
        var min_lon = 100_000_000
        var max_lon = -100_000_000
        for coords in area.outerCoordinates:
            for c in coords:
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
