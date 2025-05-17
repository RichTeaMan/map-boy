class_name Api

static var base = null;

static func getBase() -> String:
    if base == null:
        var filepaths = [
            "res://singletons/config.dev.json",
            "res://singletons/config.web.json"
        ]
        for filepath in filepaths:
            if FileAccess.file_exists(filepath):
                var file = FileAccess.open(filepath, FileAccess.READ)
                var config = JSON.parse_string(file.get_as_text())
                base = config.api_url
                print("Using API at %s" % base)
                if config.calculate_web_host == true:
                    var host: String = JavaScriptBridge.eval("window.location.protocol +'//' + window.location.host")
                    base = host + base
                return base
        printerr("Config not found")
    return base

static func get_tile_id_range(lat1: float, lon1: float, lat2: float, lon2: float) -> String:
    return "%s/tileIdRange/%s/%s/%s/%s" % [getBase(), lat1, lon1, lat2, lon2]


static func get_areas_by_tile_id(tile_id: int) -> String:
    return "%s/areas?tileId=%s" % [ getBase(), tile_id ]

static func get_areas_by_ids(area_id: int) -> String:
    return "%s/areasByIds?ids=%s" % [ getBase(), area_id ]

static func search(term: String) -> String:
    return "%s/search/%s" % [ getBase(), term ]
