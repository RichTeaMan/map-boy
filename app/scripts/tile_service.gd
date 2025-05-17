
class_name TileService


const LAT_DEGREE_DIVISION = 200
const LON_DEGREE_DIVISION = 200

const WIDTH = LAT_DEGREE_DIVISION * 360;
const HEIGHT = LON_DEGREE_DIVISION * 360;

func CalcTileIdFromLatLon(lat: float, lon: float):

    var col = CalcRowColumn(lat, lon)
    var latTile: int = col.latTile
    var lonTile: int = col.lonTile

    var tile_id = lonTile * WIDTH + latTile;
    return tile_id;

func CalcTileId(coord: Coord) -> int:
    return CalcTileIdFromLatLon(coord.lat, coord.lon)

func CalcTileIds(coords: Array[Coord]) -> int:
    var avg_lat := ext.average_float(coords.map(func(e): return e.lat))
    var avg_lon := ext.average_float(coords.map(func(e): return e.lon))
    return CalcTileIdFromLatLon(avg_lat, avg_lon)

func CalcTileIdMatrix(coords: Array) -> int: # coords: Array[Array[Coord]]
    return CalcTileIds(ext.select_many(coords))

func CalcAllTileIds(coords: Array[Coord]) -> Array[int]:

    var result: Array[int] = []
    for c in coords:
        result.append(CalcTileId(c))
    return result

func CalcAllTileIdsMatrix(coords) -> Array[int]: # coords: Array[Array[Coord]]
    return CalcAllTileIds(ext.select_many(coords))


func CalcRowColumn(lat: float, lon: float): #, out long latTile, out long lonTile):
    var resolvedLat = lat + 180.0
    var resolvedLon = lon + 180.0

    var latTile: int = floor(resolvedLat * LAT_DEGREE_DIVISION)
    var lonTile: int = floor(resolvedLon * LON_DEGREE_DIVISION)

    assert(latTile >= 0 && latTile < WIDTH);
    assert(lonTile >= 0 && lonTile < HEIGHT);

    return { latTile: latTile, lonTile: lonTile }


func CalcTileIdsInRange(lat1: float, lon1: float, lat2: float, lon2: float) -> Array[TileRef]:

    var s = CalcRowColumn(lat1, lon1)#, out var colStart, out var rowStart);
    var colStart: int = s.latTile
    var rowStart: int = s.lonTile
    var e = CalcRowColumn(lat2, lon2) #, out var colEnd, out var rowEnd);
    var colEnd: int = e.latTile
    var rowEnd: int = e.lonTile

    var tiles: Array[TileRef] = []
    #for (var row = rowStart; row <= rowEnd; row++)
    for row: int in range(rowStart, rowEnd + 1):
    
        # for (var col = colStart; col <= colEnd; col++)
        for col: int in range(colStart, colEnd + 1):
            var tile_id = row * WIDTH + col
            var tile = TileRef.create(
                tile_id,
                (row as float / LAT_DEGREE_DIVISION as float) - 180.0,
                (col as float / LON_DEGREE_DIVISION as float) - 180.0
            )
            tiles.append(tile)
    return tiles

func CalcTileIdsInRangeSpiral(lat1: float, lon1: float, lat2: float, lon2: float) -> Array[TileRef]:

    var s = CalcRowColumn(lat1, lon1)#, out var colStart, out var rowStart);
    var colStart: int = s.latTile
    var rowStart: int = s.lonTile
    var e = CalcRowColumn(lat2, lon2) #, out var colEnd, out var rowEnd);
    var colEnd: int = e.latTile
    var rowEnd: int = e.lonTile

    var centerCol: int = (colStart + colEnd) / 2;
    var centerRow: int = (rowStart + rowEnd) / 2;

    var radius = max(abs(colEnd - colStart), abs(rowEnd - rowStart)) / 2;
    var currentCol: int = centerCol;
    var currentRow: int = centerRow;
    var centralTile = currentRow * WIDTH + currentCol;

    var tiles: Array[TileRef] = []
    tiles.append(TileRef.create(
        centralTile,
        (currentRow as float / LAT_DEGREE_DIVISION as float) - 180.0,
        (currentCol as float/ LON_DEGREE_DIVISION as float) - 180.0
    ))

    var currentRadius := 1
    while currentRadius <= radius:
    
        #for (var x = -currentRadius; x <= currentRadius; x++)
        for x: int in range(-currentRadius, currentRadius + 1):        
            var tileX := currentCol - x;
            var tileY := currentRow - currentRadius;
            if tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd:
            
                var tile = tileY * WIDTH + tileX;
                #yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                tiles.append(TileRef.create(
                    tile,
                    (tileX as float / LAT_DEGREE_DIVISION as float) - 180.0,
                    (tileY as float/ LON_DEGREE_DIVISION as float) - 180.0
                ))
        
        #for (var y = -currentRadius + 1; y < currentRadius; y++)
        for y in range(-currentRadius + 1, currentRadius):
            var tileX := currentCol + currentRadius
            var tileY := currentRow + y
            if tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd:
                var tile = tileY * WIDTH + tileX;
                #yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                tiles.append(TileRef.create(
                    tile,
                    (tileX as float / LAT_DEGREE_DIVISION as float) - 180.0,
                    (tileY as float/ LON_DEGREE_DIVISION as float) - 180.0
                ))

        #for (var x = -currentRadius; x <= currentRadius; x++)
        for x in range(-currentRadius, currentRadius + 1):
            var tileX = currentCol + x;
            var tileY = currentRow + currentRadius;
            if tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd:
                var tile = tileY * WIDTH + tileX;
                #yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                tiles.append(TileRef.create(
                    tile,
                    (tileX as float / LAT_DEGREE_DIVISION as float) - 180.0,
                    (tileY as float/ LON_DEGREE_DIVISION as float) - 180.0
                ))

        #for (var y = -currentRadius + 1; y < currentRadius; y++)
        for y in range(-currentRadius + 1, currentRadius):
            var tileX = currentCol - currentRadius;
            var tileY = currentRow - y;
            if tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd:
                var tile = tileY * WIDTH + tileX;
                #yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                tiles.append(TileRef.create(
                    tile,
                    (tileX as float / LAT_DEGREE_DIVISION as float) - 180.0,
                    (tileY as float/ LON_DEGREE_DIVISION as float) - 180.0
                ))

        currentRadius = currentRadius + 1
    return tiles
    
