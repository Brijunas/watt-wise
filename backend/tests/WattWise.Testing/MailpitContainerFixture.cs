using System.Net;

using Docker.DotNet.Models;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace WattWise.Testing;

/// <summary>
/// Assembly fixture: one Mailpit container that catches every email the tests send and serves them back
/// through its API. Same image as the Development stack (<c>deploy/docker-compose.development.yml</c>); keep the tags equal.
/// </summary>
public sealed class MailpitContainerFixture : IAsyncLifetime
{
    private const string Image = "axllent/mailpit:v1.33.1";
    private const int SmtpContainerPort = 1025;
    private const int ApiContainerPort = 8025;
    private const int StartAttempts = 5;

    private IContainer container = null!;
    private int smtpPort;
    private MailpitClient? client;

    // Rootless Docker with the pasta network driver forwards no host port in the ephemeral range, so the
    // container gets fixed ports below it and Ryuk is switched off, as in PostgresContainerFixture.
    static MailpitContainerFixture() => TestcontainersSettings.ResourceReaperEnabled = false;

    /// <summary>The <c>Smtp:*</c> configuration keys that point the code under test at this container.</summary>
    public IReadOnlyDictionary<string, string?> SmtpConfiguration => SmtpTestSettings.For(IPAddress.Loopback.ToString(), smtpPort);

    public MailpitClient Client => client ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        for (int attempt = 1; ; attempt++)
        {
            smtpPort = FreeHostPort.Pick();
            int apiPort = FreeHostPort.Pick();
            if (apiPort == smtpPort)
            {
                continue;
            }

            container = BuildContainer(smtpPort, apiPort);
            try
            {
                await container.StartAsync(cancellationToken);
                client = new MailpitClient(new Uri($"http://{IPAddress.Loopback}:{apiPort}/"));
                await WaitUntilReadyAsync(apiPort, cancellationToken);
                return;
            }
            catch (Exception) when (attempt < StartAttempts)
            {
                client?.Dispose();
                client = null;
                await container.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await container.DisposeAsync();
    }

    private static IContainer BuildContainer(int smtpHostPort, int apiHostPort) =>
        new ContainerBuilder(Image)
            .WithLabel("wattwise.tests", "true")
            .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
            .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true")
            .WithExposedPort(SmtpContainerPort)
            .WithExposedPort(ApiContainerPort)
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig.PortBindings ??= new Dictionary<string, IList<PortBinding>>();
                parameters.HostConfig.PortBindings[$"{SmtpContainerPort}/tcp"] = [Binding(smtpHostPort)];
                parameters.HostConfig.PortBindings[$"{ApiContainerPort}/tcp"] = [Binding(apiHostPort)];
            })
            .Build();

    private static PortBinding Binding(int hostPort) =>
        new() { HostIP = IPAddress.Loopback.ToString(), HostPort = hostPort.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    // The image has no shell to exec in, so readiness is polled over the published API port.
    private static async Task WaitUntilReadyAsync(int apiPort, CancellationToken cancellationToken)
    {
        using HttpClient http = new() { BaseAddress = new Uri($"http://{IPAddress.Loopback}:{apiPort}/") };
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                using HttpResponseMessage response = await http.GetAsync("livez", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Mailpit did not become ready within 30 s.");
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}
