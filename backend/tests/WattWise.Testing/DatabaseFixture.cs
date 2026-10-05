using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure;

namespace WattWise.Testing;

/// <summary>Class fixture: a database of its own, cloned from the template and dropped afterwards.</summary>
public sealed class DatabaseFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private TestDatabase? database;

    public TestDatabase Database => database ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync() => database = await postgres.CreateDatabaseAsync();

    public async ValueTask DisposeAsync()
    {
        if (database is not null)
        {
            await database.DropAsync();
        }
    }

    /// <summary>Services with <c>AddInfrastructure()</c> wired to this database as <paramref name="role"/>.</summary>
    public ServiceProvider BuildServices(DatabaseRole role)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Database.ConfigurationFor(role))
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }
}
