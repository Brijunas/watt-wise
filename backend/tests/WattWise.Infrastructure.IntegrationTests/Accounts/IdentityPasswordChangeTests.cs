using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityPasswordChangeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<CurrentPasswordCheck> CheckAsync(ServiceProvider provider, Guid accountId, string password)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordChange>().CheckCurrentAsync(accountId, password, Token);
    }

    private static async Task<PasswordChangeOutcome> ChangeAsync(ServiceProvider provider, Guid accountId, string current, string next)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordChange>().ChangeAsync(accountId, current, next, Token);
    }

    private static async Task FailAsync(ServiceProvider provider, Guid accountId, int attempts)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            await CheckAsync(provider, accountId, "wrong-password");
        }
    }

    private static Task<(int Failed, DateTimeOffset? LockoutEnd)> LockoutOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(
            provider,
            identityId,
            async (users, identity) => (await users.GetAccessFailedCountAsync(identity), await users.GetLockoutEndDateAsync(identity)));

    private static Task<string> StampOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, identity) => users.GetSecurityStampAsync(identity))!;

    private static Task<long> LinkRowsAsync(ServiceProvider provider, Guid identityId, EmailLinkKind kind)
    {
        string purpose = EmailLinks.Purpose(kind);
        return AccountsTestSupport.CountAsync(
            provider,
            $"""SELECT count(*) AS "Value" FROM app.email_link_tokens WHERE identity_id = {identityId} AND purpose = {purpose}""",
            Token);
    }

    private sealed record Seeded(ServiceProvider Provider, AccountSeeder.Ids Ids, string Password) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private async Task<Seeded> SeedAsync(Action<IServiceCollection>? configure = null, bool withPassword = true)
    {
        ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock(), configure);
        string password = AccountsTestSupport.ValidPassword();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(
            provider, AccountsTestSupport.UniqueEmail(), withPassword ? password : null);
        return new Seeded(provider, ids, password);
    }

    [Fact]
    public async Task The_right_current_password_is_verified()
    {
        await using Seeded account = await SeedAsync();

        Assert.Equal(CurrentPasswordCheck.Verified, await CheckAsync(account.Provider, account.Ids.AccountId, account.Password));
    }

    [Fact]
    public async Task A_wrong_current_password_is_WrongPassword_and_counts_one_failure()
    {
        await using Seeded account = await SeedAsync();

        CurrentPasswordCheck check = await CheckAsync(account.Provider, account.Ids.AccountId, "wrong-password");

        Assert.Equal(
            (CurrentPasswordCheck.WrongPassword, 1),
            (check, (await LockoutOfAsync(account.Provider, account.Ids.IdentityId)).Failed));
    }

    [Fact]
    public async Task An_account_without_a_password_hash_is_WrongPassword_for_any_password()
    {
        await using Seeded account = await SeedAsync(withPassword: false);

        Assert.Equal(CurrentPasswordCheck.WrongPassword, await CheckAsync(account.Provider, account.Ids.AccountId, "any-password"));
    }

    [Fact]
    public async Task An_unknown_account_is_NotFound()
    {
        await using Seeded account = await SeedAsync();

        Assert.Equal(CurrentPasswordCheck.NotFound, await CheckAsync(account.Provider, Guid.NewGuid(), account.Password));
    }

    [Fact]
    public async Task The_right_password_resets_the_failure_count()
    {
        await using Seeded account = await SeedAsync();
        await FailAsync(account.Provider, account.Ids.AccountId, 2);

        await CheckAsync(account.Provider, account.Ids.AccountId, account.Password);

        Assert.Equal(0, (await LockoutOfAsync(account.Provider, account.Ids.IdentityId)).Failed);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        await using Seeded account = await SeedAsync();
        await FailAsync(account.Provider, account.Ids.AccountId, 5);

        (int failed, DateTimeOffset? lockedUntil) = await LockoutOfAsync(account.Provider, account.Ids.IdentityId);

        Assert.Equal((0, new DateTimeOffset(2026, 10, 26, 12, 15, 0, TimeSpan.Zero)), (failed, lockedUntil));
    }

    [Fact]
    public async Task A_locked_account_is_LockedOut_even_with_the_right_password_and_adds_no_failure()
    {
        await using Seeded account = await SeedAsync();
        await FailAsync(account.Provider, account.Ids.AccountId, 5);

        CurrentPasswordCheck check = await CheckAsync(account.Provider, account.Ids.AccountId, account.Password);

        Assert.Equal((CurrentPasswordCheck.LockedOut, 0), (check, (await LockoutOfAsync(account.Provider, account.Ids.IdentityId)).Failed));
    }

    [Fact]
    public async Task A_change_sets_the_new_password_and_the_old_one_stops_working()
    {
        await using Seeded account = await SeedAsync();
        string next = AccountsTestSupport.ValidPassword();

        await ChangeAsync(account.Provider, account.Ids.AccountId, account.Password, next);

        Assert.Equal(
            (true, false),
            (
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, next),
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password)));
    }

    [Fact]
    public async Task A_change_answers_the_security_stamp_it_stored_and_that_differs_from_the_old_one()
    {
        await using Seeded account = await SeedAsync();
        string before = await StampOfAsync(account.Provider, account.Ids.IdentityId);

        PasswordChangeOutcome outcome = await ChangeAsync(account.Provider, account.Ids.AccountId, account.Password, AccountsTestSupport.ValidPassword());

        string stored = await StampOfAsync(account.Provider, account.Ids.IdentityId);
        Assert.Equal((PasswordChangeOutcome.Changed(stored), true), (outcome, stored != before));
    }

    [Fact]
    public async Task A_wrong_current_password_is_WrongPassword_and_changes_nothing()
    {
        await using Seeded account = await SeedAsync();
        string before = await StampOfAsync(account.Provider, account.Ids.IdentityId);

        PasswordChangeOutcome outcome = await ChangeAsync(
            account.Provider, account.Ids.AccountId, "wrong-password", AccountsTestSupport.ValidPassword());

        Assert.Equal(
            (PasswordChangeOutcome.WrongPassword(), true, before),
            (
                outcome,
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password),
                await StampOfAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task An_unknown_account_cannot_be_changed()
    {
        await using Seeded account = await SeedAsync();

        PasswordChangeOutcome outcome = await ChangeAsync(account.Provider, Guid.NewGuid(), account.Password, AccountsTestSupport.ValidPassword());

        Assert.Equal(PasswordChangeOutcome.NotFound(), outcome);
    }

    [Fact]
    public async Task A_change_deletes_the_accounts_reset_link_and_no_other_link()
    {
        await using Seeded account = await SeedAsync();
        AccountSeeder.Ids other = await AccountSeeder.CreateAccountAsync(
            account.Provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());
        await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Confirmation);
        await EmailLinks.IssueAsync(account.Provider, other.IdentityId, EmailLinkKind.Reset);

        await ChangeAsync(account.Provider, account.Ids.AccountId, account.Password, AccountsTestSupport.ValidPassword());

        Assert.Equal(
            (0L, 1L, 1L),
            (
                await LinkRowsAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset),
                await LinkRowsAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Confirmation),
                await LinkRowsAsync(account.Provider, other.IdentityId, EmailLinkKind.Reset)));
    }

    [Fact]
    public async Task A_change_with_a_wrong_current_password_saves_nothing_so_the_reset_link_stays()
    {
        await using Seeded account = await SeedAsync();
        await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);

        await ChangeAsync(account.Provider, account.Ids.AccountId, "wrong-password", AccountsTestSupport.ValidPassword());

        Assert.Equal(1L, await LinkRowsAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset));
    }

    // Both scopes load the identity before either saves; the first save changes the row and the second finds it changed,
    // which ASP.NET Core Identity reports as a concurrency failure.
    [Fact]
    public async Task Two_changes_at_once_let_exactly_one_succeed_and_the_other_is_a_conflict()
    {
        SaveChangesBarrier barrier = new();
        await using Seeded account = await SeedAsync(services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        barrier.Arm(2);

        PasswordChangeOutcome[] outcomes = await Task.WhenAll(
            ChangeAsync(account.Provider, account.Ids.AccountId, account.Password, AccountsTestSupport.ValidPassword()),
            ChangeAsync(account.Provider, account.Ids.AccountId, account.Password, AccountsTestSupport.ValidPassword()));

        Assert.Equal(
            "Changed,Conflict",
            string.Join(",", outcomes.Select(outcome => outcome.Kind.ToString()).Order()));
    }
}
