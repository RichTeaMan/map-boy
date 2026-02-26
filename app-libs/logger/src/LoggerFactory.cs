namespace MapBoy.Logger;

public static class LoggerFactory
{
    private static List<AbstractLoggingBackend> Backends = new List<AbstractLoggingBackend>();

    public static void AddBackend<T>() where T : AbstractLoggingBackend, new()
    {
        var backend = new T();
        Backends.Add(backend);
    }

    public static void AddBackend(AbstractLoggingBackend loggingBackend)
    {
        Backends.Add(loggingBackend);
    }

    public static Logger GetLogger<T>() where T : class
    {
        return new Logger(typeof(T).Name, Backends.ToArray());
    }

    public static void Close()
    {
        foreach (var backend in Backends)
        {
            backend.Close();
        }
    }
}
