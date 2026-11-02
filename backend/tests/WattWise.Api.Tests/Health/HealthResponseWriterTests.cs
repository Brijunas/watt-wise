using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using WattWise.Api.Health;

namespace WattWise.Api.Tests.Health;

public class HealthResponseWriterTests
{
    private const string Secret = "LEAKED-SECRET-TEXT";

    [Fact]
    public async Task A_failing_check_is_written_without_its_description_or_exception_text()
    {
        HealthReportEntry entry = new(
            HealthStatus.Unhealthy,
            Secret,
            TimeSpan.FromMilliseconds(3),
            new InvalidOperationException(Secret),
            null);

        string json = await WriteAsync(entry);

        Assert.Equal(
            """{"status":"Unhealthy","durationMs":5,"checks":[{"name":"database","status":"Unhealthy","durationMs":3,"description":null}]}""",
            json);
    }

    [Fact]
    public async Task An_unhealthy_check_without_an_exception_keeps_its_description_in_the_documented_shape()
    {
        HealthReportEntry entry = new(HealthStatus.Unhealthy, "cannot connect", TimeSpan.FromMilliseconds(3), null, null);

        string json = await WriteAsync(entry);

        Assert.Equal(
            """{"status":"Unhealthy","durationMs":5,"checks":[{"name":"database","status":"Unhealthy","durationMs":3,"description":"cannot connect"}]}""",
            json);
    }

    private static async Task<string> WriteAsync(HealthReportEntry entry)
    {
        HealthReport report = new(new Dictionary<string, HealthReportEntry> { ["database"] = entry }, TimeSpan.FromMilliseconds(5));
        DefaultHttpContext context = new();
        using MemoryStream stream = new();
        context.Response.Body = stream;

        await HealthResponseWriter.WriteAsync(context, report);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
