namespace WattWise.Api.IntegrationTests;

/// <summary>Waits for something that happens after the response is sent, such as a server span ending or a log line being written.</summary>
public static class Poll
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(25);

    /// <summary>Calls <paramref name="probe"/> until it returns a non-null value; fails the test after five seconds.</summary>
    public static async Task<T> UntilAsync<T>(Func<T?> probe, string description, CancellationToken cancellationToken)
        where T : class
    {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            T? value = probe();
            if (value is not null)
            {
                return value;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out after {Timeout.TotalSeconds:0} s waiting for: {description}.");
            }

            await Task.Delay(Delay, cancellationToken);
        }
    }
}
