using System.Data;

namespace OsmTool.Api.LogPlayer;

public class LogPlayer : IDisposable
{
    private bool disposedValue;

    private HttpClient httpClient;

    public LogPlayer(string baseAddress)
    {
        httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseAddress);
    }

    public async Task Play(PathLog[] pathLogs)
    {
        var start = DateTimeOffset.Now;
        foreach (var pathLog in pathLogs.OrderBy(l => l.Milliseconds))
        {
            var method = HttpMethod.Parse(pathLog.Method);
            using var request = new HttpRequestMessage(method, pathLog.Path);
            while (!HasElapsed(start, pathLog.Milliseconds))
            {
                //Console.WriteLine($"waiting {pathLog.Milliseconds}");
                await Task.Delay(10);
            }
            using var response = await httpClient.SendAsync(request);
        }
    }

    private bool HasElapsed(DateTimeOffset start, long milliseconds)
    {
        return (long)(DateTimeOffset.Now - start).TotalMilliseconds >= milliseconds;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
