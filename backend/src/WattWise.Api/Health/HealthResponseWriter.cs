using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WattWise.Api.Health;

/// <summary>Writes a health report as camelCase JSON. Exception text is never included (a failing check's description is dropped), because /health is public.</summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        HealthReportResponse body = new(
            report.Status.ToString(),
            report.TotalDuration.TotalMilliseconds,
            [.. report.Entries.Select(entry => new HealthCheckResponse(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Duration.TotalMilliseconds,
                entry.Value.Exception is null ? entry.Value.Description : null))]);

        return context.Response.WriteAsJsonAsync(body, JsonOptions, "application/json; charset=utf-8");
    }
}
