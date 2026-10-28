using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountPersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task A_second_identity_for_the_same_account_is_rejected()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid accountId = await AccountsTestSupport.SaveAccountAsync(provider, Token);
        await using (AsyncServiceScope firstScope = provider.CreateAsyncScope())
        {
            AppDbContext firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstDb.Users.Add(AccountIdentity.Create(accountId, AccountsTestSupport.UniqueEmail()));
            await firstDb.SaveChangesAsync(Token);
        }

        // A second context: within one, EF treats the account's AccountIdentity as one-to-one and replaces the first.
        await using AsyncServiceScope secondScope = provider.CreateAsyncScope();
        AppDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        secondDb.Users.Add(AccountIdentity.Create(accountId, AccountsTestSupport.UniqueEmail()));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => secondDb.SaveChangesAsync(Token));

        Assert.Equal("23505:ix_identity_users_account_id", AccountsTestSupport.ViolationOf(exception));
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_identity_role_links_and_email_links_but_not_the_role()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        Account account = AccountsTestSupport.NewAccount();
        account.AddRole(role.Id);
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            AppDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Roles.Add(role);
            seedDb.Accounts.Add(account);
            await seedDb.SaveChangesAsync(Token);
        }

        Guid identityId = await AccountsTestSupport.CreateIdentityForAccountAsync(
            provider, account.Id, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());
        _ = await EmailLinks.IssueAsync(provider, identityId, EmailLinkKind.Confirmation);
        Assert.Equal((1L, 1L, 1L, 1L), await CountsAsync(provider, account.Id, identityId, role.Id));

        await using (AsyncServiceScope deleteScope = provider.CreateAsyncScope())
        {
            AppDbContext deleteDb = deleteScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await deleteDb.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync(Token);
        }

        // identities, role links, email links, roles
        Assert.Equal((0L, 0L, 0L, 1L), await CountsAsync(provider, account.Id, identityId, role.Id));
    }

    // characterization: AccountConfiguration.cs:32-39 (the stored text of a language)
    [Theory]
    [InlineData(Language.Lt, "lt")]
    [InlineData(Language.En, "en")]
    public async Task The_preferred_language_is_stored_as_lt_or_en(Language language, string expected)
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Account account = AccountsTestSupport.NewAccount(language);
        await SaveAsync(provider, account);

        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string stored = await db.Database
            .SqlQuery<string>($"SELECT preferred_language AS \"Value\" FROM app.accounts WHERE id = {account.Id}")
            .SingleAsync(Token);
        Assert.Equal(expected, stored);
    }

    // characterization: AccountConfiguration.cs:41-49 (the language read back from its stored text)
    [Theory]
    [InlineData(Language.Lt)]
    [InlineData(Language.En)]
    public async Task The_preferred_language_round_trips(Language language)
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Account account = AccountsTestSupport.NewAccount(language);
        await SaveAsync(provider, account);

        Account loaded = await LoadAsync(provider, account.Id);

        Assert.Equal(language, loaded.PreferredLanguage);
    }

    // characterization: AccountConfiguration.cs:47 (any other stored text fails to load)
    [Fact]
    public async Task A_stored_language_other_than_lt_or_en_fails_to_load()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Guid id = Guid.NewGuid();
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO app.accounts (id, preferred_language, created_at) VALUES ({id}, 'xx', now())", Token);
        }

        Assert.Equal(1L, await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.accounts WHERE id = {id}", Token));

        await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(provider, id));
    }

    [Fact]
    public async Task CreatedAt_keeps_microseconds_and_nothing_finer()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Instant whole = Instant.FromUtc(2026, 10, 26, 12, 0);
        // 123,456,400 ns is 123,456 microseconds and 400 ns; PostgreSQL stores microseconds only.
        Account account = Account.Create(Language.Lt, whole.PlusNanoseconds(123_456_400));
        await SaveAsync(provider, account);

        Account loaded = await LoadAsync(provider, account.Id);

        Assert.Equal(whole.PlusNanoseconds(123_456_000), loaded.CreatedAt);
    }

    private static async Task SaveAsync(ServiceProvider provider, Account account)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);
    }

    private static async Task<Account> LoadAsync(ServiceProvider provider, Guid id)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == id, Token);
    }

    private static async Task<(long Identities, long RoleLinks, long EmailLinks, long Roles)> CountsAsync(
        ServiceProvider provider, Guid accountId, Guid identityId, Guid roleId) =>
        (
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.identity_users WHERE id = {identityId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE account_id = {accountId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.email_link_tokens WHERE identity_id = {identityId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.roles WHERE id = {roleId}", Token));
}
