using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;

namespace OsmTool.Api.LogPlayer;

public class PathLog
{
    public long Milliseconds { get; set; }
    public required string Path { get; set; }
    public required string Method { get; set; }

    public static async Task<PathLog[]> CreateFromFile(string logPath)
    {
        DateTimeOffset? startDateTime = null;
        var pathLogs = new List<PathLog>();
        await foreach (var line in File.ReadLinesAsync(logPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            int dateStart = line.IndexOf('[');
            int dateEnd = line.IndexOf(']');
            if (dateStart != 0 || dateEnd == -1)
            {
                continue;
            }
            try
            {
                var dateTime = DateTimeOffset.Parse(line.Substring(dateStart, dateEnd).Trim('[', ']'));
                var parts = line.Substring(dateEnd + 1).Trim().Split(' ');
                var method = HttpMethod.Parse(parts[0]).Method;
                var path = parts[1];

                if (startDateTime == null)
                {
                    startDateTime = dateTime;
                }

                pathLogs.Add(new PathLog
                {
                    Milliseconds = (long)(dateTime - startDateTime).Value.TotalMilliseconds,
                    Path = path,
                    Method = method
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to parse line - {line}");
                Console.WriteLine(e);
            }

        }
        return pathLogs.OrderBy(l => l.Milliseconds).ToArray();
    }
}
