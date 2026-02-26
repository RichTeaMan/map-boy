namespace MapBoy.Logger;

public abstract class AbstractLoggingBackend
{
    public bool IsClosed { get; private set; }

    public void Log(DateTimeOffset dateTime, LogLevel logLevel, string source, string message)
    {
        if (IsClosed)
        {
            Console.WriteLine("Logger is closed, refusing to log.");
            Console.WriteLine($"[{dateTime}] [{logLevel}] [{source}] {message}");
            return;
        }
        OnLog(dateTime, logLevel, source, message);
    }

    public abstract void OnLog(DateTimeOffset dateTime, LogLevel logLevel, string source, string message);

    public void LogTimer(DateTimeOffset dateTime, string source, string key, int durationMs)
    {
        if (IsClosed)
        {
            Console.WriteLine("Logger is closed, refusing to log timer.");
            Console.WriteLine($"[{dateTime}] [{source}] {key}: {durationMs}ms");
            return;
        }
        OnLogTimer(dateTime, source, key, durationMs);
    }

    public abstract void OnLogTimer(DateTimeOffset dateTime, string source, string key, int durationMs);


    public void Close()
    {
        if (!IsClosed)
        {
            IsClosed = true;
            OnClose();
        }
    }
    protected virtual void OnClose() { }
}
