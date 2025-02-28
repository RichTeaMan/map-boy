namespace OsmTool.Test;

[TestClass]
public sealed class HighwayBuilderServiceTest
{
    [TestMethod]
    public void BasicTest()
    {
        var highwayBuilderService = new HighwayBuilderService();
        var coords = new Coord[] {
            new Coord(1,0),
            new Coord(1.5,0),
            new Coord(2,0)
        };
        var highwayCoords = highwayBuilderService.CalcHighwayCoordinates(coords, 0.1).ToArray();

        var expectedCoords = new Coord[] {
            new Coord { Lat = 1, Lon = -0.1 },
            new Coord { Lat = 1.5, Lon = -0.1 },
            new Coord { Lat = 2, Lon = -0.1 },
            new Coord { Lat = 2, Lon = 0.1 },
            new Coord { Lat = 1.5, Lon = 0.1 },
            new Coord { Lat = 1, Lon = 0.1 },
        };
        CollectionAssert.AreEqual(expectedCoords, highwayCoords);
    }
}
