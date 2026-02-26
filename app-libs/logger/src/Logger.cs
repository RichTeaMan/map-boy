namespace MapBoy.Logger;

public sealed class Logger
{
    private string Source;

    private AbstractLoggingBackend[] AbstractLoggingBackends;

    public Logger(string source, AbstractLoggingBackend[] abstractLoggingBackends)
    {
        Source = source;
        AbstractLoggingBackends = abstractLoggingBackends;
    }

    public Timer Timer(string key)
    {
        var dateTime = DateTimeOffset.Now;
        return new Timer((int durationMs) =>
        {
            foreach (var backend in AbstractLoggingBackends)
            {
                backend.LogTimer(dateTime, Source, key, durationMs);
            }
        });
    }

    public void Log(LogLevel logLevel, string message, Exception ex)
    {
        var resolvedMessage = $"{message}\n{ex.Message}";
        Log(logLevel, resolvedMessage);
    }

    public void Log(LogLevel logLevel, string message)
    {
        var dateTime = DateTimeOffset.Now;
        foreach (var backend in AbstractLoggingBackends)
        {
            backend.Log(dateTime, logLevel, Source, message);
        }
    }

    public void Trace(string message)
    {
        Log(LogLevel.TRACE, message);
    }

    public void Trace(string message, Exception exception)
    {
        Log(LogLevel.TRACE, message, exception);
    }

    public void Debug(string message)
    {
        Log(LogLevel.DEBUG, message);
    }

    public void Debug(string message, Exception exception)
    {
        Log(LogLevel.DEBUG, message, exception);
    }

    public void Info(string message)
    {
        Log(LogLevel.INFO, message);
    }

    public void Info(string message, Exception exception)
    {
        Log(LogLevel.INFO, message, exception);
    }

    public void Warn(string message)
    {
        Log(LogLevel.WARN, message);
    }

    public void Warn(string message, Exception exception)
    {
        Log(LogLevel.WARN, message, exception);
    }

    public void Error(string message)
    {
        Log(LogLevel.ERROR, message);
    }

    public void Error(string message, Exception exception)
    {
        Log(LogLevel.ERROR, message, exception);
    }
}
