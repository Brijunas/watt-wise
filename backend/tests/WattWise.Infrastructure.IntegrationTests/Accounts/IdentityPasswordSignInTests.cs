using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityPasswordSignInTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static FakeClock NewClock() => new(Instant.FromUtc(2026, 11, 11, 12, 0));

    private static async Task<PasswordCheck> CheckAsync(ServiceProvider provider, string email, string password)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync(email, password, Token);
    }

    private static async Task FailAsync(ServiceProvider provider, string email, int attempts)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            await CheckAsync(provider, email, "wrong-password");
        }
    }

    private static Task<int> FailedCountAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, identity) => users.GetAccessFailedCountAsync(identity));

    private static Task<string> StampOfAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (users, identity) => users.GetSecurityStampAsync(identity))!;

    private async Task<Seeded> SeedAsync(FakeClock clock, bool confirmed = true)
    {
        ServiceProvider provider = fixture.BuildAccountServices(clock);
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(provider, email, password, confirmed);
        return new Seeded(provider, ids, email, password);
    }

    private sealed record Seeded(ServiceProvider Provider, AccountSeeder.Ids Ids, string Email, string Password) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    [Fact]
    public async Task The_right_password_of_a_confirmed_account_succeeds_with_its_account_id()
    {
        await using Seeded account = await SeedAsync(NewClock());

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(PasswordCheck.Succeeded(account.Ids.AccountId, await StampOfAsync(account.Provider, account.Ids.IdentityId)), result);
    }

    [Fact]
    public async Task An_account_without_a_password_hash_is_InvalidCredentials_and_counts_one_failure()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(NewClock());
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(provider, email, password: null);

        PasswordCheck result = await CheckAsync(provider, email, AccountsTestSupport.ValidPassword());

        Assert.Equal((PasswordCheck.InvalidCredentials(), 1), (result, await FailedCountAsync(provider, ids.IdentityId)));
    }

    [Fact]
    public async Task An_unknown_email_is_InvalidCredentials()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(NewClock());

        PasswordCheck result = await CheckAsync(provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());

        Assert.Equal(PasswordCheck.InvalidCredentials(), result);
    }

    [Fact]
    public async Task A_wrong_password_is_InvalidCredentials_and_counts_one_failure()
    {
        await using Seeded account = await SeedAsync(NewClock());

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, "wrong-password");

        Assert.Equal((PasswordCheck.InvalidCredentials(), 1), (result, await FailedCountAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task The_right_password_resets_the_failure_count()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 2);

        await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(0, await FailedCountAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task The_fifth_wrong_password_is_still_InvalidCredentials_and_locks_the_account_for_15_minutes()
    {
        FakeClock clock = NewClock();
        await using Seeded account = await SeedAsync(clock);
        await FailAsync(account.Provider, account.Email, 4);

        PasswordCheck fifth = await CheckAsync(account.Provider, account.Email, "wrong-password");

        DateTimeOffset? lockoutEnd = await AccountsTestSupport.InScopeAsync(
            account.Provider, account.Ids.IdentityId, (users, identity) => users.GetLockoutEndDateAsync(identity));
        Assert.Equal(
            (PasswordCheck.InvalidCredentials(), new DateTimeOffset(2026, 11, 11, 12, 15, 0, TimeSpan.Zero)),
            (fifth, lockoutEnd));
    }

    [Fact]
    public async Task A_locked_account_is_LockedOut_even_with_the_right_password()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 5);

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(PasswordCheck.LockedOut(), result);
    }

    [Fact]
    public async Task A_wrong_password_on_a_locked_account_is_LockedOut_and_adds_no_failure()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 5);
        int before = await FailedCountAsync(account.Provider, account.Ids.IdentityId);

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, "wrong-password");

        Assert.Equal((PasswordCheck.LockedOut(), before), (result, await FailedCountAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task The_lock_is_still_on_one_second_before_15_minutes_pass()
    {
        FakeClock clock = NewClock();
        await using Seeded account = await SeedAsync(clock);
        await FailAsync(account.Provider, account.Email, 5);
        clock.Advance(Duration.FromMinutes(15) - Duration.FromSeconds(1));

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(PasswordCheck.LockedOut(), result);
    }

    [Fact]
    public async Task The_right_password_succeeds_once_more_than_15_minutes_have_passed()
    {
        FakeClock clock = NewClock();
        await using Seeded account = await SeedAsync(clock);
        await FailAsync(account.Provider, account.Email, 5);
        // ASP.NET Core Identity still counts the lock-out end instant itself as locked.
        clock.Advance(Duration.FromMinutes(15) + Duration.FromSeconds(1));

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(PasswordCheck.Succeeded(account.Ids.AccountId, await StampOfAsync(account.Provider, account.Ids.IdentityId)), result);
    }

    [Fact]
    public async Task An_unconfirmed_email_with_the_right_password_is_EmailNotConfirmed()
    {
        await using Seeded account = await SeedAsync(NewClock(), confirmed: false);

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(PasswordCheck.EmailNotConfirmed(), result);
    }

    [Fact]
    public async Task An_unconfirmed_email_with_a_wrong_password_is_InvalidCredentials_so_it_is_not_revealed()
    {
        await using Seeded account = await SeedAsync(NewClock(), confirmed: false);

        PasswordCheck result = await CheckAsync(account.Provider, account.Email, "wrong-password");

        Assert.Equal(PasswordCheck.InvalidCredentials(), result);
    }

    [Fact]
    public async Task The_email_is_looked_up_by_its_normalized_form()
    {
        await using Seeded account = await SeedAsync(NewClock());

        PasswordCheck result = await CheckAsync(account.Provider, account.Email.ToUpperInvariant(), account.Password);

        Assert.Equal(PasswordCheck.Succeeded(account.Ids.AccountId, await StampOfAsync(account.Provider, account.Ids.IdentityId)), result);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_check_before_it_reads_anything()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(NewClock());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync("a@example.com", "pw", cancelled.Token));
    }

    [Fact]
    public async Task The_time_provider_follows_the_clock()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        clock.Advance(Duration.FromMinutes(20));

        Assert.Equal(
            new DateTimeOffset(2026, 11, 11, 12, 20, 0, TimeSpan.Zero),
            provider.GetRequiredService<TimeProvider>().GetUtcNow());
    }

    private static Task<string> StampAsync(ServiceProvider provider, Guid identityId) =>
        AccountsTestSupport.InScopeAsync(provider, identityId, (_, identity) => Task.FromResult(identity.ConcurrencyStamp!));

    [Fact]
    public async Task Parallel_wrong_passwords_all_count_and_the_fifth_locks_the_account()
    {
        FakeClock clock = NewClock();
        await using Seeded account = await SeedAsync(clock);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] attempts = [.. Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            await CheckAsync(account.Provider, account.Email, "wrong-password");
        }, Token))];

        start.SetResult();
        await Task.WhenAll(attempts);

        DateTimeOffset? lockoutEnd = await AccountsTestSupport.InScopeAsync(
            account.Provider, account.Ids.IdentityId, (users, identity) => users.GetLockoutEndDateAsync(identity));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 12, 15, 0, TimeSpan.Zero), lockoutEnd);
    }

    [Fact]
    public async Task Four_wrong_passwords_count_four_and_set_no_lock()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 4);

        DateTimeOffset? lockoutEnd = await AccountsTestSupport.InScopeAsync(
            account.Provider, account.Ids.IdentityId, (users, identity) => users.GetLockoutEndDateAsync(identity));
        Assert.Equal((4, null), (await FailedCountAsync(account.Provider, account.Ids.IdentityId), lockoutEnd));
    }

    [Fact]
    public async Task The_fifth_wrong_password_sets_the_lock_and_sends_the_count_back_to_zero()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 5);

        Assert.Equal(0, await FailedCountAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_failed_attempt_gives_the_row_a_new_concurrency_stamp()
    {
        await using Seeded account = await SeedAsync(NewClock());
        string before = await StampAsync(account.Provider, account.Ids.IdentityId);

        await CheckAsync(account.Provider, account.Email, "wrong-password");

        Assert.NotEqual(before, await StampAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_success_with_no_failures_leaves_the_concurrency_stamp_alone()
    {
        await using Seeded account = await SeedAsync(NewClock());
        string before = await StampAsync(account.Provider, account.Ids.IdentityId);

        await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(before, await StampAsync(account.Provider, account.Ids.IdentityId));
    }

    [Fact]
    public async Task A_success_after_a_failure_resets_the_count_and_gives_the_row_a_new_stamp()
    {
        await using Seeded account = await SeedAsync(NewClock());
        await FailAsync(account.Provider, account.Email, 1);
        string before = await StampAsync(account.Provider, account.Ids.IdentityId);

        await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal(
            (0, true),
            (await FailedCountAsync(account.Provider, account.Ids.IdentityId), before != await StampAsync(account.Provider, account.Ids.IdentityId)));
    }

    [Fact]
    public async Task A_success_does_not_reset_another_accounts_failure_count()
    {
        await using Seeded account = await SeedAsync(NewClock());
        string otherEmail = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids other = await AccountSeeder.CreateAccountAsync(account.Provider, otherEmail, AccountsTestSupport.ValidPassword());
        await FailAsync(account.Provider, otherEmail, 2);
        await FailAsync(account.Provider, account.Email, 1);

        await CheckAsync(account.Provider, account.Email, account.Password);

        Assert.Equal((0, 2), (await FailedCountAsync(account.Provider, account.Ids.IdentityId), await FailedCountAsync(account.Provider, other.IdentityId)));
    }

    [Fact]
    public async Task A_failure_counts_only_the_row_that_failed()
    {
        await using Seeded account = await SeedAsync(NewClock());
        AccountSeeder.Ids other = await AccountSeeder.CreateAccountAsync(account.Provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());

        await FailAsync(account.Provider, account.Email, 1);

        Assert.Equal((1, 0), (await FailedCountAsync(account.Provider, account.Ids.IdentityId), await FailedCountAsync(account.Provider, other.IdentityId)));
    }

    private (ServiceProvider Provider, RecordingPasswordHasher Hasher) BuildWithRecordingHasher()
    {
        RecordingPasswordHasher hasher = new();
        ServiceProvider provider = fixture.BuildAccountServices(
            NewClock(), services => services.Replace(ServiceDescriptor.Scoped<Microsoft.AspNetCore.Identity.IPasswordHasher<WattWise.Infrastructure.Accounts.AccountIdentity>>(_ => hasher)));
        return (provider, hasher);
    }

    [Fact]
    public async Task A_locked_account_still_has_the_given_password_verified_when_it_is_right()
    {
        (ServiceProvider provider, RecordingPasswordHasher hasher) = BuildWithRecordingHasher();
        await using ServiceProvider _ = provider;
        string email = AccountsTestSupport.UniqueEmail();
        string password = AccountsTestSupport.ValidPassword();
        await AccountSeeder.CreateAccountAsync(provider, email, password);
        await FailAsync(provider, email, 5);
        hasher.Calls.Clear();

        PasswordCheck result = await CheckAsync(provider, email, password);

        Assert.Equal((PasswordCheck.LockedOut(), $"Verify:{password}"), (result, string.Join(",", hasher.Calls)));
    }

    [Fact]
    public async Task A_locked_account_still_has_the_given_password_verified_when_it_is_wrong()
    {
        (ServiceProvider provider, RecordingPasswordHasher hasher) = BuildWithRecordingHasher();
        await using ServiceProvider _ = provider;
        string email = AccountsTestSupport.UniqueEmail();
        await AccountSeeder.CreateAccountAsync(provider, email, AccountsTestSupport.ValidPassword());
        await FailAsync(provider, email, 5);
        hasher.Calls.Clear();

        PasswordCheck result = await CheckAsync(provider, email, "wrong-password");

        Assert.Equal((PasswordCheck.LockedOut(), "Verify:wrong-password"), (result, string.Join(",", hasher.Calls)));
    }

    [Fact]
    public async Task A_locked_account_without_a_password_hash_has_the_given_password_hashed_instead()
    {
        (ServiceProvider provider, RecordingPasswordHasher hasher) = BuildWithRecordingHasher();
        await using ServiceProvider _ = provider;
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(provider, email, password: null);
        await AccountsTestSupport.InScopeAsync(
            provider, ids.IdentityId, async (users, identity) => (await users.SetLockoutEndDateAsync(identity, DateTimeOffset.Parse("2030-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture))).Succeeded);
        hasher.Calls.Clear();

        PasswordCheck result = await CheckAsync(provider, email, "any-password");

        Assert.Equal((PasswordCheck.LockedOut(), "Hash"), (result, string.Join(",", hasher.Calls)));
    }

    [Fact]
    public async Task An_unknown_email_has_the_given_password_hashed_so_the_time_matches()
    {
        (ServiceProvider provider, RecordingPasswordHasher hasher) = BuildWithRecordingHasher();
        await using ServiceProvider _ = provider;

        PasswordCheck result = await CheckAsync(provider, AccountsTestSupport.UniqueEmail(), "any-password");

        Assert.Equal((PasswordCheck.InvalidCredentials(), "Hash"), (result, string.Join(",", hasher.Calls)));
    }
}
