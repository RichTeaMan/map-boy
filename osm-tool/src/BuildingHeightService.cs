namespace OsmTool;

public class BuildingHeightResult
{
    public double Height { get; set; }
    public double MinHeight { get; set; }
}

public class RoofInfo
{
    public required string RoofType { get; set; }
    public double RoofHeight { get; set; }
    public required string RoofColour { get; set; }

    public required string RoofOrientation { get; set; }

    public static RoofInfo Default()
    {
        return new RoofInfo
        {
            RoofColour = "",
            RoofType = "",
            RoofHeight = 0.0,
            RoofOrientation = "",
        };
    }
}

public class BuildingHeightService
{


    public BuildingHeightResult CalcBuildingHeight(Dictionary<string, string> tags)
    {
        /*
        https://wiki.openstreetmap.org/wiki/Key:layer
        For technical reasons renderers typically give the layer tag the least weight of all considerations when determining how to draw features.

        A 2D renderer could establish a 3D model of features, filter them by relevance and visually compose the result according to 3D ordering and rendering priorities. layer=* does only affect the 3D model and should have no influence whatsoever on relevance filtering and rendering priorities (visibility).

        The 3D modeling is mostly determined by the natural (common sense) vertical ordering of features in combination with layer and level tags approximately in this order:

            natural/common sense ordering: (location=underground, tunnel) under (landcover, landuse, natural) under waterways under (highway, railway) under (man_made, building) under (bridge, location=overground, location=overhead)
            layer tag value:
                layer can only "overrule" the natural ordering of features within one particular group but not place for example a river or landuse above a bridge or an aerialway (exception: use in indoor mapping or with location tag)
                layer tags on "natural features" are frequently completely ignored
            level tag value: considered together with layer - layer models the gross placement of man made objects while level is for features within such objects.
            */

        double height = 0.0;
        double minHeight = 0.0;

        if (tags.TryGetValue("location", out string? location))
        {
            switch (location)
            {
                case "underground":
                    height = -10.0;
                    break;
                case "tunnel":
                    height = -20.0;
                    break;
            }
        }

        if (tags.TryGetValue("landuse", out string? landUse))
        {
            switch (landUse)
            {
                default:
                    height += 0.05;
                    break;
            }
        }
        if (tags.TryGetValue("natural", out string? natural))
        {
            switch (natural)
            {
                case "water":
                    height += 0.15;
                    break;
                case "wood":
                case "tree":
                    height += 4;
                    break;
            }
        }
        if (tags.TryGetValue("man_made", out string? manMade))
        {
            switch (manMade)
            {
                case "tower":
                    height += 10;
                    break;
            }
        }
        if (tags.TryGetValue("leisure", out string? leisure))
        {
            height += 0.10;
        }
        if (tags.ContainsKey("waterway") && tags.ContainsKey("water"))
        {
            height += 0.10;
        }
        if (tags.ContainsKey("building") || tags.ContainsKey("building:colour") || tags.ContainsKey("building:part"))
        {
            height += 4.5;
        }
        if (tags.ContainsKey("bridge:structure"))
        {
            height += 1.5;
        }
        if (tags.TryGetValue("building:levels", out string? buildingLevelsStr))
        {
            if (double.TryParse(buildingLevelsStr, out double buildingLevels))
            {
                height = buildingLevels * 3; // 3m suggested in https://wiki.openstreetmap.org/wiki/Key:building:levels?uselang=en-GB. not a great, but works if height is not specified
            }
        }
        if (tags.TryGetValue("min_height", out string? minHeightStr))
        {
            if (double.TryParse(minHeightStr, out double minHeightValue))
            {
                minHeight = minHeightValue;
            }
        }
        if (tags.TryGetValue("height", out string? heightStr))
        {
            if (double.TryParse(heightStr, out double heightValue))
            {
                height = heightValue;
            }
        }
        if (tags.TryGetValue("layer", out string? layer))
        {
            if (int.TryParse(layer, out int layerValue))
            {
                height += 0.01 * layerValue;
            }
        }

        return new BuildingHeightResult
        {
            Height = height,
            MinHeight = minHeight
        };
    }

    public RoofInfo FetchRoofInfo(Dictionary<string, string> tags)
    {

        string resultRoofColour = "";
        string resultRoofType = "";
        double resultRoofHeight = 0.0;
        string resultRoofOrientation = "";
        if (tags.TryGetValue("roof:colour", out string? roofColour))
        {
            resultRoofColour = roofColour;
        }

        if (tags.TryGetValue("roof:shape", out string? roofShape))
        {
            resultRoofType = roofShape;
        }

        if (tags.TryGetValue("roof:height", out string? roofHeightStr))
        {
            if (double.TryParse(roofHeightStr, out double roofHeight))
            {
                resultRoofHeight = roofHeight;
            }
        }

        if (tags.TryGetValue("roof:orientation", out string? roofOrientation))
        {
            resultRoofOrientation = roofOrientation;
        }
        return new RoofInfo
        {
            RoofColour = resultRoofColour,
            RoofType = resultRoofType,
            RoofHeight = resultRoofHeight,
            RoofOrientation = resultRoofOrientation,
        };
    }

    /// <summary>
    /// True if the given tags are for a building with 3D specfic rendering instructions.
    /// 
    /// False result may still have a height tag, but only as a general indication.
    /// </summary>
    /// <param name="tags"></param>
    /// <returns></returns>
    public bool Is3dBuilding(Dictionary<string, string> tags)
    {

        if (tags.GetValueOrDefault("building:part", "").ToLower() == "yes")
        {
            return true;
        }
        var tagNames = new string[] {
            "building:colour",
            "roof:colour", // some flat buidling seem to have this one, but removing them leaves lots of holes in the map
        };
        return tagNames.Any(tags.ContainsKey);
    }
}