using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

namespace WattWise.Api.IntegrationTests;

public class OtlpExportTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_otlp_endpoint_turns_export_on()
    {
        // The listener does not speak gRPC, so the export itself fails; a connection attempt is the proof it was tried.
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Observability:OtlpEndpoint"] = $"http://127.0.0.1:{port}",
        };
        await using ApiFactory factory = new(settings);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.True(response.IsSuccessStatusCode, $"/health returned {(int)response.StatusCode}.");
        _ = factory.Services.GetRequiredService<TracerProvider>().ForceFlush(timeoutMilliseconds: 5000);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using TcpClient accepted = await listener.AcceptTcpClientAsync(timeout.Token);
        Assert.True(accepted.Connected);
    }
}
