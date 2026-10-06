namespace WattWise.Api.Health;

/// <summary>The /health response body.</summary>
public sealed record HealthReportResponse(string Status, double DurationMs, IReadOnlyList<HealthCheckResponse> Checks);
