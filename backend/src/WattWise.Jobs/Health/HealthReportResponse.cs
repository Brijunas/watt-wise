namespace WattWise.Jobs.Health;

/// <summary>The /health response body.</summary>
public sealed record HealthReportResponse(string Status, double DurationMs, IReadOnlyList<HealthCheckResponse> Checks);
