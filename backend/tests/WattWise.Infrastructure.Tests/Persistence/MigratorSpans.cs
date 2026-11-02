using System.Collections.Concurrent;
using System.Diagnostics;

namespace WattWise.Infrastructure.Tests.Persistence;

/// <summary>
/// Collects the spans of one migrator run. Listeners are process-wide, so only the spans under this run's own root are kept.
/// </summary>
internal sealed class MigratorSpans : IDisposable
{
    private readonly ActivitySource testSource = new("WattWise.Tests.MigratorSpans");
    private readonly ConcurrentQueue<Activity> stopped = new();
    private readonly ActivityListener listener;
    private readonly Activity root;

    public MigratorSpans()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "WattWise.Infrastructure" or "WattWise.Tests.MigratorSpans",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        root = testSource.StartActivity("migrate") ?? throw new InvalidOperationException("The test listener did not record the root span.");
    }

    public Activity Single() =>
        Assert.Single(stopped, span => span.TraceId == root.TraceId && span.DisplayName.StartsWith("schema-step ", StringComparison.Ordinal));

    public void Dispose()
    {
        root.Dispose();
        listener.Dispose();
        testSource.Dispose();
    }
}
