using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using Npgsql;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>What the account tests share: services with ASP.NET Core Identity as the Api has them, and a way to make an account with its AccountIdentity.</summary>
internal static class AccountsTestSupport
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The SQLSTATE and constraint name of a unique or primary key violation, as <c>23505:ix_name</c>.</summary>
    public static string ViolationOf(Exception? exception)
    {
        DbUpdateException update = Assert.IsType<DbUpdateException>(exception);
        PostgresException postgres = Assert.IsType<PostgresException>(update.InnerException);
        return $"{postgres.SqlState}:{postgres.ConstraintName}";
    }

    /// <summary>A new password that follows every rule: one character of each class, then a random tail.</summary>
    public static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    /// <summary>
    /// Services for the <c>api</c> role, ASP.NET Core Identity included. Pass <paramref name="clock"/> (a <c>FakeClock</c>, the
    /// <see cref="IClock"/> email links expire by) and use <paramref name="configure"/> to add or replace services, such as the
    /// <see cref="TimeProvider"/> ASP.NET Core Identity reads for lockout.
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
        Guid accountId = await SaveAccountAsync(provider, cancellationToken);
        Guid identityId = await CreateIdentityForAccountAsync(provider, accountId, email, password);
        return new AccountIdentityIds(accountId, identityId);
    }

    /// <summary>Saves a new account in a scope of its own and returns its id.</summary>
    public static async Task<Guid> SaveAccountAsync(ServiceProvider provider, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    /// <summary>Creates the AccountIdentity of an existing account through <see cref="UserManager{TUser}"/>, in a scope of its own, and returns its id.</summary>
    public static async Task<Guid> CreateIdentityForAccountAsync(
        ServiceProvider provider, Guid accountId, string email, string password)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity accountIdentity = AccountIdentity.Create(accountId, email);
        IdentityResult result = await users.CreateAsync(accountIdentity, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return accountIdentity.Id;
    }

    public static async Task<T> InScopeAsync<T>(
        ServiceProvider provider, Guid identityId, Func<UserManager<AccountIdentity>, AccountIdentity, Task<T>> action)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity accountIdentity = await users.FindByIdAsync(identityId.ToString())
            ?? throw new InvalidOperationException("The AccountIdentity does not exist.");
        return await action(users, accountIdentity);
    }

    public static Task<bool> CheckPasswordAsync(ServiceProvider provider, Guid identityId, string password) =>
        InScopeAsync(provider, identityId, (users, accountIdentity) => users.CheckPasswordAsync(accountIdentity, password));

    /// <summary>Counts the rows a <c>SELECT count(*) AS "Value" …</c> query returns, in a new scope, not the one that wrote them.</summary>
    public static async Task<long> CountAsync(ServiceProvider provider, FormattableString sql, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQuery<long>(sql).SingleAsync(cancellationToken);
    }
}
