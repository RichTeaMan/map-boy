using System.Data;
using System.Drawing;
using Microsoft.Data.Sqlite;
using OsmTool.Models;

namespace OsmTool;


public class SuggestedColourService
{
    public string CalcSuggestedColour(Dictionary<string, string> tags)
    {
        foreach (var (k, v) in tags)
        {
            if (k.Contains("note"))
            {
                Console.WriteLine($"'{k}' -> '{v}'");
            }
        }

        if (tags.TryGetValue("building:colour", out string? buildingColour))
        {
            if (buildingColour.StartsWith("#"))
            {
                return buildingColour;
            }
            var knownColour = Color.FromName(buildingColour);
            if (knownColour.ToArgb() != 0)
            {
                return "#" + knownColour.ToArgb().ToString("X6");
            }
            Console.WriteLine($"Irregular building:colour: {buildingColour}");
        }
        if (tags.TryGetValue("type", out string? areaType))
        {
            switch (areaType)
            {
                case "boundary": return "no-colour";
            }
        }
        if (tags.ContainsKey("indoor"))
        {
            return "no-colour";
        }
        if (tags.TryGetValue("highway", out string? highway))
        {
            switch (highway)
            {
                case "secondary":
                case "tertiary":
                case "service":
                case "residential":
                    return "white";
                case "primary":
                    return "yellow";
                case "motorway":
                case "trunk":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                    return "red";
                case "living_street":
                case "bus_guideway":
                case "raceway":
                case "road":
                case "proposed":
                case "mini_roundabout":
                case "motorway_junction":
                case "passing_place":
                case "services":
                case "stop":
                case "turning_circle":
                case "turning_loop":
                    return "red";
                case "pedestrian":
                case "footway":
                case "path":
                case "sidewalk":
                case "cycleway":
                    return "light-grey";
            }
        }
        if (tags.TryGetValue("area:highway", out string? areaHighway))
        {
            switch (areaHighway)
            {
                case "motorway":
                case "trunk":
                case "primary":
                case "secondary":
                case "tertiary":
                case "residential":
                case "service":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                case "living_street":
                case "bus_guideway":
                case "raceway":
                case "road":
                case "proposed":
                case "mini_roundabout":
                case "motorway_junction":
                case "passing_place":
                case "services":
                case "stop":
                case "turning_circle":
                case "turning_loop":
                    return "red";
                case "pedestrian":
                case "footway":
                case "path":
                case "sidewalk":
                case "cycleway":
                    return "light-grey";
                case "unclassified":
                case "traffic_island":
                    return "no-colour";
            }
        }
        if (tags.TryGetValue("railway", out string? _railwayValue))
        {
            return "black";
        }
        if (tags.TryGetValue("waterway", out string? waterway))
        {
            switch (waterway)
            {
                case "river":
                case "riverbank":
                case "stream":
                case "canal":
                case "drain":
                case "ditch":
                case "weir":
                case "dam":
                case "dock":
                case "boatyard":
                case "lock_gate":
                case "waterfall":
                case "water_point":
                case "water_slide":
                case "water_tap":
                case "water_well":
                case "watermill":
                case "waterhole":
                case "watering_place":
                case "water_works":
                    return "blue";
            }
        }
        if (tags.ContainsKey("water"))
        {
            return "blue";
        }
        if (tags.TryGetValue("building:part", out string? buildingPart))
        {
            if (buildingPart == "no")
            {
                return "no-colour";
            }
        }
        if (tags.ContainsKey("building") || tags.ContainsKey("building:colour") || tags.ContainsKey("building:part") || tags.ContainsKey("shop") || tags.ContainsKey("disused:shop"))
        {
            return "grey";
        }
        if (tags.ContainsKey("bridge:structure"))
        {
            return "grey";
        }
        if (tags.TryGetValue("landuse", out string? landUse))
        {
            switch (landUse)
            {
                case "farmland":
                    return "pale-yellow";
                case "grass":
                case "recreation_ground":
                    return "green";
                case "construction":
                    return "grey";
                case "retail":
                    return "light-red";
                case "railway":
                case "industrial":
                    return "light-purple";
                case "military":
                case "commercial":
                case "residential":

                default:
                    return "light-grey"; //???
            }
        }
        if (tags.TryGetValue("natural", out string? natural))
        {
            switch (natural)
            {
                case "water": return "blue";
                case "scrub": return "green";
                case "wood": return "dark-green";
            }
        }
        if (tags.TryGetValue("leisure", out string? leisure))
        {
            switch (leisure)
            {
                case "ice_rink":
                    return "grey";
                case "park":
                case "playground":
                case "sports_centre":
                case "stadium":
                case "swimming_pool":
                case "track":
                case "water_park":
                case "wildlife_hide":
                case "fitness_centre":
                case "golf_course":
                case "miniature_golf":
                case "recreation_ground":
                case "nature_reserve":
                case "garden":
                case "common":
                case "dog_park":
                case "horse_riding":
                    return "green";
                case "marina":
                    return "blue";
                case "slip_way":
                    return "white";
                case "pitch":
                    return "turf-green";
            }
        }
        if (tags.TryGetValue("man_made", out string? manMade))
        {
            switch (manMade)
            {
                case "pier":
                    return "white";
                case "storage_tank":
                    return "no-colour";
                case "tower":
                case "train_station":
                    return "grey";
            }
        }
        if (tags.TryGetValue("amenity", out string? amenity))
        {
            switch (amenity)
            {
                case "parking":
                    return "light-grey";
                case "school":
                    return "light-yellow";
                case "prison":
                    return "dark-grey";
            }
        }

        return "unknown";
    }
}
