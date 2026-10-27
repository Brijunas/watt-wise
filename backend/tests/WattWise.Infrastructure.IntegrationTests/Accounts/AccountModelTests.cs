using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountModelTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_account_and_its_identity_round_trip()
    {
        Account account = AccountsTestSupport.NewAccount(Language.En);
        string email = AccountsTestSupport.UniqueEmail();
        await using (ServiceProvider writer = fixture.BuildServices(DatabaseRole.Api))
        await using (AsyncServiceScope scope = writer.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Accounts.Add(account);
            db.Users.Add(AccountIdentity.Create(account.Id, email));
            await db.SaveChangesAsync(Token);
        }

        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope readScope = reader.CreateAsyncScope();
        AppDbContext readDb = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account loaded = await readDb.Accounts.SingleAsync(a => a.Id == account.Id, Token);
        AccountIdentity accountIdentity = await readDb.Users.SingleAsync(i => i.AccountId == account.Id, Token);

        Assert.Equal(Language.En, loaded.PreferredLanguage);
        // PostgreSQL keeps microseconds, NodaTime ticks.
        Assert.True(Math.Abs((account.CreatedAt - loaded.CreatedAt).TotalNanoseconds) < 1000);
        Assert.Equal(email, accountIdentity.Email);
    }

    [Theory]
    [InlineData(Language.Lt, "lt")]
    [InlineData(Language.En, "en")]
    public async Task The_preferred_language_is_stored_as_lowercase_text(Language language, string expected)
    {
        Account account = AccountsTestSupport.NewAccount(language);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);

        string stored = await db.Database
            .SqlQuery<string>($"SELECT preferred_language AS \"Value\" FROM app.accounts WHERE id = {account.Id}")
            .SingleAsync(Token);

        Assert.Equal(expected, stored);
    }

    [Fact]
    public async Task A_second_identity_for_the_same_account_is_rejected()
    {
        Account account = AccountsTestSupport.NewAccount();
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        db.Users.Add(AccountIdentity.Create(account.Id, AccountsTestSupport.UniqueEmail()));
        await db.SaveChangesAsync(Token);

        // A second context: within one, EF treats the account's AccountIdentity as one-to-one and replaces the first.
        await using AsyncServiceScope secondScope = provider.CreateAsyncScope();
        AppDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        secondDb.Users.Add(AccountIdentity.Create(account.Id, AccountsTestSupport.UniqueEmail()));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => secondDb.SaveChangesAsync(Token));

        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(AccountsTestSupport.UniqueViolation, postgres.SqlState);
        Assert.Contains("account_id", postgres.ConstraintName);
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_identity_and_role_links_but_not_the_role()
    {
        Account account = AccountsTestSupport.NewAccount();
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        account.AddRole(role.Id);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(role);
        db.Accounts.Add(account);
        db.Users.Add(AccountIdentity.Create(account.Id, AccountsTestSupport.UniqueEmail()));
        await db.SaveChangesAsync(Token);
        Assert.Equal(1, await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE account_id = {account.Id}", Token));

        await db.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync(Token);

        Assert.Equal(0, await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.identity_users WHERE account_id = {account.Id}", Token));
        Assert.Equal(0, await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE account_id = {account.Id}", Token));
        Assert.Equal(1, await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.roles WHERE id = {role.Id}", Token));
    }
}
