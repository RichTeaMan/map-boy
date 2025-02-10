using OsmTool.Models;

namespace OsmTool;

public interface IReader
{

    IEnumerable<OsmNode> IterateNodes();

    IEnumerable<OsmWay> IterateWays();
}
