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
            fixture.Database.ConfigurationFor(DatabaseRole.Jobs),
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

    private static IEnumerable<LogEvent> RequestLines(LogEventCollector logs)
    {
        return logs.Snapshot().Where(logEvent => logEvent.Properties.ContainsKey("StatusCode")
            && ScalarValue(logEvent, "RequestPath") as string == "/hangfire");
    }

    private static object? ScalarValue(LogEvent logEvent, string name)
    {
        return logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
    }
}
