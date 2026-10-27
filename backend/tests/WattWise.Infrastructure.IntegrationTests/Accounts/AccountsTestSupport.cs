using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>What the account tests share: services with ASP.NET Core Identity as the Api has them, and a way to make an account with its AccountIdentity.</summary>
internal static class AccountsTestSupport
{
    /// <summary>PostgreSQL's SQLSTATE <c>unique_violation</c>.</summary>
    public const string UniqueViolation = "23505";

    /// <summary>A new password that follows every rule: one character of each class, then a random tail.</summary>
    public static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    /// <summary>
    /// Services for the <c>api</c> role, ASP.NET Core Identity included. Pass <paramref name="clock"/> (a <c>FakeClock</c>)
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

    /// <summary>
    /// Saves a new account and creates its AccountIdentity through <see cref="UserManager{TUser}"/>, in a scope of its own,
    /// and returns the ids, so a test reads the stored rows through its own scope.
    /// </summary>
    public static async Task<AccountIdentityIds> CreateAccountIdentityAsync(
        ServiceProvider provider, string email, string password, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity accountIdentity = AccountIdentity.Create(account.Id, email);
        IdentityResult result = await users.CreateAsync(accountIdentity, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return new AccountIdentityIds(account.Id, accountIdentity.Id);
    }

    /// <summary>Counts the rows a <c>SELECT count(*) AS "Value" …</c> query returns, in a new scope, not the one that wrote them.</summary>
    public static async Task<long> CountAsync(ServiceProvider provider, FormattableString sql, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<long>(sql).SingleAsync(cancellationToken);
    }
}
