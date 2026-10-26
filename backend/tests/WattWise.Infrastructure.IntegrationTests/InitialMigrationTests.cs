using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

public class InitialMigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly string[] AccountTables =
    [
        "accounts", "identity_users", "identity_user_logins", "identity_user_tokens",
        "roles", "permissions", "account_roles", "role_permissions", "email_link_tokens",
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_creates_the_account_tables()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string[] present = await db.Database
            .SqlQuery<string>($"SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'app'")
            .ToArrayAsync(Token);

        Assert.Equal(AccountTables.Order(StringComparer.Ordinal), present.Order(StringComparer.Ordinal));
    }
}
