using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MapBoy.Models;

public partial class WayRender : GodotObject
{

    private readonly Dictionary<string, Material> materialMap = new Dictionary<string, Material>();

    private readonly Dictionary<string, Color> colorMap = new()
    {
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
        { "dark-grey", Colors.DarkGray },
        { "darkgrey", Colors.DarkGray },
        { "pale-yellow", Colors.Honeydew },
    };

    private void PrintVertor2s(IEnumerable<Vector2> coordinates)
    {
        foreach (var c in coordinates)
        {
            GD.Print($"{c.X},{c.Y}");
        }
    }

    private Material FetchOutlineMaterial()
    {
        if (!materialMap.TryGetValue("outline-mat", out Material outlineMaterial))
        {
            var shaderMaterial = new ShaderMaterial();
            var shader = GD.Load<Shader>("res://shaders/outline.gdshader");
            shaderMaterial.Shader = shader;
            outlineMaterial = shaderMaterial;
            materialMap.Add("outline-mat", outlineMaterial);
        }
        return outlineMaterial;
    }
    private Material FetchMaterial(string colour)
    {
        if (!materialMap.TryGetValue(colour, out Material material))
        {
            var standardMaterial = new StandardMaterial3D();
            standardMaterial.VertexColorUseAsAlbedo = true;
            standardMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            standardMaterial.AlbedoColor = Colors.Black;
            var cleanedColour = colour;
            if (colour.StartsWith("polygon-"))
            {
                cleanedColour = colour.TrimPrefix("polygon-");
                standardMaterial.NextPass = FetchOutlineMaterial();
            }
            if (colorMap.TryGetValue(cleanedColour, out Color colourMatch))
            {
                standardMaterial.AlbedoColor = colourMatch;
            }
            else
            {
                standardMaterial.AlbedoColor = Color.FromHtml(cleanedColour);
                GD.PrintErr($"Warning, unknown colour: {cleanedColour}");
            }
            materialMap.Add(colour, standardMaterial);
            material = standardMaterial;
        }
        return material;
    }


    private Vector2 CalculateAverage(IEnumerable<Vector2> polygonPoints)
    {
        var cumm = new Vector2();
        foreach (var p in polygonPoints)
        {
            cumm += p;
        }
        return cumm / polygonPoints.Count();
    }


    /// <summary>
    /// Calculates corners by finding 4 furthest points from the average.
    /// Is this mathematically accurate? Probably not, but we're rolling with it.
    /// 
    /// Corners are returned in the order they are orignally in the parameter.
    /// Returns empty array if something went wrong
    /// </summary>
    /// <param name="polygonPoints"></param>
    /// <returns></returns>
    private Vector2[] CalculateCorners(Vector2[] polygonPoints)
    {
        var num = 4;
        if (polygonPoints.Count() < num)
        {
            GD.PrintErr($"calc_corners: Polygon does not have {num} corners.");
            return [];
        }

        var average = CalculateAverage(polygonPoints);
        var distances = new List<(double, Vector2, int)>();
        var polygonLength = polygonPoints.Count();
        // head and tail are usually the same coord (always the same?)
        if (polygonPoints[0] == polygonPoints[polygonLength - 1])
        {
            polygonLength -= 1;
        }
        foreach (var i in Enumerable.Range(0, polygonLength))
        {
            var p = polygonPoints[i];
            var distanceSqr = (p - average).LengthSquared();
            distances.Add((distanceSqr, p, i));
        }

        //distances.sort_custom(func (a,b): return a.distance_sqr > b.distance_sqr)
        var orderedDistances = distances.OrderByDescending(item => item.Item1).ToArray();

        var cornerDistances = new List<(double, Vector2, int)>();
        foreach (var i in Enumerable.Range(0, num))
        {
            cornerDistances.Add(orderedDistances[i]);
        }
        //corner_distances.sort_custom(func (a,b): return a.index < b.index)
        var orderedCornerDistances = cornerDistances.OrderBy(item => item.Item3).ToArray();
        var corners = new List<Vector2>();
        foreach (var distance in cornerDistances)
        {
            var pointIndex = distance.Item3;
            var point = polygonPoints[pointIndex];
            corners.Add(point);
        }
        return corners.ToArray();
    }

    private ArrayMesh[] Create2dMeshFromPolygon(Vector2[] polygonPoints, Vector2[][] innerZones)
    {

        List<Vector2[]> resolvedPointsCollection = new List<Vector2[]>{ polygonPoints };

        foreach (var innerZone in innerZones)
        {
            List<Vector2[]> excludeResults = new List<Vector2[]>();
            foreach (var resolvedPoints in resolvedPointsCollection)
            {
                var excludeResult = Geometry2D.ExcludePolygons(resolvedPoints, innerZone);
                excludeResults.AddRange(excludeResult);
            }
            resolvedPointsCollection = excludeResults;
        }

        var meshes = new List<ArrayMesh>();
        foreach (var excludedPoints in resolvedPointsCollection)
        {
            var indices = Geometry2D.TriangulatePolygon(excludedPoints);
            if (indices.Count() > 0)
            {
                var vertices = excludedPoints.Select(p => new Vector3(p.X, 0, p.Y)).ToArray();
                var mesh = BuildArrayMesh(vertices, indices);
                meshes.Add(mesh);
            }
            else
            {
                GD.PrintErr($"Error: Triangulation 2D failed over {excludedPoints.Count()} points.");
            }
        }
        return meshes.ToArray();
    }

    private ArrayMesh Create3dMeshFromPolygon(Vector2[] polygonPoints, double height)
    {

        var indices = Geometry2D.TriangulatePolygon(polygonPoints);

        if (indices.Length == 0)
        {
            GD.PrintErr($"Error: Triangulation 3D failed over {polygonPoints.Count()} points.");
            return null;
        }

        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);

        return GenerateExtrudedMesh(polygonPoints, indices, height);
    }

    /// <summary>
    /// Draws an ellipse, filling the vertices and indices for a mesh. Returns points on the curved edge.
    /// Seems to work best with an odd number for subdivisions.
    /// </summary>
    private Vector3[] DrawEllipse(List<Vector3> vertices, List<int> indices, int subdivisions, double height, Vector2 a, Vector2 b)
    {
        var halfSub = subdivisions / 2;
        var jump = (b - a) / subdivisions;
        //assert(b.is_equal_approx(a + (jump * subdivisions)))

        var width = (a - b).Length();

        var ac = width / 2.0;
        var bc = height;
        var acSqr = Math.Pow(ac, 2);

        var centerPointIndex = vertices.Count;
        var centrePoint = a + (halfSub * jump);
        vertices.Add(new Vector3(centrePoint.X, 0.0, centrePoint.Y));

        var result = new List<Vector3>();
        var pStart = new Vector3(a.X, 0.0, a.Y);
        result.Add(pStart);
        var roofHeights = new List<double>();
        var prevTopIndex = vertices.Count;
        vertices.Add(pStart);

        foreach (var i in Enumerable.Range(0, subdivisions - 1))
        {
            var ri = i + 1;
            var delta = ri * jump;
            var vert = a + delta;
            var h = 0.0;
            var x = ac - Math.Abs(delta.Length());
            if (ri <= halfSub)
            {
                h = bc * Math.Sqrt(acSqr - Math.Pow(x, 2)) / ac;
                roofHeights.Add(h);
            }
            else
            {
                // eurgh
                h = roofHeights[roofHeights.Count - 1];
                roofHeights.RemoveAt(roofHeights.Count - 1);
                //assert(h != null)
            }
            GD.Print($"[{ri}] a {x}/{ac} | h {h}/{bc}");
            var topVert = new Vector3(vert.X, h, vert.Y);
            result.Add(topVert);

            var topIndex = vertices.Count;
            vertices.Add(topVert);

            indices.Add(centerPointIndex);
            indices.Add(prevTopIndex);
            indices.Add(topIndex);

            prevTopIndex = topIndex;
        }

        var pEnd = new Vector3(b.X, 0.0, b.Y);
        result.Add(pEnd);
        indices.Add(centerPointIndex);
        indices.Add(prevTopIndex);
        indices.Add(vertices.Count);
        vertices.Add(pEnd);
        return result.ToArray();
    }

    private ArrayMesh GenerateRoofMesh(Vector2[] polygonPoints, Area area)
    {
        if (area.RoofType == "pyramidal")
        {
            var roofVertices = new List<Vector3>();
            var centre = new Vector2();
            var isClockwise = Geometry2D.IsPolygonClockwise(polygonPoints);
            foreach (var p in polygonPoints)
            {
                roofVertices.Add(new Vector3(p.X, 0, p.Y));
                centre += p;
            }
            if (isClockwise)
            {
                roofVertices.Reverse();
            }
            centre = new Vector2(centre.X / polygonPoints.Length, centre.Y / polygonPoints.Length);
            var centreIndex = roofVertices.Count;
            roofVertices.Add(new Vector3(centre.X, area.RoofHeight, centre.Y));
            var roofIndices = new List<int>();

            foreach (var i in Enumerable.Range(0, polygonPoints.Length))
            {
                var nextIndex = (i + 1) % polygonPoints.Length;
                roofIndices.Add(i);
                roofIndices.Add(nextIndex);
                roofIndices.Add(centreIndex);
            }
            return BuildArrayMesh(roofVertices, roofIndices);
        }
        if (area.RoofType == "round")
        {
            var corners = CalculateCorners(polygonPoints);
            if (!Geometry2D.IsPolygonClockwise(corners))
            {
                corners.Reverse();
            }

            var s1 = (corners[0] - corners[1]).LengthSquared();
            var s2 = (corners[1] - corners[2]).LengthSquared();
            var pIndex = 0;
            // along - midpoint of shortest side
            if (area.RoofOrientation == "along")
            {
                if (s1 < s2)
                {
                    pIndex = 0;
                }
                else
                {
                    pIndex = 1;
                }
            }
            // across - midpoint of longest side
            else if (area.RoofOrientation == "across")
            {
                if (s1 > s2)
                {
                    pIndex = 0;
                }
                else
                {
                    pIndex = 1;
                }
            }
            else
            {
                GD.PrintErr("Unknown roof orientation: {area.roofOrientation}");
            }

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var curvePoints1 = DrawEllipse(
                vertices,
                indices,
                7,
                area.RoofHeight,
                corners[pIndex % corners.Length],
                corners[(pIndex + 1) % corners.Length]);
            var curvePoints2 = DrawEllipse(
                vertices,
                indices,
                7,
                area.RoofHeight,
                corners[(pIndex + 2) % corners.Length],
                corners[(pIndex + 3) % corners.Length]);
            curvePoints2.Reverse();

            if (curvePoints1.Length != curvePoints2.Length)
            {
                GD.PrintErr("Curves have different number of points");
                return null;
            }

            foreach (var i in Enumerable.Range(0, curvePoints1.Length - 1))
            {
                continue;
                /*
                // unsure why continue is there
                var baseVIndex = vertices.Count;
                var v1 = curvePoints2[i];
                var v2 = curvePoints2[i + 1];
                var v3 = curvePoints2[i];
                var v4 = curvePoints2[i + 1];

                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                vertices.Add(v4);

                indices.Add(baseVIndex);
                indices.Add(baseVIndex + 2);
                indices.Add(baseVIndex + 3);

                indices.Add(baseVIndex);
                indices.Add(baseVIndex + 3);
                indices.Add(baseVIndex + 1);
                */
            }

            return BuildArrayMesh(vertices, indices);
        }
        return null;
    }

    private MeshInstance3D GenerateRoof(Vector2[] polygonPoints, Area area)
    {
        var mesh = GenerateRoofMesh(polygonPoints, area);
        if (mesh == null)
        {
            return null;
        }
        var meshInstance = new MeshInstance3D();
        meshInstance.Mesh = mesh;
        mesh.SurfaceSetMaterial(0, FetchMaterial($"polygon-{area.RoofColour}"));
        var pos = meshInstance.Position;
        pos.Y = area.Height - (area.RoofHeight + area.MinHeight);
        meshInstance.Position = pos;
        return meshInstance;
    }

    private ArrayMesh GenerateExtrudedMesh(Vector2[] points, int[] triangleIndices, double extrusionHeight)
    {


        var vertices = new List<Vector3>(points.Length * 2);
        var indices = new List<int>((triangleIndices.Length * 2) + (points.Length * 6));

        // Top face
        var topFaceStartIndex = vertices.Count;
        foreach (var point in points)
        {
            vertices.Add(new Vector3(point.X, extrusionHeight, point.Y));
        }

        // Bottom face
        var bottomFaceStartIndex = vertices.Count;
        foreach (var point in points)
        {
            vertices.Add(new Vector3(point.X, 0, point.Y));
        }

        // Generate top face indices (clockwise)
        foreach (var tr in triangleIndices)
        {
            indices.Add(tr);
        }

        // Generate bottom face indices (counter-clockwise)
        foreach (var tr in triangleIndices.Reverse())
        {
            indices.Add(tr + bottomFaceStartIndex);
        }

        // inverted triangles?
        // this seems bizarrely accuate
        var isInverted = indices[0] > indices[1];

        // Generate side faces
        foreach (var i in Enumerable.Range(0, points.Length))
        {
            //break
            var nextIndex = (i + 1) % points.Length;

            if (isInverted)
            {
                // Side face 1
                indices.Add(topFaceStartIndex + i);
                indices.Add(bottomFaceStartIndex + nextIndex);
                indices.Add(bottomFaceStartIndex + i);

                // Side face 2
                indices.Add(topFaceStartIndex + i);
                indices.Add(topFaceStartIndex + nextIndex);
                indices.Add(bottomFaceStartIndex + nextIndex);
            }
            else
            {
                // Side face 1
                indices.Add(topFaceStartIndex + i);
                indices.Add(bottomFaceStartIndex + i);
                indices.Add(bottomFaceStartIndex + nextIndex);

                // Side face 2
                indices.Add(topFaceStartIndex + i);
                indices.Add(bottomFaceStartIndex + nextIndex);
                indices.Add(topFaceStartIndex + nextIndex);
            }
        }
        return BuildArrayMesh(vertices, indices);
    }

    public MapAreaNode CreateAreaNode(Area area)
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
        var yPosition = area.Height;
        var areaColour = area.SuggestedColour;
        if (area.Height > 0.5)
        {
            areaColour = $"polygon-{areaColour}";
            yPosition = area.MinHeight;
        }
        var position = c(area.OuterCoordinates[0][0].Lat, area.OuterCoordinates[0][0].Lon);

        var mapAreaNode = new MapAreaNode();
        mapAreaNode.Name = $"area_{area.Id}_[{area.Source}]";

        var innerZones = new List<Vector2[]>();
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
                var coordVector = c(coord.Lat, coord.Lon);
                vertices.Add(coordVector - position);
            }
            innerZones.Add(vertices.ToArray());
            if (innerZones.Count > 10)
            {
                GD.Print($"Area [{area.Id}: {area.Source}] has an excessive number of inner zones. Ignoring all of them.");
                innerZones.Clear();
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
                var mesh = Create3dMeshFromPolygon(vertices, area.Height - (area.RoofHeight + area.MinHeight));
                meshes.Add(mesh);
            }
            else
            {
                meshes.AddRange(Create2dMeshFromPolygon(vertices, innerZones.ToArray()));
            }
            foreach (var mesh in meshes)
            {
                if (mesh == null)
                {
                    continue;
                }
                var meshInstance = new MeshInstance3D();
                meshInstance.Mesh = mesh;
                mesh.SurfaceSetMaterial(0, FetchMaterial(areaColour));
                mapAreaNode.AddChild(meshInstance);
            }
            var roofNode = GenerateRoof(vertices, area);
            if (roofNode != null)
            {
                mapAreaNode.AddChild(roofNode);
            }
        }

        if (mapAreaNode.GetChildCount() == 0)
        {
            GD.Print($"Mesh failure for area [{area.Source}]");
            return null;
        }

        mapAreaNode.AreaId = area.Id;
        mapAreaNode.Position = new Vector3(position.X, yPosition, position.Y);
        mapAreaNode.IsLarge = area.IsLarge;
        if (mapAreaNode.IsLarge)
        {
            var minLat = 100_000_000d;
            var maxLat = -100_000_000d;
            var minLon = 100_000_000d;
            var maxLon = -100_000_000d;
            foreach (var coords in area.OuterCoordinates)
            {
                foreach (var c in coords)
                {
                    if (c.Lat > maxLat)
                    {
                        maxLat = c.Lat;
                    }
                    if (c.Lat < minLat)
                    {
                        minLat = c.Lat;
                    }
                    if (c.Lon > maxLon)
                    {
                        maxLon = c.Lon;
                    }
                    if (c.Lon < minLon)
                    {
                        minLon = c.Lon;
                    }
                }
            }
            mapAreaNode.MinVert = c(minLat, minLon);
            mapAreaNode.MaxVert = c(maxLat, maxLon);
        }
        return mapAreaNode;
    }


    private Vector2 c(double lat, double lon)
    {
        return Global.LatLonToVector(lat, lon);
    }

    private Vector2 c(Coord coord)
    {
        return Global.LatLonToVector(coord.Lat, coord.Lon);
    }

    private ArrayMesh BuildArrayMesh(IEnumerable<Vector3> vertices, IEnumerable<int> indices)
    {
        Godot.Collections.Array arrays = [];
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
