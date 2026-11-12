using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityPasswordResetTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<PasswordResetOutcome> ResetAsync(ServiceProvider provider, Guid identityId, string token, string newPassword)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordReset>().ResetAsync(identityId, token, newPassword, Token);
    }

    private static async Task LockAsync(ServiceProvider provider, string email, int failures)
    {
        for (int attempt = 0; attempt < failures; attempt++)
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync(email, "wrong-password", Token);
        }
    }

    private static Task<(int Failed, DateTimeOffset? LockoutEnd)> LockoutOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(
            provider,
            identityId,
            async (users, identity) => (await users.GetAccessFailedCountAsync(identity), await users.GetLockoutEndDateAsync(identity)));

    private static Task<string> StampOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, identity) => users.GetSecurityStampAsync(identity))!;

    private sealed record Seeded(ServiceProvider Provider, AccountSeeder.Ids Ids, string Email, string Password) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private async Task<Seeded> SeedAsync(Action<IServiceCollection>? configure = null)
    {
        ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock(), configure);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        return new Seeded(provider, await AccountSeeder.CreateAccountAsync(provider, email, password), email, password);
    }

    [Fact]
    public async Task A_valid_link_resets_and_answers_the_account_id()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);

        PasswordResetOutcome outcome = await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        Assert.Equal(PasswordResetOutcome.Reset(account.Ids.AccountId), outcome);
    }

    [Fact]
    public async Task A_valid_link_sets_the_new_password_and_the_old_one_stops_working()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string newPassword = AccountsTestSupport.ValidPassword();

        await ResetAsync(account.Provider, account.Ids.IdentityId, token, newPassword);

        Assert.Equal(
            (true, false),
            (
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, newPassword),
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password)));
    }

    [Fact]
    public async Task A_valid_link_replaces_the_security_stamp()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string before = await StampOfAsync(account.Provider, account.Ids.IdentityId);

        await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        Assert.NotEqual(before, await StampOfAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_second_use_of_the_link_is_an_invalid_link()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        PasswordResetOutcome second = await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        Assert.Equal(PasswordResetOutcome.InvalidLink(), second);
    }

    [Fact]
    public async Task An_altered_token_is_an_invalid_link_and_leaves_the_password_alone()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string altered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        PasswordResetOutcome outcome = await ResetAsync(account.Provider, account.Ids.IdentityId, altered, AccountsTestSupport.ValidPassword());

        Assert.Equal(
            (PasswordResetOutcome.InvalidLink(), true),
            (outcome, await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password)));
    }

    [Fact]
    public async Task An_unknown_identity_is_an_invalid_link()
    {
        await using Seeded account = await SeedAsync();

        PasswordResetOutcome outcome = await ResetAsync(account.Provider, Guid.NewGuid(), "any-token", AccountsTestSupport.ValidPassword());

        Assert.Equal(PasswordResetOutcome.InvalidLink(), outcome);
    }

    [Fact]
    public async Task A_valid_link_lifts_a_lockout()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        await LockAsync(account.Provider, account.Email, 5);
        Assert.NotNull((await LockoutOfAsync(account.Provider, account.Ids.IdentityId)).LockoutEnd);

        await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        Assert.Equal((0, null), await LockoutOfAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_valid_link_clears_the_failed_attempt_count()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        await LockAsync(account.Provider, account.Email, 3);

        await ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword());

        Assert.Equal((0, null), await LockoutOfAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task An_invalid_link_leaves_the_lockout_and_the_count_as_they_were()
    {
        await using Seeded account = await SeedAsync();
        await LockAsync(account.Provider, account.Email, 5);
        (int failed, DateTimeOffset? lockedUntil) = await LockoutOfAsync(account.Provider, account.Ids.IdentityId);

        await ResetAsync(account.Provider, account.Ids.IdentityId, "not-a-token", AccountsTestSupport.ValidPassword());

        Assert.Equal((failed, lockedUntil), await LockoutOfAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_reset_before_it_reads_anything()
    {
        await using Seeded account = await SeedAsync();
        await using AsyncServiceScope scope = account.Provider.CreateAsyncScope();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scope.ServiceProvider.GetRequiredService<IPasswordReset>()
                .ResetAsync(account.Ids.IdentityId, "token", AccountsTestSupport.ValidPassword(), cancelled.Token));
    }

    // Both scopes load the identity and check the link before either saves; the first save uses the link up and the second
    // finds nothing left to change, which ASP.NET Core Identity reports as a concurrency failure.
    [Fact]
    public async Task Two_uses_of_the_same_link_at_once_let_exactly_one_reset_and_the_other_is_an_invalid_link()
    {
        SaveChangesBarrier barrier = new();
        await using Seeded account = await SeedAsync(services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        barrier.Arm(2);

        PasswordResetOutcome[] outcomes = await Task.WhenAll(
            ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword()),
            ResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword()));

        Assert.Equal((1, 1), (outcomes.Count(outcome => outcome.IsReset), outcomes.Count(outcome => !outcome.IsReset)));
    }
}
