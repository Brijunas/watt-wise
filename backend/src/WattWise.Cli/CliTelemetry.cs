using System.Diagnostics;

namespace WattWise.Cli;

/// <summary>The CLI's trace source; its name matches the "WattWise.*" source the telemetry listens to.</summary>
internal static class CliTelemetry
{
    public static readonly ActivitySource Source = new("WattWise.Cli");
}
