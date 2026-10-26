using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using WattWise.Domain.Identities;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Identity;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>What the account tests share: services with Identity as the Api has them, and a way to make an account with its identity.</summary>
internal static class AccountsTestSupport
{
    /// <summary>A new password that follows every rule: one character of each class, then a random tail.</summary>
    public static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    /// <summary>
    /// Services for the <c>api</c> role, Identity included. Pass <paramref name="clock"/> (a <c>FakeClock</c>)
    /// to control the time email links expire by, and <paramref name="configure"/> to add or replace services.
    /// </summary>
    public static ServiceProvider BuildAccountServices(
        this DatabaseFixture fixture, IClock? clock = null, Action<IServiceCollection>? configure = null) =>
        fixture.BuildServices(
            DatabaseRole.Api,
            services =>
            {
                if (clock is not null)
                {
                    services.Replace(ServiceDescriptor.Singleton(clock));
                }

                configure?.Invoke(services);
            });

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static Account NewAccount(Language language = Language.Lt) =>
        Account.Create(language, SystemClock.Instance.GetCurrentInstant());

    /// <summary>Saves a new account and creates its identity through <see cref="UserManager{TUser}"/>.</summary>
    public static async Task<AccountIdentity> CreateIdentityAsync(
        IServiceProvider services, string email, string password, CancellationToken cancellationToken)
    {
        AppDbContext db = services.GetRequiredService<AppDbContext>();
        Account account = NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        UserManager<AccountIdentity> users = services.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = AccountIdentity.Create(account.Id, email);
        IdentityResult result = await users.CreateAsync(identity, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return identity;
    }
}
