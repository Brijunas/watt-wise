using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// Migrates databases that are missing what the steps install. Each test destroys part of the clone, which no other test in the class reads.
/// </summary>
public class MigrateFromScratchTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => PlatformSql.Token;

    [Fact]
    public async Task Pending_migrations_are_applied_to_a_database_without_history()
    {
        await PlatformSql.ExecuteAsync(fixture, DatabaseRole.Cli, "DROP SCHEMA app CASCADE");
        await PlatformSql.ExecuteAsync(fixture, DatabaseRole.Cli, "DELETE FROM migrations.__ef_migrations_history");

        await MigrateAsync();

        List<long> accounts = await PlatformSql.QueryAsync<long>(
            fixture, DatabaseRole.Cli, "SELECT count(*) AS \"Value\" FROM pg_tables WHERE schemaname = 'app' AND tablename = 'accounts'");
        Assert.Equal([1L], accounts);
    }

    // characterization: the table name is Hangfire.PostgreSql's install script, version 1.21.1 (backend/Directory.Packages.props:13).
    [Fact]
    public async Task Hangfire_tables_are_installed_into_an_empty_schema()
    {
        await PlatformSql.ExecuteAsync(fixture, DatabaseRole.Cli, "DROP SCHEMA hangfire CASCADE");
        await PlatformSql.ExecuteAsync(fixture, DatabaseRole.Cli, "CREATE SCHEMA hangfire");

        await MigrateAsync();

        List<long> jobs = await PlatformSql.QueryAsync<long>(
            fixture, DatabaseRole.Cli, "SELECT count(*) AS \"Value\" FROM pg_tables WHERE schemaname = 'hangfire' AND tablename = 'job'");
        Assert.Equal([1L], jobs);
    }

    private async Task MigrateAsync()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);
    }
}
