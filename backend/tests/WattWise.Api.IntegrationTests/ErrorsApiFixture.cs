using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenTelemetry.Trace;

using Serilog.Core;

using WattWise.Api.Endpoints;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// Class fixture: one Api host for every test of a class, with the test error endpoints mapped and
/// a Serilog sink (<see cref="Logs"/>) and an OpenTelemetry span processor (<see cref="Spans"/>) registered.
/// Activity listeners and the sink see events from every host in the process, so tests filter by trace id.
/// It allows <see cref="AllowedOrigin"/> through CORS. It owns a database of its own, managed by a <see cref="DatabaseFixture"/>.
/// </summary>
public sealed class ErrorsApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    public const string AllowedOrigin = "https://app.example.test";

    private readonly DatabaseFixture databaseFixture = new(postgres);
    private ApiFactory? factory;

    public ApiFactory Factory => factory ?? throw new InvalidOperationException("The fixture is not initialized.");

    /// <summary>Every log event the host wrote.</summary>
    public LogEventCollector Logs { get; } = new();

    /// <summary>Every span the host's tracer provider ended.</summary>
    public SpanCollector Spans { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await databaseFixture.InitializeAsync();
        Dictionary<string, string?> settings = new(databaseFixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
        };
        factory = new ApiFactory(
            settings,
            services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointModule, TestErrorsEndpointModule>());
                services.AddSingleton<ILogEventSink>(Logs);
                services.ConfigureOpenTelemetryTracerProvider(tracer => tracer.AddProcessor(Spans));
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }

        await databaseFixture.DisposeAsync();
    }
}
