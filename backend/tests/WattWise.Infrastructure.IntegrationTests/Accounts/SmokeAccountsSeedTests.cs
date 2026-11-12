using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class SmokeAccountsSeedTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string AdminRoleId = "0192e5a0-0000-7000-8000-000000000001";

    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task A_first_run_creates_a_confirmed_English_account_that_signs_in_with_the_password_and_holds_no_role()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        SmokeAccountsSupport.Snapshot stored = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (SeedOutcome.Created, true, PasswordCheckOutcome.Succeeded, "en", ""),
            (
                result.Value,
                stored.EmailConfirmed,
                await SmokeAccountsSupport.SignInAsync(cli, email, password),
                await SmokeAccountsSupport.LanguageOfAsync(cli, stored.AccountId),
                await AdminRoleSupport.RoleIdsOfAsync(cli, stored.AccountId)));
    }

    [Fact]
    public async Task A_first_run_for_the_admin_creates_the_account_with_the_Admin_role()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: true);

        SmokeAccountsSupport.Snapshot stored = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal((SeedOutcome.Created, AdminRoleId), (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, stored.AccountId)));
    }

    [Fact]
    public async Task A_first_run_sends_no_email_and_issues_no_link_token()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();

        await SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: false);

        SmokeAccountsSupport.Snapshot stored = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (0L, 0L),
            (
                await AccountsTestSupport.CountAsync(cli, $"""SELECT count(*) AS "Value" FROM app.email_requests WHERE account_id = {stored.AccountId}""", Token),
                await AccountsTestSupport.CountAsync(cli, $"""SELECT count(*) AS "Value" FROM app.email_link_tokens WHERE identity_id = {stored.IdentityId}""", Token)));
    }

    [Fact]
    public async Task A_second_run_with_the_same_input_changes_nothing_and_ends_no_session()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: true);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, before.AccountId);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: true);

        Assert.Equal(
            (SeedOutcome.Unchanged, before, "1|1"),
            (result.Value, await SmokeAccountsSupport.SnapshotAsync(cli, email), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task A_run_with_the_email_in_another_case_finds_the_same_account()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email.ToUpperInvariant(), password, admin: false);

        Assert.Equal((SeedOutcome.Unchanged, 1L), (result.Value, await SmokeAccountsSupport.IdentityCountAsync(cli, email)));
    }

    [Fact]
    public async Task A_changed_password_is_restored_the_security_stamp_rotates_and_the_accounts_sessions_end()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string wanted = AccountsTestSupport.ValidPassword();
        string changed = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, changed, admin: false);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, before.AccountId);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, wanted, admin: false);

        SmokeAccountsSupport.Snapshot after = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (SeedOutcome.Changed, PasswordCheckOutcome.Succeeded, PasswordCheckOutcome.InvalidCredentials, true, "0|0"),
            (
                result.Value,
                await SmokeAccountsSupport.SignInAsync(cli, email, wanted),
                await SmokeAccountsSupport.SignInAsync(cli, email, changed),
                after.SecurityStamp != before.SecurityStamp,
                await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task A_password_restore_keeps_another_accounts_sessions()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        await SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: false);
        string otherEmail = AccountsTestSupport.UniqueEmail();
        await SmokeAccountsSupport.SeedAsync(cli, otherEmail, AccountsTestSupport.ValidPassword(), admin: false);
        Guid otherSessionId = await SmokeAccountsSupport.StartSessionAsync(cli, (await SmokeAccountsSupport.SnapshotAsync(cli, otherEmail)).AccountId);

        await SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: false);

        Assert.Equal("1|1", await SessionRows.OfSessionAsync(cli, otherSessionId));
    }

    [Fact]
    public async Task An_account_without_a_password_hash_gets_the_password()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await AccountSeeder.CreateAccountAsync(cli, email, password: null);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        Assert.Equal(
            (SeedOutcome.Changed, PasswordCheckOutcome.Succeeded),
            (result.Value, await SmokeAccountsSupport.SignInAsync(cli, email, password)));
    }

    [Fact]
    public async Task Failed_sign_ins_are_cleared_without_replacing_the_stamp_or_ending_sessions()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, before.AccountId);
        await SmokeAccountsSupport.SignInAsync(cli, email, "wrong-password");

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        SmokeAccountsSupport.Snapshot after = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (SeedOutcome.Changed, 0, before.SecurityStamp, "1|1"),
            (result.Value, after.AccessFailedCount, after.SecurityStamp, await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task A_lockout_is_lifted_so_the_account_signs_in_again_without_replacing_the_stamp_or_ending_sessions()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, before.AccountId);
        await LockOutAsync(cli, before.IdentityId);
        Assert.Equal(PasswordCheckOutcome.LockedOut, await SmokeAccountsSupport.SignInAsync(cli, email, password));

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        SmokeAccountsSupport.Snapshot after = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (SeedOutcome.Changed, null, PasswordCheckOutcome.Succeeded, before.SecurityStamp, "1|1"),
            (
                result.Value,
                after.LockoutEnd,
                await SmokeAccountsSupport.SignInAsync(cli, email, password),
                after.SecurityStamp,
                await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task An_unconfirmed_email_is_confirmed_without_replacing_the_stamp()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await AccountSeeder.CreateAccountAsync(cli, email, password, confirmed: false);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        SmokeAccountsSupport.Snapshot after = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            (SeedOutcome.Changed, true, before.SecurityStamp, PasswordCheckOutcome.Succeeded),
            (result.Value, after.EmailConfirmed, after.SecurityStamp, await SmokeAccountsSupport.SignInAsync(cli, email, password)));
    }

    [Fact]
    public async Task A_plain_user_that_holds_the_Admin_role_loses_it_and_its_sessions_end()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: true);
        Guid accountId = (await SmokeAccountsSupport.SnapshotAsync(cli, email)).AccountId;
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, accountId);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        Assert.Equal(
            (SeedOutcome.Changed, "", "0|0"),
            (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, accountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task An_admin_missing_the_role_gets_it_back_and_keeps_its_sessions()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);
        Guid accountId = (await SmokeAccountsSupport.SnapshotAsync(cli, email)).AccountId;
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, accountId);

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: true);

        Assert.Equal(
            (SeedOutcome.Changed, AdminRoleId, "1|1"),
            (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, accountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task A_password_the_validator_would_have_stopped_fails_with_the_error_codes_only_and_stores_no_identity_and_no_account()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        await using AsyncServiceScope scope = cli.CreateAsyncScope();
        ISmokeAccounts accounts = scope.ServiceProvider.GetRequiredService<ISmokeAccounts>();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => accounts.EnsureAsync(email, "x", Token));

        Assert.Equal(
            (
                // characterization: Identity PasswordValidator order
                "Seeding the account failed: PasswordTooShort, PasswordRequiresNonAlphanumeric, PasswordRequiresDigit, PasswordRequiresUpper.",
                0L,
                0L),
            (thrown.Message, await SmokeAccountsSupport.IdentityCountAsync(cli, email), await SmokeAccountsSupport.AccountsWithoutIdentityAsync(cli)));
    }

    [Fact]
    public async Task A_cancelled_token_stops_before_the_account_is_touched()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture);
        string email = AccountsTestSupport.UniqueEmail();
        await using AsyncServiceScope scope = cli.CreateAsyncScope();
        ISmokeAccounts accounts = scope.ServiceProvider.GetRequiredService<ISmokeAccounts>();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => accounts.EnsureAsync(email, AccountsTestSupport.ValidPassword(), cancelled.Token));

        Assert.Equal(0L, await SmokeAccountsSupport.IdentityCountAsync(cli, email));
    }

    // The lockout is "active" only while it ends after now (IdentitySmokeAccounts: LockoutEnd > now).
    [Theory]
    [InlineData(1, SeedOutcome.Changed)]
    [InlineData(0, SeedOutcome.Unchanged)]
    [InlineData(-1, SeedOutcome.Unchanged)]
    public async Task A_lockout_counts_only_while_it_ends_after_now(int secondsFromNow, SeedOutcome expected)
    {
        FakeClock clock = new(Instant.FromUtc(2026, 11, 11, 12, 0));
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(
            fixture, configure: services => services.Replace(ServiceDescriptor.Singleton<IClock>(clock)));
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);
        await SetLockoutEndAsync(cli, (await SmokeAccountsSupport.SnapshotAsync(cli, email)).IdentityId, clock.GetCurrentInstant().ToDateTimeOffset().AddSeconds(secondsFromNow));

        Result<SeedOutcome> result = await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false);

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task The_role_removal_is_rolled_back_when_ending_the_sessions_fails()
    {
        FailingCommandInterceptor fault = new();
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(
            fixture, configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(fault)));
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, password, admin: true);
        Guid accountId = (await SmokeAccountsSupport.SnapshotAsync(cli, email)).AccountId;
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, accountId);
        fault.Arm("DELETE FROM app.sessions");

        Exception? thrown = await Record.ExceptionAsync(() => SmokeAccountsSupport.SeedAsync(cli, email, password, admin: false));

        Assert.Equal(
            ("Injected fault.", AdminRoleId, "1|1"),
            (thrown?.Message, await AdminRoleSupport.RoleIdsOfAsync(cli, accountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task The_password_restore_is_rolled_back_when_ending_the_sessions_fails()
    {
        FailingCommandInterceptor fault = new();
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(
            fixture, configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(fault)));
        string email = AccountsTestSupport.UniqueEmail();
        string stored = AccountsTestSupport.ValidPassword();
        await SmokeAccountsSupport.SeedAsync(cli, email, stored, admin: false);
        SmokeAccountsSupport.Snapshot before = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Guid sessionId = await SmokeAccountsSupport.StartSessionAsync(cli, before.AccountId);
        fault.Arm("DELETE FROM app.sessions");

        Exception? thrown = await Record.ExceptionAsync(
            () => SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: false));

        SmokeAccountsSupport.Snapshot after = await SmokeAccountsSupport.SnapshotAsync(cli, email);
        Assert.Equal(
            ("Injected fault.", before.PasswordHash, before.SecurityStamp, PasswordCheckOutcome.Succeeded, "1|1"),
            (
                thrown?.Message,
                after.PasswordHash,
                after.SecurityStamp,
                await SmokeAccountsSupport.SignInAsync(cli, email, stored),
                await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task Outside_Development_seeding_throws_and_writes_nothing()
    {
        await using ServiceProvider cli = SmokeAccountsSupport.BuildCli(fixture, "Production");
        string email = AccountsTestSupport.UniqueEmail();

        Exception? thrown = await Record.ExceptionAsync(
            () => SmokeAccountsSupport.SeedAsync(cli, email, AccountsTestSupport.ValidPassword(), admin: true));

        Assert.Equal(
            ("Smoke accounts can be seeded only in the Development environment.", 0L),
            (thrown?.Message, await SmokeAccountsSupport.IdentityCountAsync(cli, email)));
    }

    private static async Task LockOutAsync(ServiceProvider provider, Guid identityId) =>
        await SetLockoutEndAsync(provider, identityId, DateTimeOffset.UtcNow.AddHours(1));

    private static async Task SetLockoutEndAsync(ServiceProvider provider, Guid identityId, DateTimeOffset lockoutEnd)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = await users.FindByIdAsync(identityId.ToString()) ?? throw new InvalidOperationException("The identity does not exist.");
        IdentityResult result = await users.SetLockoutEndDateAsync(identity, lockoutEnd);
        Assert.True(result.Succeeded);
    }
}
