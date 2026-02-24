using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MapBoy.Models;

public partial class WayRender : GodotObject
{

    private Dictionary<string, Material> material_map = new Dictionary<string, Material>();


    //Mesh.PrimitiveType PRIMITIVE_TRIANGLES = Mesh.PrimitiveType.TriangleStrip;
    Mesh.PrimitiveType PRIMITIVE_TRIANGLES = Mesh.PrimitiveType.Triangles;

    private Dictionary<string, Color> colorMap = new Dictionary<string, Color> {
    { "black", Colors.Black },
    { "red", Colors.OrangeRed },
    { "blue", Colors.LightBlue },
    { "white", Colors.White },
    { "green", Colors.LightGreen },
    { "dark-green", Colors.OliveDrab },
    { "light-green", Colors.PaleGreen },
    { "grey", Colors.Gray },
    { "light-grey", Colors.LightGray },
    { "lightgrey", Colors.LightGray },
    { "yellow", Colors.Yellow },
    { "purple", Colors.Purple },
    { "light-yellow", Colors.LightYellow },
    { "turf-green", Colors.MediumSeaGreen },
    { "light-purple", Colors.Plum },
    { "light-red", Colors.LightPink },
    { "dark-grey", Colors.SlateGray },
    { "darkgrey", Colors.SlateGray },
    { "pale-yellow", Colors.Honeydew },
};

    private void print_vertor2s(IEnumerable<Vector2> coordinates)
    {
        foreach (var c in coordinates)
        {
            GD.Print($"{c.X},{c.Y}");
        }
    }

    private Material fetch_outline_material()
    {
        if (!material_map.TryGetValue("outline-mat", out Material outlineMaterial))
        {
            var shaderMaterial = new ShaderMaterial();
            var shader = GD.Load<Shader>("res://shaders/outline.gdshader");
            shaderMaterial.Shader = shader;
            outlineMaterial = shaderMaterial;
            material_map.Add("outline-mat", outlineMaterial);
        }
        return outlineMaterial;
    }
    private Material fetch_material(string colour)
    {
        if (!material_map.TryGetValue(colour, out Material material))
        {
            var standardMaterial = new StandardMaterial3D();
            standardMaterial.VertexColorUseAsAlbedo = true;
            standardMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            standardMaterial.AlbedoColor = Colors.Black;
            var cleaned_colour = colour;
            if (colour.StartsWith("polygon-"))
            {
                cleaned_colour = colour.TrimPrefix("polygon-");
                standardMaterial.NextPass = fetch_outline_material();
            }
            if (colorMap.TryGetValue(cleaned_colour, out Color colourMatch))
            {
                standardMaterial.AlbedoColor = colourMatch;
            }
            else
            {
                standardMaterial.AlbedoColor = Color.FromHtml(cleaned_colour);
                GD.PrintErr($"Warning, unknown colour: {cleaned_colour}");
            }
            material_map.Add(colour, standardMaterial);
            material = standardMaterial;
        }
        return material;
    }


    private Vector2 calc_average(IEnumerable<Vector2> polygon_points)
    { //PackedVector2Array
        var cumm = new Vector2();
        foreach (var p in polygon_points)
        {
            cumm += p;
        }
        return cumm / polygon_points.Count();
    }


    /// <summary>
    /// Calculates corners by finding 4 furthest points from the average.
    /// Is this mathematically accurate? Probably not, but we're rolling with it.
    /// 
    /// Corners are returned in the order they are orignally in the parameter.
    /// Returns empty array if something went wrong
    /// </summary>
    /// <param name="polygon_points"></param>
    /// <returns></returns>
    private Vector2[] calc_corners(Vector2[] polygon_points)
    {
        var num = 4;
        if (polygon_points.Count() < num)
        {
            GD.PrintErr($"calc_corners: Polygon does not have {num} corners.");
            return [];
        }

        var average = calc_average(polygon_points);
        var distances = new List<(double, Vector2, int)>();
        var polygon_length = polygon_points.Count();
        // head and tail are usually the same coord (always the same?)
        if (polygon_points[0] == polygon_points[polygon_length - 1])
        {
            polygon_length -= 1;
        }
        foreach (var i in Enumerable.Range(0, polygon_length))
        {
            var p = polygon_points[i];
            var distance_sqr = (p - average).LengthSquared();
            distances.Add((distance_sqr, p, i));
        }

        //distances.sort_custom(func (a,b): return a.distance_sqr > b.distance_sqr)
        var orderedDistances = distances.OrderByDescending(item => item.Item1).ToArray();

        var corner_distances = new List<(double, Vector2, int)>();
        foreach (var i in Enumerable.Range(0, num))
        {
            corner_distances.Add(orderedDistances[i]);
        }
        //corner_distances.sort_custom(func (a,b): return a.index < b.index)
        var orderedCornerDistances = corner_distances.OrderBy(item => item.Item3).ToArray();
        var corners = new List<Vector2>();
        foreach (var distance in corner_distances)
        {
            var point_index = distance.Item3;
            var point = polygon_points[point_index];
            corners.Add(point);
        }
        return corners.ToArray();
    }

    private ArrayMesh[] create_2d_mesh_from_polygon(Vector2[] polygon_points, Vector2[][] inner_zones)
    {

        Godot.Collections.Array<Vector2[]> excluded_points_collection = [polygon_points];

        foreach (var inner_zone in inner_zones)
        {
            Godot.Collections.Array<Vector2[]> exclude_results = new Godot.Collections.Array<Vector2[]>();
            foreach (var excluded_points in excluded_points_collection)
            {
                var exclude_result = Geometry2D.ExcludePolygons(excluded_points, inner_zone);
                exclude_results.AddRange(exclude_result);
            }
            excluded_points_collection = exclude_results;
        }

        ArrayMesh[] meshes = [];

        // TODO wtf is this loop for
        foreach (var excluded_points in excluded_points_collection)
        {
            var indices = Geometry2D.TriangulatePolygon(polygon_points);

            if (indices.Count() == 0)
            {
                GD.PrintErr($"Error: Triangulation 2D failed over {polygon_points.Count()} points.");
                //for p in polygon_points:
                //    print("%s, %s" % [p.x, p.y])
                continue;
            }

            Godot.Collections.Array arrays = [];
            //arrays.Resize(Mesh.ARRAY_MAX)
            arrays.Resize((int)Mesh.ArrayType.Max);

            List<Vector3> vertices = new List<Vector3>();
            foreach (var point in polygon_points)
            {
                vertices.Add(new Vector3(point.X, 0, point.Y));
            }

            //arrays[Mesh.ARRAY_VERTEX] = vertices
            //arrays[Mesh.ARRAY_INDEX] = indices
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indices;

            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(PRIMITIVE_TRIANGLES, arrays);
            meshes.Append(mesh);
        }
        return meshes;
    }

    private ArrayMesh create_3d_mesh_from_polygon(Vector2[] polygon_points, double height)
    {

        var indices = Geometry2D.TriangulatePolygon(polygon_points);

        if (indices.Count() == 0)
        {
            GD.PrintErr($"Error: Triangulation 3D failed over {polygon_points.Count()} points.");
            return null;
        }

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);

        return _generate_extruded_mesh(polygon_points, indices, height);
    }

    /// <summary>
    /// Draws an ellipse, filling the vertices and indices for a mesh. Returns points on the curved edge.
    /// Seems to work best with an odd number for subdivisions.
    /// </summary>
    private Vector3[] draw_ellipse(List<Vector3> vertices, List<int> indices, int subdivisions, double height, Vector2 a, Vector2 b)
    {
        var half_sub = subdivisions / 2;
        var jump = (b - a) / subdivisions;
        //assert(b.is_equal_approx(a + (jump * subdivisions)))

        var width_sqr = (a - b).LengthSquared();
        var width = (a - b).Length();

        var ac = width / 2.0;
        var bc = height;
        var ac_sqr = Math.Pow(ac, 2);

        var centre_point_index = vertices.Count;
        var centre_point = a + (half_sub * jump);
        vertices.Add(new Vector3(centre_point.X, 0.0, centre_point.Y));

        var result = new List<Vector3>();
        var p_start = new Vector3(a.X, 0.0, a.Y);
        result.Add(p_start);
        var roof_heights = new List<double>();
        var prev_height = 0.0;
        var prev_top_index = vertices.Count;
        vertices.Add(p_start);

        foreach (var i in Enumerable.Range(0, subdivisions - 1))
        {
            var ri = i + 1;
            var delta = ri * jump;
            var vert = a + delta;
            var h = 0.0;
            var x = ac - Math.Abs(delta.Length());
            if (ri <= half_sub)
            {
                h = bc * Math.Sqrt(ac_sqr - Math.Pow(x, 2)) / ac;
                roof_heights.Add(h);
            }
            else
            {
                // eurgh
                h = roof_heights[roof_heights.Count - 1];
                roof_heights.RemoveAt(roof_heights.Count - 1);
                //assert(h != null)
            }
            GD.Print($"[{ri}] a {x}/{ac} | h {h}/{bc}");
            var top_vert = new Vector3(vert.X, h, vert.Y);
            result.Add(top_vert);

            var top_index = vertices.Count;
            vertices.Add(top_vert);

            indices.Add(centre_point_index);
            indices.Add(prev_top_index);
            indices.Add(top_index);

            prev_height = h;
            prev_top_index = top_index;
        }

        var p_end = new Vector3(b.X, 0.0, b.Y);
        result.Add(p_end);
        indices.Add(centre_point_index);
        indices.Add(prev_top_index);
        indices.Add(vertices.Count);
        vertices.Add(p_end);
        return result.ToArray();
    }

    private ArrayMesh _generate_roof_mesh(Vector2[] polygon_points, Area area)
    {
        if (area.RoofType == "pyramidal")
        {
            var roof_vertices = new List<Vector3>();
            var centre = new Vector2();
            var is_clockwise = Geometry2D.IsPolygonClockwise(polygon_points);
            foreach (var p in polygon_points)
            {
                roof_vertices.Add(new Vector3(p.X, 0, p.Y));
                centre += p;
            }
            if (is_clockwise)
            {
                roof_vertices.Reverse();
            }
            centre = new Vector2(centre.X / polygon_points.Length, centre.Y / polygon_points.Length);
            var centre_index = roof_vertices.Count;
            roof_vertices.Append(new Vector3(centre.X, area.RoofHeight, centre.Y));
            var roof_indices = new List<int>();

            foreach (var i in Enumerable.Range(0, polygon_points.Length))
            {
                var next_index = (i + 1) % polygon_points.Length;
                roof_indices.Add(i);
                roof_indices.Add(next_index);
                roof_indices.Add(centre_index);
            }
            //var arrays : Array = []
            //arrays.resize(Mesh.ARRAY_MAX)
            //arrays[Mesh.ARRAY_VERTEX] = roof_vertices
            //arrays[Mesh.ARRAY_INDEX] = roof_indices
            //var mesh = ArrayMesh.new()
            //mesh.add_surface_from_arrays(PRIMITIVE_TRIANGLES, arrays);
            return BuildArrayMesh(roof_vertices, roof_indices);
        }
        if (area.RoofType == "round")
        {
            var corners = calc_corners(polygon_points);
            if (!Geometry2D.IsPolygonClockwise(corners))
            {
                corners.Reverse();
            }

            //var roofHeight = float(area.roofHeight)

            var s1 = (corners[0] - corners[1]).LengthSquared();
            var s2 = (corners[1] - corners[2]).LengthSquared();
            var p_index = 0;
            // along - midpoint of shortest side
            if (area.RoofOrientation == "along")
            {
                if (s1 < s2)
                {
                    p_index = 0;
                }
                else
                {
                    p_index = 1;
                }
            }
            // across - midpoint of longest side
            else if (area.RoofOrientation == "across")
            {
                if (s1 > s2)
                {
                    p_index = 0;
                }
                else
                {
                    p_index = 1;
                }
            }
            else
            {
                GD.PrintErr("Unknown roof orientation: {area.roofOrientation}");
            }

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var curve_points_1 = draw_ellipse(
                vertices,
                indices,
                7,
                area.RoofHeight,
                corners[(p_index) % corners.Length],
                corners[(p_index + 1) % corners.Length]);
            var curve_points_2 = draw_ellipse(
                vertices,
                indices,
                7,
                area.RoofHeight,
                corners[(p_index + 2) % corners.Length],
                corners[(p_index + 3) % corners.Length]);
            curve_points_2.Reverse();

            if (curve_points_1.Count() != curve_points_2.Count())
            {
                GD.PrintErr("Curves have different number of points");
                return null;
            }

            foreach (var i in Enumerable.Range(0, curve_points_1.Count() - 1))
            {
                continue;
                /* unsure why continue is there
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
                */
            }

            return BuildArrayMesh(vertices, indices);
        }
        return null;
    }

    private MeshInstance3D generate_roof(Vector2[] polygon_points, Area area)
    {
        var mesh = _generate_roof_mesh(polygon_points, area);
        if (mesh == null)
        {
            return null;
        }
        var mesh_instance = new MeshInstance3D();
        mesh_instance.Mesh = mesh;
        mesh.SurfaceSetMaterial(0, fetch_material($"polygon-{area.RoofColour}"));
        var pos = mesh_instance.Position;
        pos.Y = area.Height - (area.RoofHeight + area.MinHeight);
        mesh_instance.Position = pos;
        return mesh_instance;
    }

    private ArrayMesh _generate_extruded_mesh(Vector2[] points, int[] triangle_indices, double extrusion_height)
    {


        var vertices = new List<Vector3>(points.Length * 2);
        var indices = new List<int>((triangle_indices.Length * 2) + (points.Length * 6));

        // Top face
        var top_face_start_index = vertices.Count();
        foreach (var point in points)
        {
            vertices.Add(new Vector3(point.X, extrusion_height, point.Y));
        }

        // Bottom face
        var bottom_face_start_index = vertices.Count();
        foreach (var point in points)
        {
            vertices.Add(new Vector3(point.X, 0, point.Y));
        }

        // Generate top face indices (clockwise)
        foreach (var tr in triangle_indices)
        {
            indices.Add(tr);
        }

        // Generate bottom face indices (counter-clockwise)
        //var rev_triangle_indices = triangle_indices.duplicate()
        //rev_triangle_indices.reverse()
        foreach (var tr in triangle_indices.Reverse())
        {
            indices.Add(tr + bottom_face_start_index);
        }

        // inverted triangles?
        // this seems bizarrely accuate
        var is_inverted = indices[0] > indices[1];

        // Generate side faces
        foreach (var i in Enumerable.Range(0, points.Length))
        {
            //break
            var next_index = (i + 1) % points.Length;

            if (is_inverted)
            {
                // Side face 1
                indices.Add(top_face_start_index + i);
                indices.Add(bottom_face_start_index + next_index);
                indices.Add(bottom_face_start_index + i);

                // Side face 2
                indices.Add(top_face_start_index + i);
                indices.Add(top_face_start_index + next_index);
                indices.Add(bottom_face_start_index + next_index);
            }
            else
            {
                // Side face 1
                indices.Add(top_face_start_index + i);
                indices.Add(bottom_face_start_index + i);
                indices.Add(bottom_face_start_index + next_index);

                // Side face 2
                indices.Add(top_face_start_index + i);
                indices.Add(bottom_face_start_index + next_index);
                indices.Add(top_face_start_index + next_index);
            }
        }
        return BuildArrayMesh(vertices, indices);
    }

    public MapAreaNode create_area_node(Area area)
    {
        if (area.OuterCoordinates.Length == 0)
        {
            GD.PrintErr("Closed loop way must have at least 1 collection of outer coordinates.");
            return null;
        }
        if (area.OuterCoordinates[0].Length == 0)
        {
            GD.PrintErr("Closed loop way must have at least 3 nodes.");
            return null;
        }
        var y_position = area.Height;
        var area_colour = area.SuggestedColour;
        if (area.Height > 0.5)
        {
            area_colour = $"polygon-{area_colour}";
            y_position = area.MinHeight;
        }
        var position = c(area.OuterCoordinates[0][0].Lat, area.OuterCoordinates[0][0].Lon);

        var map_area_node = new MapAreaNode();
        map_area_node.Name = $"area_{area.Id}_[{area.Source}]";

        var inner_zones = new List<Vector2[]>();
        foreach (var coordinates in area.InnerCoordinates)
        {
            if (coordinates.Length < 3)
            {
                GD.PrintErr("Closed loop inner zone must have at least 3 nodes.");
                continue;
            }
            var vertices = new List<Vector2>();
            foreach (var coord in coordinates)
            {
                var coord_vector = c(coord.Lat, coord.Lon);
                vertices.Add(coord_vector - position);
            }
            inner_zones.Add(vertices.ToArray());
            if (inner_zones.Count > 10)
            {
                GD.Print($"Area [{area.Id}: {area.Source}] has an excessive number of inner zones. Ignoring all of them.");
                inner_zones.Clear();
                break;
            }
        }
        foreach (var coordinates in area.OuterCoordinates)
        {

            if (coordinates.Length < 3)
            {
                GD.Print("Closed loop way must have at least 3 nodes.");
                continue;
            }

            //var vertices = new List<Vector2>();
            //foreach (var coord in coordinates) {
            //    var adj = c(coord.Lat, coord.Lon) - position;
            //    //print("%s,%s"%[coord_vector.x, coord_vector.y])
            //    //print("%s,%s"%[adj.x, adj.y])
            //    vertices.Add(adj);
            //}    
            var vertices = coordinates.Select(coord => c(coord) - position).ToArray();

            var meshes = new List<ArrayMesh>();
            if (area.Height > 0.5)
            {
                var mesh = create_3d_mesh_from_polygon(vertices, area.Height - (area.RoofHeight + area.MinHeight));
                meshes.Add(mesh);
            }
            else
            {
                meshes.AddRange(create_2d_mesh_from_polygon(vertices, inner_zones.ToArray()));
            }
            foreach (var mesh in meshes)
            {
                if (mesh == null)
                {
                    continue;
                }
                var mesh_instance = new MeshInstance3D();
                mesh_instance.Mesh = mesh;
                mesh.SurfaceSetMaterial(0, fetch_material(area_colour));
                map_area_node.AddChild(mesh_instance);
            }
            var roof_node = generate_roof(vertices, area);
            if (roof_node != null)
            {
                map_area_node.AddChild(roof_node);
            }
        }

        if (map_area_node.GetChildCount() == 0)
        {
            GD.Print($"Mesh failure for area [{area.Source}]");
            return null;
        }

        map_area_node.area_id = area.Id;
        map_area_node.Position = new Vector3(position.X, y_position, position.Y);
        map_area_node.is_large = area.IsLarge;
        if (map_area_node.is_large)
        {
            var min_lat = 100_000_000d;
            var max_lat = -100_000_000d;
            var min_lon = 100_000_000d;
            var max_lon = -100_000_000d;
            foreach (var coords in area.OuterCoordinates)
            {
                foreach (var c in coords)
                {
                    if (c.Lat > max_lat)
                    {
                        max_lat = c.Lat;
                    }
                    if (c.Lat < min_lat)
                    {
                        min_lat = c.Lat;
                    }
                    if (c.Lon > max_lon)
                    {
                        max_lon = c.Lon;
                    }
                    if (c.Lon < min_lon)
                    {
                        min_lon = c.Lon;
                    }
                }
            }
            map_area_node.min_vert = c(min_lat, min_lon);
            map_area_node.max_vert = c(max_lat, max_lon);
        }
        return map_area_node;
    }


    private Vector2 c(double lat, double lon)
    {
        return Global.lat_lon_to_vector(lat, lon);
    }

    private Vector2 c(Coord coord)
    {
        return Global.lat_lon_to_vector(coord.Lat, coord.Lon);
    }

    private ArrayMesh BuildArrayMesh(IEnumerable<Vector3> vertices, IEnumerable<int> indices)
    {
        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(PRIMITIVE_TRIANGLES, arrays);
        return mesh;
    }
}
