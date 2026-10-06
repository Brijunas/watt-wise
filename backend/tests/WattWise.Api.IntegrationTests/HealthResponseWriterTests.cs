using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using WattWise.Api.Health;

namespace WattWise.Api.IntegrationTests;

public class HealthResponseWriterTests
{
    private const string Secret = "LEAKED-SECRET-TEXT";

    [Fact]
    public async Task A_failing_check_never_exposes_its_exception_text()
    {
        HealthReportEntry entry = new(
            HealthStatus.Unhealthy,
            Secret,
            TimeSpan.FromMilliseconds(5),
            new InvalidOperationException(Secret),
            null);

        (string json, JsonElement root) = await WriteAsync(entry);

        Assert.DoesNotContain(Secret, json);
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        JsonElement check = Assert.Single(root.GetProperty("checks").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, check.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task A_check_without_an_exception_keeps_its_description()
    {
        HealthReportEntry entry = new(HealthStatus.Healthy, "all good", TimeSpan.FromMilliseconds(5), null, null);

        (_, JsonElement root) = await WriteAsync(entry);

        JsonElement check = Assert.Single(root.GetProperty("checks").EnumerateArray());
        Assert.Equal("all good", check.GetProperty("description").GetString());
    }

    private static async Task<(string Json, JsonElement Root)> WriteAsync(HealthReportEntry entry)
    {
        HealthReport report = new(new Dictionary<string, HealthReportEntry> { ["database"] = entry }, TimeSpan.FromMilliseconds(5));
        DefaultHttpContext context = new();
        using MemoryStream stream = new();
        context.Response.Body = stream;

        await HealthResponseWriter.WriteAsync(context, report);

        string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        return (json, JsonDocument.Parse(json).RootElement.Clone());
    }
}
