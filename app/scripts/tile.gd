class_name TileRef

var id: int
var lat: float
var lon: float

static func create(id, lat, lon) -> TileRef:
    var tile = TileRef.new()
    tile.Id = id
    tile.lat = lat
    tile.lon = lon
    return tile
