using System.Diagnostics;

namespace OsmTool;

public class Tile
{
    public long Id { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}
public class TileService
{

    const int LAT_DEGREE_DIVISION = 200;
    const int LON_DEGREE_DIVISION = 200;

    const long WIDTH = LAT_DEGREE_DIVISION * 360;
    const long HEIGHT = LON_DEGREE_DIVISION * 360;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lat">East and west.</param>
    /// <param name="lon">North and south.</param>
    /// <returns></returns>
    public long CalcTileId(double lat, double lon)
    {
        long latTile, lonTile;
        CalcRowColumn(lat, lon, out latTile, out lonTile);

        var tile = lonTile * WIDTH + latTile;
        return tile;
    }

    private void CalcRowColumn(double lat, double lon, out long latTile, out long lonTile)
    {
        var resolvedLat = lat + 180.0;
        var resolvedLon = lon + 180.0;

        latTile = (long)Math.Floor(resolvedLat * LAT_DEGREE_DIVISION);
        lonTile = (long)Math.Floor(resolvedLon * LON_DEGREE_DIVISION);

        Debug.Assert(latTile >= 0 && latTile < WIDTH);
        Debug.Assert(lonTile >= 0 && lonTile < HEIGHT);
    }

    public IEnumerable<Tile> CalcTileIdsInRange(double lat1, double lon1, double lat2, double lon2)
    {
        CalcRowColumn(lat1, lon1, out var colStart, out var rowStart);
        CalcRowColumn(lat2, lon2, out var colEnd, out var rowEnd);

        for (var row = rowStart; row <= rowEnd; row++)
        {
            for (var col = colStart; col <= colEnd; col++)
            {
                var tile = row * WIDTH + col;
                yield return new Tile { Id = tile, Lat = (row / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (col / (double)LON_DEGREE_DIVISION) - 180.0 };
            }
        }
    }

    public IEnumerable<Tile> CalcTileIdsInRangeSpiral(double lat1, double lon1, double lat2, double lon2)
    {
        CalcRowColumn(lat1, lon1, out var colStart, out var rowStart);
        CalcRowColumn(lat2, lon2, out var colEnd, out var rowEnd);

        var centerCol = (colStart + colEnd) / 2;
        var centerRow = (rowStart + rowEnd) / 2;

        var radius = Math.Max(Math.Abs(colEnd - colStart), Math.Abs(rowEnd - rowStart)) / 2;
        var currentCol = centerCol;
        var currentRow = centerRow;
        var centralTile = currentRow * WIDTH + currentCol;
        yield return new Tile { Id = centralTile, Lat = (currentRow / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (currentCol / (double)LON_DEGREE_DIVISION) - 180.0 };

        int currentRadius = 1;
        while (currentRadius <= radius)
        {
            for (var x = -currentRadius; x <= currentRadius; x++)
            {
                var tileX = currentCol - x;
                var tileY = currentRow - currentRadius;
                if (tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd)
                {
                    var tile = tileY * WIDTH + tileX;
                    yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                }
            }
            for (var y = -currentRadius + 1; y < currentRadius; y++)
            {
                var tileX = currentCol + currentRadius;
                var tileY = currentRow + y;
                if (tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd)
                {
                    var tile = tileY * WIDTH + tileX;
                    yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                }
            }
            for (var x = -currentRadius; x <= currentRadius; x++)
            {
                var tileX = currentCol + x;
                var tileY = currentRow + currentRadius;
                if (tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd)
                {
                    var tile = tileY * WIDTH + tileX;
                    yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                }
            }
            for (var y = -currentRadius + 1; y < currentRadius; y++)
            {
                var tileX = currentCol - currentRadius;
                var tileY = currentRow - y;
                if (tileX >= colStart && tileX <= colEnd && tileY >= rowStart && tileY <= rowEnd)
                {
                    var tile = tileY * WIDTH + tileX;
                    yield return new Tile { Id = tile, Lat = (tileX / (double)LAT_DEGREE_DIVISION) - 180.0, Lon = (tileY / (double)LON_DEGREE_DIVISION) - 180.0 };
                }
            }
            currentRadius++;
        }
    }
}
