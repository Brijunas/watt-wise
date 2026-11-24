using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Serilog.Core;
using Serilog.Events;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class RequestLoggingTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_dashboard_request_writes_exactly_one_information_line_with_method_path_and_status()
    {
        LogEventCollector logs = new();
        await using TestHostFactory<Program> factory = new(
            JobsTestSettings.For(fixture),
            services =>
            {
                services.AddSingleton<IStartupFilter, LoopbackClient>();
                services.AddSingleton<ILogEventSink>(logs);
            });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/hangfire", Token);

        await Poll.UntilAsync(() => RequestLines(logs).FirstOrDefault(), "the request log line", Token);

        Assert.Equal(
            ["Information GET /hangfire 200"],
            RequestLines(logs).Select(logEvent =>
                $"{logEvent.Level} {ScalarValue(logEvent, "RequestMethod")} {ScalarValue(logEvent, "RequestPath")} {ScalarValue(logEvent, "StatusCode")}"));
    }

    [Fact]
    public async Task A_health_request_writes_no_line_at_the_default_information_level()
    {
        LogEventCollector logs = new();
        await using TestHostFactory<Program> factory = new(
            JobsTestSettings.For(fixture),
            services =>
            {
                services.AddSingleton<IStartupFilter, LoopbackClient>();
                services.AddSingleton<ILogEventSink>(logs);
            });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage health = await client.GetAsync("/health", Token);
        // The later /hangfire line proves the /health line, written first, had its chance to appear.
        using HttpResponseMessage dashboard = await client.GetAsync("/hangfire", Token);

        await Poll.UntilAsync(() => RequestLines(logs).FirstOrDefault(), "the request log line", Token);

        Assert.Empty(RequestLines(logs, "/health"));
    }

    private static IEnumerable<LogEvent> RequestLines(LogEventCollector logs, string path = "/hangfire")
    {
        return logs.Snapshot().Where(logEvent => logEvent.Properties.ContainsKey("StatusCode")
            && ScalarValue(logEvent, "RequestPath") as string == path);
    }

    private static object? ScalarValue(LogEvent logEvent, string name)
    {
        return logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
    }
}
