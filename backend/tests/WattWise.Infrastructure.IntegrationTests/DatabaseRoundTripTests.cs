using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

public class DatabaseRoundTripTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Api_connects_as_the_api_role()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string user = await db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync(Token);

        Assert.Equal("api", user);
    }

    [Fact]
    public async Task Cli_sees_the_initial_migration_applied()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Contains("20261005094112_Initial", await db.Database.GetAppliedMigrationsAsync(Token));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Token));
    }

    [Fact]
    public async Task Api_cannot_read_the_migration_history()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM app.__ef_migrations_history").ToListAsync(Token));

        Assert.Equal("42501", exception.SqlState);
    }
}
