namespace WattWise.Testing;

/// <summary>Waits for something that happens after the response is sent, such as a server span ending or a log line being written.</summary>
public static class Poll
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Calls <paramref name="probe"/> until it returns a non-null value; fails the test after
    /// <paramref name="timeout"/> (five seconds when omitted).
    /// </summary>
    public static async Task<T> UntilAsync<T>(
        Func<T?> probe,
        string description,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
        where T : class
    {
        TimeSpan limit = timeout ?? DefaultTimeout;
        DateTime deadline = DateTime.UtcNow + limit;
        while (true)
        {
            T? value = probe();
            if (value is not null)
            {
                return value;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out after {limit.TotalSeconds:0} s waiting for: {description}.");
            }

            await Task.Delay(Delay, cancellationToken);
        }
    }
}
