class_name WayRender

static var material_map = {}

static func print_vertor2s(coordinates: Array[Vector2]):
    for c in coordinates:
        print("%s,%s"%[c.x, c.y])

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
                material.albedo_color = Color.WHITE
            "green":
                material.albedo_color = Color.LIGHT_GREEN
            "dark-green":
                material.albedo_color = Color.OLIVE_DRAB
            "light-green":
                material.albedo_color = Color.PALE_GREEN
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

static func calc_average(polygon_points: PackedVector2Array) -> Vector2:
    var cumm = Vector2()
    for p in polygon_points:
        cumm += p
    return cumm / polygon_points.size()

## Calculates corners by finding 4 furthest points from the average.
## Is this mathematically accurate? Probably not, but we're rolling with it.
## 
## Corners are returned in the order they are orignally in the parameter.
## Returns empty array if something went wrong
static func calc_corners(polygon_points: PackedVector2Array) -> PackedVector2Array:
    var num = 4
    if polygon_points.size() < num:
        printerr("calc_corners: Polygon does not have %s corners." % num)
        return PackedVector2Array()
    var average = calc_average(polygon_points)
    var distances = []
    var polygon_length = polygon_points.size()
    # head and tail are ususally the same coord (always the same?)
    if polygon_points[0] == polygon_points[polygon_length - 1]:
        polygon_length -= 1
    for i in range(polygon_length):
        var p = polygon_points[i]
        var distance_sqr = (p - average).length_squared()
        distances.append({
            "distance_sqr": distance_sqr,
            "point": p,
            "index": i
        })
    
    distances.sort_custom(func (a,b): return a.distance_sqr > b.distance_sqr)
    var corner_distances = []
    var corners = PackedVector2Array()
    for i in range(num):
        corner_distances.append(distances[i])
    corner_distances.sort_custom(func (a,b): return a.index < b.index)
    for distance in corner_distances:
        var point_index = distance.index
        var point = polygon_points[point_index]
        corners.append(point)
    return corners

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
            printerr("Error: Triangulation failed over %s points." % polygon_points.size())
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
        printerr("Error: Triangulation failed over %s points." % polygon_points.size())
        return null

    var arrays = []
    arrays.resize(Mesh.ARRAY_MAX)

    return _generate_extruded_mesh(polygon_points, indices, height)

## Draws an ellipse, filling the vertices and indices for a mesh. Returns points on the curved edge.
## Seems to work best with an odd number for subdivisions.
static func draw_ellipse(vertices: PackedVector3Array, indices: PackedInt32Array, subdivisions: int, height: float, a: Vector2, b: Vector2) -> Array[Vector3]:
    var half_sub = subdivisions / 2
    var jump: Vector2 = (b - a) / subdivisions
    assert(b.is_equal_approx(a + (jump * subdivisions)))
    
    var width_sqr = (a - b).length_squared()
    var width = (a - b).length()
    
    var ac := float(width / 2.0)
    var bc := float(height)
    var ac_sqr := pow(ac, 2)
    
    var centre_point_index = vertices.size()
    var centre_point: Vector2 = a + (half_sub * jump)
    vertices.append(Vector3(centre_point.x, 0.0, centre_point.y))
    
    var result: Array[Vector3] = []
    var p_start = Vector3(a.x, 0.0, a.y)
    result.append(p_start)
    var roof_heights: Array[float] = []
    var prev_height = 0.0
    var prev_top_index = vertices.size()
    vertices.append(p_start)
    
    for i in range(subdivisions - 1):
        var ri = i + 1
        var delta: Vector2 = ri * jump
        var vert: Vector2 = a + delta
        var h = 0.0
        var x = ac - abs(delta.length())
        if ri <= half_sub:
            h = ( bc * sqrt(ac_sqr - pow(x, 2)) ) / ac
            roof_heights.append(h)
        else:
            h = roof_heights.pop_back()
            assert(h != null)
        print("[%s] a %s/%s | h %s/%s" % [ri, x, ac, h, bc])
        var top_vert = Vector3(vert.x, h, vert.y)
        result.append(top_vert)
        
        var top_index = vertices.size()
        vertices.append(top_vert)
        
        indices.append(centre_point_index)
        indices.append(prev_top_index)
        indices.append(top_index)
        
        prev_height = h
        prev_top_index = top_index

    var p_end = Vector3(b.x, 0.0, b.y)
    result.append(p_end)
    indices.append(centre_point_index)
    indices.append(prev_top_index)
    indices.append(vertices.size())
    vertices.append(p_end)
    return result


static func _generate_roof_mesh(polygon_points: PackedVector2Array, area) -> ArrayMesh:
    if (area.roofType == "pyramidal"):
        var roof_vertices = PackedVector3Array()
        var centre = Vector2()
        var is_clockwise = Geometry2D.is_polygon_clockwise(polygon_points)
        for p in polygon_points:
            roof_vertices.append(Vector3(p.x, 0, p.y))
            centre += p
        if is_clockwise:
            roof_vertices.reverse()
        centre = Vector2(centre.x / polygon_points.size(), centre.y / polygon_points.size())
        var centre_index = roof_vertices.size()
        roof_vertices.append(Vector3(centre.x, area.roofHeight, centre.y))
        var roof_indices = PackedInt32Array()
        
        for i in range(polygon_points.size()):
            var next_index = (i + 1) % polygon_points.size()
            roof_indices.append(i)
            roof_indices.append(next_index)
            roof_indices.append(centre_index)
        var arrays : Array = []
        arrays.resize(Mesh.ARRAY_MAX)
        arrays[Mesh.ARRAY_VERTEX] = roof_vertices
        arrays[Mesh.ARRAY_INDEX] = roof_indices
        var mesh = ArrayMesh.new()
        mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
        return mesh
    if (area.roofType == "round"):
        var corners := calc_corners(polygon_points)
        if !Geometry2D.is_polygon_clockwise(corners):
            corners.reverse()
        
        var roofHeight = float(area.roofHeight)
        
        var s1 = (corners[0] - corners[1]).length_squared()
        var s2 = (corners[1] - corners[2]).length_squared()
        var p_index = 0
        # along - midpoint of shortest side
        if area.roofOrientation == "along":
            if s1 < s2:
                p_index = 0
            else:
                p_index = 1
        # across - midpoint of longest side
        elif area.roofOrientation == "across":
            if s1 > s2:
                p_index = 0
            else:
                p_index = 1
        else:
            printerr("Unknown roof orientation: %s" % area.roofOrientation)
        
        var vertices := PackedVector3Array()
        var indices := PackedInt32Array()
        var curve_points_1 = draw_ellipse(
            vertices,
            indices,
            7,
            roofHeight,
            corners[(p_index) % corners.size()],
            corners[(p_index + 1) % corners.size()])
        var curve_points_2 = draw_ellipse(
            vertices,
            indices,
            7,
            roofHeight,
            corners[(p_index + 2) % corners.size()],
            corners[(p_index + 3) % corners.size()])
        curve_points_2.reverse()
        
        if curve_points_1.size() != curve_points_2.size():
            printerr("Curves have different number of points")
            return null
        
        for i in range(curve_points_1.size() - 1):
            continue
            var base_v_index = vertices.size()
            var v1 = curve_points_1[i]
            var v2 = curve_points_1[i + 1]
            var v3 = curve_points_2[i]
            var v4 = curve_points_2[i + 1]
            
            vertices.append(v1)
            vertices.append(v2)
            vertices.append(v3)
            vertices.append(v4)
            
            indices.append(base_v_index)
            indices.append(base_v_index + 2)
            indices.append(base_v_index + 3)
            
            indices.append(base_v_index)
            indices.append(base_v_index + 3)
            indices.append(base_v_index + 1)
        
        var arrays : Array = []
        arrays.resize(Mesh.ARRAY_MAX)
        arrays[Mesh.ARRAY_VERTEX] = vertices
        arrays[Mesh.ARRAY_INDEX] = indices
        var mesh = ArrayMesh.new()
        mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
        return mesh
    return null

static func generate_roof(polygon_points: PackedVector2Array, area):
    var mesh = _generate_roof_mesh(polygon_points, area)
    if mesh == null:
        return null
    var mesh_instance = MeshInstance3D.new()
    mesh_instance.mesh = mesh
    mesh.surface_set_material(0, fetch_material("polygon-%s" % area.roofColour))
    mesh_instance.position.y = area.height - (area.roofHeight + area.minHeight)
    return mesh_instance

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
        printerr("Closed loop way must have at least 1 collection of outer coordinates.")
        return null
    if area.outerCoordinates[0].size() == 0:
        printerr("Closed loop way must have at least 3 nodes.")
        return null
    var y_position = area.height
    var area_colour = area.suggestedColour
    if area.height > 0.5:
        area_colour = "polygon-%s" % area_colour
        y_position = area.minHeight
    var position = c(area.outerCoordinates[0][0].lat, area.outerCoordinates[0][0].lon)
    
    var map_area_node = MapAreaNode.new()
    map_area_node.name = "area_%s_[%s]" % [ area.id, area.source ]
    
    var inner_zones: Array[PackedVector2Array] = []
    for coordinates in area.innerCoordinates:
        if coordinates.size() < 3:
            printerr("Closed loop inner zone must have at least 3 nodes.")
            continue
        var vertices := PackedVector2Array()
        for c in coordinates:
            var coord_vector = c(c.lat, c.lon)
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
            var adj = c(c.lat, c.lon) - position
            #print("%s,%s"%[coord_vector.x, coord_vector.y])
            #print("%s,%s"%[adj.x, adj.y])
            vertices.append(adj)
            
                

        var meshes: Array[ArrayMesh] = []
        if area.height > 0.5:
            var mesh = create_3d_mesh_from_polygon(vertices, area.height - (area.roofHeight + area.minHeight))
            meshes.append(mesh)
        else:
            meshes.append_array(create_2d_mesh_from_polygon(vertices, inner_zones))

        for mesh in meshes:
            if mesh == null:
                continue
            var mesh_instance = MeshInstance3D.new()
            mesh_instance.mesh = mesh
            mesh.surface_set_material(0, fetch_material(area_colour))
            map_area_node.add_child(mesh_instance)
        var roof_node = generate_roof(vertices, area)
        if roof_node != null:
            map_area_node.add_child(roof_node)
    
    if map_area_node.get_child_count() == 0:
        print("Mesh failure for area [%s]" % area.source)
        for coll in area.outerCoordinates:
            for c in coll:
                pass
                #print("%s,%s"%[c.lat, c.lon])
            #print("---")
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
        map_area_node.min_vert = c(min_lat, min_lon)
        map_area_node.max_vert = c(max_lat, max_lon)
    return map_area_node

static func c(lat: float, lon: float) -> Vector2:
    return Global.lat_lon_to_vector(lat, lon)
