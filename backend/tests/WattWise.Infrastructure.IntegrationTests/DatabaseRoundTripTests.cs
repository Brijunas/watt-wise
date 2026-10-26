using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Cli_sees_every_migration_applied()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        IEnumerable<string> applied = await db.Database.GetAppliedMigrationsAsync(Token);
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Token));
    }
}
