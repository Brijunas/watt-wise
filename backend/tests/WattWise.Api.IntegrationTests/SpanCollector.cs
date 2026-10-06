using System.Collections.Concurrent;
using System.Diagnostics;

using OpenTelemetry;

namespace WattWise.Api.IntegrationTests;

/// <summary>An OpenTelemetry processor that keeps every span once it ends. Activity listeners are process-wide, so filter by trace id.</summary>
public sealed class SpanCollector : BaseProcessor<Activity>
{
    private readonly ConcurrentQueue<Activity> spans = new();

    public override void OnEnd(Activity data)
    {
        spans.Enqueue(data);
    }

    public IReadOnlyList<Activity> Snapshot()
    {
        return [.. spans];
    }
}
