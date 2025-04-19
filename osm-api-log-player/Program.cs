namespace OsmTool.Api.LogPlayer;


public class Program
{

    public static async Task<int> Main(string[] args)
    {
        int players = 10;
        string baseAddress = "http://localhost:5291";
        Console.WriteLine($"Reading log from {args[0]}");
        var logs = await PathLog.CreateFromFile(args[0]);

        Console.WriteLine($"Replay is {logs.Last().Milliseconds / 1000} seconds.");

        Console.WriteLine("playing...");
        await Parallel.ForEachAsync(Enumerable.Range(0, players), async (i, _) =>
        {
            using var logPlayer = new LogPlayer(baseAddress);
            await logPlayer.Play(logs);
        });
        return 0;
    }


}
