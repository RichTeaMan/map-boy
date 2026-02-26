public sealed class Timer
{
    private DateTimeOffset start = new DateTimeOffset();

    private Action<int> StopCallback;

    public Timer(Action<int> stopCallback)
    {
        StopCallback = stopCallback;
    }

    /// <summary>
    /// Stops the timer and returns the elapsed duration in milliseconds.
    /// </summary>
    public int Stop()
    {
        var end = new DateTimeOffset();
        var duration = end - start;
        var durationMs = (int)duration.TotalMilliseconds;
        StopCallback(durationMs);
        return durationMs;
    }
}
