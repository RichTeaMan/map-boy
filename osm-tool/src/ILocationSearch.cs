using OsmTool.Models;

namespace OsmTool;

public interface ILocationSearch
{
    void InitIndex();

    void UpdateIndex(IEnumerable<SearchIndexEntry> searchIndexEntries);

    IEnumerable<SearchIndexResult> SearchAreas(string searchTerm);
}
