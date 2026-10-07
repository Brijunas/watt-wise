using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class DatabaseMigratorTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schema_steps_run_ef_core_first_then_hangfire()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        string[] names = [.. scope.ServiceProvider.GetServices<ISchemaStep>().Select(step => step.Name)];

        Assert.Equal(["ef-core-migrations", "hangfire-storage"], names);
    }

    [Fact]
    public async Task Migrating_an_up_to_date_database_again_succeeds()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        // The clone is already migrated by the fixture, so every step must be a no-op here.
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);
    }
}
