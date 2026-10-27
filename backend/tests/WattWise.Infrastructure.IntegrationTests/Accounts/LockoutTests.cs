using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class LockoutTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ASP.NET Core Identity takes the time from DateTimeOffset.UtcNow, not from TimeProvider, so the lockout end
    // can only be checked as a window around now (and there is no way to test it expiring).
    [Fact]
    public async Task The_fifth_failed_attempt_locks_the_identity_for_15_minutes()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AccountIdentity identity = await AccountsTestSupport.CreateIdentityAsync(
            scope.ServiceProvider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token);
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            await users.AccessFailedAsync(identity);
        }

        Assert.False(await users.IsLockedOutAsync(identity));

        DateTimeOffset before = DateTimeOffset.UtcNow;
        await users.AccessFailedAsync(identity);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.True(await users.IsLockedOutAsync(identity));
        DateTimeOffset? lockoutEnd = await users.GetLockoutEndDateAsync(identity);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value, before.AddMinutes(15), after.AddMinutes(15));
    }
}
