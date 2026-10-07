using System.Diagnostics;

namespace WattWise.Infrastructure.Observability;

/// <summary>Spans Infrastructure starts itself, e.g. one per schema step. Covered by the <c>WattWise.*</c> source.</summary>
public static class InfrastructureTelemetry
{
    public static readonly ActivitySource Source = new("WattWise.Infrastructure");
}
