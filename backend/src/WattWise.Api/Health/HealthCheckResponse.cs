namespace WattWise.Api.Health;

/// <summary>One entry of the /health response. Never carries exception details.</summary>
public sealed record HealthCheckResponse(string Name, string Status, double DurationMs, string? Description);
