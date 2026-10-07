using System.Diagnostics;

using OpenTelemetry;

namespace WattWise.Infrastructure.Observability;

/// <summary>
/// Keeps Npgsql spans that have no parent out of the export. Background polling (Hangfire's workers and
/// queue listener) runs SQL outside any request or job, and each such command would otherwise become a
/// one-span trace of its own, burying real traces. A database span inside a request, a job or a CLI
/// command has a parent and is kept. The span is still created, so log events written during it keep
/// their trace id; it is only marked as not recorded, which the exporters skip.
/// </summary>
public sealed class ParentlessDatabaseSpanFilter : BaseProcessor<Activity>
{
    /// <summary>The activity source Npgsql emits its command spans from.</summary>
    public const string NpgsqlSource = "Npgsql";

    // Done at start rather than end, so the order of this processor and the exporters doesn't matter.
    public override void OnStart(Activity data)
    {
        if (data.Source.Name == NpgsqlSource && data.ParentSpanId == default)
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
