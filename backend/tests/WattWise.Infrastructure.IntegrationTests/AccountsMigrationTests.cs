using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class AccountsMigrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string Initial = "20261005094112_Initial";
    private const string Accounts = "20261026152554_Accounts";

    private static readonly string[] AccountTables =
    [
        "accounts", "identity_users", "identity_user_logins", "identity_user_tokens",
        "roles", "permissions", "account_roles", "role_permissions", "email_link_tokens",
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrating_a_database_at_the_initial_migration_applies_accounts()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.GetService<IMigrator>().MigrateAsync(Initial, Token);

        Assert.DoesNotContain(Accounts, await db.Database.GetAppliedMigrationsAsync(Token));
        Assert.Empty(await TablesAsync(db));

        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);

        Assert.Contains(Accounts, await db.Database.GetAppliedMigrationsAsync(Token));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Token));
        Assert.Equal(AccountTables.Order(StringComparer.Ordinal), await TablesAsync(db));
    }

    private static async Task<string[]> TablesAsync(AppDbContext db)
    {
        string[] present = await db.Database
            .SqlQuery<string>($"SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'app'")
            .ToArrayAsync(Token);
        return [.. present.Where(table => AccountTables.Contains(table)).Order(StringComparer.Ordinal)];
    }
}
