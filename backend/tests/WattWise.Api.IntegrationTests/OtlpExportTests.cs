using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

namespace WattWise.Api.IntegrationTests;

public class OtlpExportTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string TraceExportPath = "/opentelemetry.proto.collector.trace.v1.TraceService/Export";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_otlp_endpoint_exports_traces_to_the_trace_service()
    {
        await using OtlpTestReceiver receiver = await OtlpTestReceiver.StartAsync(Token);
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Observability:OtlpEndpoint"] = receiver.Endpoint.GetLeftPart(UriPartial.Authority),
        };
        await using TestHostFactory<Program> factory = new(settings);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        _ = factory.Services.GetRequiredService<TracerProvider>().ForceFlush(timeoutMilliseconds: 5000);

        // Waits until the trace export call arrives or 10 s pass, without failing; the assert below decides.
        await WaitForTraceExportOrTimeoutAsync(receiver);
        Assert.Contains(TraceExportPath, receiver.Paths());
    }

    private static async Task WaitForTraceExportOrTimeoutAsync(OtlpTestReceiver receiver)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!receiver.Paths().Contains(TraceExportPath) && !timeout.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), CancellationToken.None);
        }
    }
}
