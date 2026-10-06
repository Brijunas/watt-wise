using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using WattWise.Api.Endpoints;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// Class fixture: one Api host for every test of a class, with the test error endpoints mapped and
/// a fake log collector registered. It allows <see cref="AllowedOrigin"/> through CORS. It owns a database of its own, managed by a <see cref="DatabaseFixture"/>.
/// </summary>
public sealed class ErrorsApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    public const string AllowedOrigin = "https://app.example.test";

    private readonly DatabaseFixture databaseFixture = new(postgres);
    private ApiFactory? factory;

    public ApiFactory Factory => factory ?? throw new InvalidOperationException("The fixture is not initialized.");

    public FakeLogCollector Logs => Factory.Services.GetRequiredService<FakeLogCollector>();

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
                services.AddLogging(builder => builder.AddFakeLogging());
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
