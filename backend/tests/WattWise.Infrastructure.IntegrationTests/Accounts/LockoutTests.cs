using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class LockoutTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // UserManager reads its time from a TimeProvider in the service provider, so a fixed one gives exact lockout ends.
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_fifth_failed_attempt_locks_the_identity_until_exactly_15_minutes_later()
    {
        await using ServiceProvider provider = BuildProvider();
        AccountIdentityIds ids = await CreateAsync(provider);

        await FailAsync(provider, ids.IdentityId, 5);

        // Locked out means the end lies ahead of the fake's 12:00 and lockout is enabled for the new identity.
        (DateTimeOffset? lockoutEnd, bool lockedOut) = await AccountsTestSupport.InScopeAsync(
            provider,
            ids.IdentityId,
            async (users, accountIdentity) => (await users.GetLockoutEndDateAsync(accountIdentity), await users.IsLockedOutAsync(accountIdentity)));
        Assert.Equal((new DateTimeOffset(2026, 10, 26, 12, 15, 0, TimeSpan.Zero), true), (lockoutEnd, lockedOut));
    }

    [Fact]
    public async Task Four_failed_attempts_leave_the_identity_unlocked()
    {
        await using ServiceProvider provider = BuildProvider();
        AccountIdentityIds ids = await CreateAsync(provider);

        await FailAsync(provider, ids.IdentityId, 4);

        DateTimeOffset? lockoutEnd = await AccountsTestSupport.InScopeAsync(
            provider, ids.IdentityId, (users, accountIdentity) => users.GetLockoutEndDateAsync(accountIdentity));
        Assert.Null(lockoutEnd);
    }

    private ServiceProvider BuildProvider() =>
        fixture.BuildAccountServices(configure: services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedTimeProvider(Now))));

    private static Task<AccountIdentityIds> CreateAsync(ServiceProvider provider) =>
        AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), AccountsTestSupport.Token);

    // One scope per attempt, as each sign-in request has its own.
    private static async Task FailAsync(ServiceProvider provider, Guid identityId, int attempts)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            await AccountsTestSupport.InScopeAsync(provider, identityId, async (users, accountIdentity) =>
                (await users.AccessFailedAsync(accountIdentity)).Succeeded);
        }
    }
}
