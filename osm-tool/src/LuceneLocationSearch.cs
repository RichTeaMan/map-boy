using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Documents.Extensions;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using OsmTool.Models;

namespace OsmTool;

public class LuceneLocationSearch : ILocationSearch, IDisposable
{
    const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    private IndexWriter? indexWriter;

    private bool disposedValue;

    private string filePath;

    private List<IDisposable> disposable = new List<IDisposable>();

    public LuceneLocationSearch(string filePath)
    {
        this.filePath = filePath;
    }

    public void InitIndex()
    {
        // nothing to init... I think?
    }

    private IndexWriter fetchIndexWriter()
    {
        if (indexWriter == null)
        {
            var dir = FSDirectory.Open(filePath);

            // Create an analyzer to process the text
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            indexWriter = new IndexWriter(dir, indexConfig);

            disposable.Add(dir);
            disposable.Add(analyzer);
            disposable.Add(indexWriter);
        }
        return indexWriter;
    }

    public IEnumerable<SearchIndexResult> SearchAreas(string searchTerm)
    {
        Console.WriteLine($"Searching {searchTerm}...");
        var query = new FuzzyQuery(new Term("name", searchTerm.Trim().ToString()));
        //var query = new TermQuery(new Term("name", searchTerm.ToString()));
        using var reader = fetchIndexWriter().GetReader(true);//applyAllDeletes: true);
        var searcher = new IndexSearcher(reader);
        var hits = searcher.Search(query, 20 /* top 20 */).ScoreDocs;

        Console.WriteLine($"Found {hits.Length}");

        foreach (var hit in hits.OrderByDescending(h => h.Score))
        {
            var foundDoc = searcher.Doc(hit.Doc);
            var name = foundDoc.Get("name");
            var lat = foundDoc.GetField("lat").GetDoubleValue();
            var lon = foundDoc.GetField("lon").GetDoubleValue();
            if (name == null || lat == null || lon == null)
            {
                continue;
            }
            yield return new SearchIndexResult
            {
                Name = name,
                Lat = lat.Value,
                Lon = lon.Value,
                Rank = hit.Score
            };
        }
    }

    public void UpdateIndex(IEnumerable<SearchIndexEntry> searchIndexEntries)
    {
        var writer = fetchIndexWriter();
        foreach (var searchIndexChunk in searchIndexEntries.Chunk(1000))
        {
            foreach (var searchIndexEntry in searchIndexChunk)
            {
                var doc = new Document() {
                    new StringField("name",
                        searchIndexEntry.Name.Trim(),
                        Field.Store.YES),
                    new StoredField("lat",
                        searchIndexEntry.Lat),
                    new StoredField("lon",
                        searchIndexEntry.Lon)
                };
                writer.AddDocument(doc);
            }
            writer.Commit();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                foreach (var d in disposable)
                {
                    d?.Dispose();
                }
            }
            disposedValue = true;
        }
    }

    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
