
namespace MapBoy.Logger;

public class ConsoleLoggingBackend : AbstractLoggingBackend
{
    public override void OnLog(DateTimeOffset dateTime, LogLevel logLevel, string source, string message)
    {
        Console.WriteLine($"[{dateTime}] [{logLevel}] [{source}] {message}");
    }

    public override void OnLogTimer(DateTimeOffset dateTime, string source, string key, int durationMs)
    {
        Console.WriteLine($"[{dateTime}] [{source}] {key}: {durationMs}ms");
    }
}