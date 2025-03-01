using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Markup;

namespace OsmTool;

public class SuggestedColourService
{
    public const string NO_COLOUR = "no-colour";
    public const string UNKNOWN_COLOUR = "unknown";
    public string CalcSuggestedColour(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("building:colour", out string? buildingColour))
        {
            if (buildingColour.StartsWith("#"))
            {
                return buildingColour;
            }
            switch (buildingColour)
            {
                case "grey":
                    return "#808080";
                case "lightgrey":
                    return "#d3d3d3";
                case "light_brown":
                case "lightbrown":
                    return "#deb887";
                case "darkbrown":
                    return "#8b4513";
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
                case "boundary": return NO_COLOUR;
            }
        }
        if (tags.ContainsKey("indoor"))
        {
            return NO_COLOUR;
        }
        if (tags.TryGetValueFromKeys(["highway", "area:highway"], out string? highway))
        {
            switch (highway)
            {
                case "secondary":
                case "tertiary":
                case "service":
                case "residential":
                case "corridor":
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
                case "traffic_island":
                case "steps":
                    return NO_COLOUR;
            }
        }
        if (tags.ContainsKey("traffic_calming"))
        {
            return NO_COLOUR;
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
                return NO_COLOUR;
            }
        }
        if (tags.ContainsKey("building") || tags.ContainsKey("building:colour") || tags.ContainsKey("building:support") || tags.ContainsKey("building:part") || tags.ContainsKey("shop") || tags.ContainsKey("disused:shop"))
        {
            return "grey";
        }
        if (tags.ContainsKey("house"))
        {
            // how many houses are actually grey? discuss.
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
                case "water":
                    return "blue";
                case "shrubbery":
                case "scrub":
                case "meadow":
                case "heath":
                case "grassland":
                case "tree_row":
                    return "green";
                case "wood":
                    return "dark-green";
                case "wetland":
                    return "green"; // also look at wetland=reedbed
            }
        }
        if (tags.TryGetValueFromKeys(["leisure", "abandoned:leisure"], out string? leisure))
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
                case "golf_course":
                case "miniature_golf":
                case "fitness_station":
                    return "light-green";
                case "outdoor_seating":
                case "bench":
                    return NO_COLOUR;
            }

            if (leisure?.Contains("fitness_station") == true)
            {
                return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("historic", out string? historic))
        {
            switch (historic)
            {
                case "memorial":
                    return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("golf", out string? golf))
        {
            switch (golf)
            {
                case "bunker":
                    return "yellow";
                case "green":
                case "tee":
                case "fairway":
                    return "green";
            }
        }
        if (tags.TryGetValue("man_made", out string? manMade))
        {
            switch (manMade)
            {
                case "pier":
                    return "white";
                case "storage_tank":
                case "planter":
                case "shed":
                case "street_cabinet":
                case "reservoir_covered":
                case "bioswale":
                case "shaft":
                case "embankment":
                case "tunnel":
                    return NO_COLOUR;
                case "tower":
                case "train_station":
                case "bridge":
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
                case "schoolyard":
                case "kindergarten":
                case "social_facility":
                    return "light-yellow";
                case "prison":
                    return "dark-grey";
                case "parking_space":
                case "place_of_worship":
                case "fuel":
                case "fountain":
                case "car_pooling":
                case "waste_transfer_station":
                case "university":
                case "bicycle_parking":
                case "parcel_locker":
                case "fire_station":
                case "cafe":
                case "taxi":
                case "bench":
                case "grave_yard":
                case "waste_disposal":
                case "restaurant":
                    return NO_COLOUR;
            }
        }
        if (tags.ContainsKey("proposed:amenity")
            || tags.ContainsKey("proposed:construction")
            || tags.ContainsKey("proposed:building")
            || tags.ContainsKey("proposed:building:part")
            || tags.ContainsKey("planned:tourism")
            || tags.ContainsKey("was:building")
            || tags.ContainsKey("disused:amenity"))
        {
            return NO_COLOUR;
        }
        if (tags.TryGetValue("proposed:landuse", out string? proposedLanduse))
        {
            Console.WriteLine($"proposed:landuse={proposedLanduse}");
            switch (proposedLanduse)
            {
                case "construction":
                default:
                    return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("construction:leisure", out string? constructionLeisure))
        {
            Console.WriteLine($"construction:leisure={constructionLeisure}");
            switch (constructionLeisure)
            {
                case "garden":
                default:
                    return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("location", out string? location))
        {
            // TODO probably a lot of interesting details to be had here
            switch (location)
            {
                case "roof":
                    return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("generator:source", out string? generatorSource))
        {
            switch (generatorSource)
            {
                case "solar":
                default:
                    return NO_COLOUR;
            }
        }
        if (tags.TryGetValue("power", out string? power))
        {
            switch (power)
            {
                case "substation":
                default:
                    return "grey";
            }
        }
        if (tags.ContainsKey("playground") || tags.ContainsKey("sport"))
        {
            return NO_COLOUR;
        }
        if (tags.ContainsKey("razed:location") || tags.ContainsKey("demolished:building"))
        {
            return NO_COLOUR;
        }

        return UNKNOWN_COLOUR;
    }
}
