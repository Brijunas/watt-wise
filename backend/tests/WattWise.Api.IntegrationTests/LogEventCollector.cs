using System.Collections.Concurrent;

using Serilog.Core;
using Serilog.Events;

namespace WattWise.Api.IntegrationTests;

/// <summary>A Serilog sink that keeps every event it receives, so tests can assert on what the host logged.</summary>
public sealed class LogEventCollector : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    public void Emit(LogEvent logEvent)
    {
        events.Enqueue(logEvent);
    }

    public IReadOnlyList<LogEvent> Snapshot()
    {
        return [.. events];
    }

    public void Clear()
    {
        events.Clear();
    }
}
