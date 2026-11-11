using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>
/// A fault injected into the bulk delete of sessions, the last step of a reset or a change, must leave the password, the
/// sessions and the reset link as they were. Each test runs the same steps as the handler inside the real runner.
/// </summary>
public class PasswordAtomicityTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string SessionDelete = "DELETE FROM app.sessions";

    private static CancellationToken Token => AccountsTestSupport.Token;

    private sealed record Seeded(ServiceProvider Provider, AccountSeeder.Ids Ids, string Password, FailingCommandInterceptor Fault) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private async Task<Seeded> SeedAsync()
    {
        FailingCommandInterceptor fault = new();
        ServiceProvider provider = fixture.BuildAccountServices(
            EmailLinks.NewClock(), services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(fault)));
        string password = AccountsTestSupport.ValidPassword();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(provider, AccountsTestSupport.UniqueEmail(), password);
        await AddSessionAsync(provider, ids.AccountId);
        await AddSessionAsync(provider, ids.AccountId);
        return new Seeded(provider, ids, password, fault);
    }

    private static async Task AddSessionAsync(ServiceProvider provider, Guid accountId)
    {
        Instant now = Instant.FromUtc(2026, 10, 26, 12, 0);
        Session session = Session.Start(accountId, "Firefox on Linux", "stamp", Guid.NewGuid().ToByteArray(), now, now + Duration.FromDays(7));
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISessionRepository>().Add(session);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
    }

    private static Task<long> SessionRowsAsync(ServiceProvider provider, Guid accountId) =>
        AccountsTestSupport.CountAsync(provider, $"""SELECT count(*) AS "Value" FROM app.sessions WHERE account_id = {accountId}""", Token);

    private static Task<long> ResetLinkRowsAsync(ServiceProvider provider, Guid identityId)
    {
        string purpose = EmailLinks.Purpose(EmailLinkKind.Reset);
        return AccountsTestSupport.CountAsync(
            provider,
            $"""SELECT count(*) AS "Value" FROM app.email_link_tokens WHERE identity_id = {identityId} AND purpose = {purpose}""",
            Token);
    }

    private static Task<string> StampOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, identity) => users.GetSecurityStampAsync(identity))!;

    private static Task<Result<int>> RunResetAsync(ServiceProvider provider, Guid identityId, string token, string newPassword) =>
        RunAsync(
            provider,
            async (scope, cancellationToken) =>
            {
                PasswordResetOutcome outcome = await scope.GetRequiredService<IPasswordReset>().ResetAsync(identityId, token, newPassword, cancellationToken);
                return await scope.GetRequiredService<IAccountSessions>().EndAllAsync(outcome.AccountId!.Value, cancellationToken);
            });

    private static Task<Result<int>> RunChangeAsync(ServiceProvider provider, Guid accountId, string current, string next) =>
        RunAsync(
            provider,
            async (scope, cancellationToken) =>
            {
                _ = await scope.GetRequiredService<IPasswordChange>().ChangeAsync(accountId, current, next, cancellationToken);
                return await scope.GetRequiredService<IAccountSessions>().EndAllAsync(accountId, cancellationToken);
            });

    private static async Task<Result<int>> RunAsync(ServiceProvider provider, Func<IServiceProvider, CancellationToken, Task<int>> steps)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().ExecuteAsync<int>(
            async token => await steps(scope.ServiceProvider, token), Token);
    }

    [Fact]
    public async Task Without_a_fault_a_reset_and_the_end_of_the_sessions_commit_together()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string newPassword = AccountsTestSupport.ValidPassword();

        Result<int> result = await RunResetAsync(account.Provider, account.Ids.IdentityId, token, newPassword);

        Assert.Equal(
            (2, true, 0L),
            (
                result.Value,
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, newPassword),
                await SessionRowsAsync(account.Provider, account.Ids.AccountId)));
    }

    [Fact]
    public async Task A_fault_in_the_session_delete_of_a_reset_leaves_the_old_password_the_sessions_and_the_link()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string stampBefore = await StampOfAsync(account.Provider, account.Ids.IdentityId);
        account.Fault.Arm(SessionDelete);

        Exception? thrown = await Record.ExceptionAsync(
            () => RunResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword()));

        Assert.Equal(
            ("Injected fault.", 1, true, 2L, 1L, stampBefore),
            (
                thrown?.Message,
                account.Fault.Failures,
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password),
                await SessionRowsAsync(account.Provider, account.Ids.AccountId),
                await ResetLinkRowsAsync(account.Provider, account.Ids.IdentityId),
                await StampOfAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task After_a_fault_in_a_reset_the_same_link_still_resets_the_password()
    {
        await using Seeded account = await SeedAsync();
        string token = await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        account.Fault.Arm(SessionDelete);
        _ = await Record.ExceptionAsync(
            () => RunResetAsync(account.Provider, account.Ids.IdentityId, token, AccountsTestSupport.ValidPassword()));
        string newPassword = AccountsTestSupport.ValidPassword();

        Microsoft.AspNetCore.Identity.IdentityResult redeemed =
            await EmailLinks.RedeemAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset, token, newPassword);

        Assert.Equal(
            (true, true),
            (redeemed.Succeeded, await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, newPassword)));
    }

    [Fact]
    public async Task Without_a_fault_a_change_and_the_end_of_the_sessions_commit_together()
    {
        await using Seeded account = await SeedAsync();
        await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string next = AccountsTestSupport.ValidPassword();

        Result<int> result = await RunChangeAsync(account.Provider, account.Ids.AccountId, account.Password, next);

        Assert.Equal(
            (2, true, 0L, 0L),
            (
                result.Value,
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, next),
                await SessionRowsAsync(account.Provider, account.Ids.AccountId),
                await ResetLinkRowsAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task A_fault_in_the_session_delete_of_a_change_leaves_the_old_password_the_sessions_and_the_reset_link()
    {
        await using Seeded account = await SeedAsync();
        await EmailLinks.IssueAsync(account.Provider, account.Ids.IdentityId, EmailLinkKind.Reset);
        string stampBefore = await StampOfAsync(account.Provider, account.Ids.IdentityId);
        account.Fault.Arm(SessionDelete);

        Exception? thrown = await Record.ExceptionAsync(
            () => RunChangeAsync(account.Provider, account.Ids.AccountId, account.Password, AccountsTestSupport.ValidPassword()));

        Assert.Equal(
            ("Injected fault.", 1, true, 2L, 1L, stampBefore),
            (
                thrown?.Message,
                account.Fault.Failures,
                await AccountsTestSupport.CheckPasswordAsync(account.Provider, account.Ids.IdentityId, account.Password),
                await SessionRowsAsync(account.Provider, account.Ids.AccountId),
                await ResetLinkRowsAsync(account.Provider, account.Ids.IdentityId),
                await StampOfAsync(account.Provider, account.Ids.IdentityId)));
    }
}
