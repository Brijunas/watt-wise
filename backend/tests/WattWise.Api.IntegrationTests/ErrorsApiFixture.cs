using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using WattWise.Api.Endpoints;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// Class fixture: one Api host for every test of a class, with the test error endpoints mapped and
/// a fake log collector registered. It owns a database of its own.
/// </summary>
public sealed class ErrorsApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private TestDatabase? database;
    private ApiFactory? factory;

    public ApiFactory Factory => factory ?? throw new InvalidOperationException("The fixture is not initialized.");

    public FakeLogCollector Logs => Factory.Services.GetRequiredService<FakeLogCollector>();

    public async ValueTask InitializeAsync()
    {
        database = await postgres.CreateDatabaseAsync();
        factory = new ApiFactory(
            database.ConfigurationFor(DatabaseRole.Api),
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

        if (database is not null)
        {
            await database.DropAsync();
        }
    }
}
