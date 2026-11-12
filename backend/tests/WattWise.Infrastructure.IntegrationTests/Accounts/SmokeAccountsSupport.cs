using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>
/// Runs the seed handler the way <c>WattWise.Cli seed smoke-accounts</c> does: on the <c>cli</c> role's services, each call in a
/// scope of its own, and reads an account back through another scope.
/// </summary>
internal static class SmokeAccountsSupport
{
    /// <summary>What a stored smoke account looks like, as the seed command cares about it.</summary>
    public sealed record Snapshot(
        Guid AccountId,
        Guid IdentityId,
        bool EmailConfirmed,
        int AccessFailedCount,
        DateTimeOffset? LockoutEnd,
        string SecurityStamp,
        string ConcurrencyStamp,
        string? PasswordHash);

    /// <summary>Services for the <c>cli</c> role that run as <paramref name="environmentName"/>, which the seeding adapter checks.</summary>
    public static ServiceProvider BuildCli(
        DatabaseFixture fixture, string environmentName = "Development", Action<IServiceCollection>? configure = null) =>
        fixture.BuildServices(
            DatabaseRole.Cli,
            services =>
            {
                services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
                configure?.Invoke(services);
            });

    public static async Task<Result<SeedOutcome>> SeedAsync(ServiceProvider cli, string email, string password, bool admin)
    {
        await using AsyncServiceScope scope = cli.CreateAsyncScope();
        SeedSmokeAccountCommandHandler handler = new(
            scope.ServiceProvider.GetRequiredService<IEmailAddressNormalizer>(),
            scope.ServiceProvider.GetRequiredService<ISmokeAccounts>(),
            scope.ServiceProvider.GetRequiredService<IAccountRoles>(),
            scope.ServiceProvider.GetRequiredService<IAccountSessions>(),
            scope.ServiceProvider.GetRequiredService<ITransactionRunner>());
        return await handler.Handle(new SeedSmokeAccountCommand(email, password, admin), AccountsTestSupport.Token);
    }

    public static async Task<Snapshot> SnapshotAsync(ServiceProvider reader, string email)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("The account does not exist.");
        return new Snapshot(
            identity.AccountId,
            identity.Id,
            identity.EmailConfirmed,
            identity.AccessFailedCount,
            identity.LockoutEnd,
            identity.SecurityStamp!,
            identity.ConcurrencyStamp!,
            identity.PasswordHash);
    }

    public static async Task<PasswordCheckOutcome> SignInAsync(ServiceProvider provider, string email, string password)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        PasswordCheck check = await scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync(email, password, AccountsTestSupport.Token);
        return check.Outcome;
    }

    public static async Task<string> LanguageOfAsync(ServiceProvider reader, Guid accountId)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"""SELECT preferred_language AS "Value" FROM app.accounts WHERE id = {accountId}""")
            .SingleAsync(AccountsTestSupport.Token);
    }

    /// <summary>Starts a session for the account, stored as sign-in does, and returns its id.</summary>
    public static async Task<Guid> StartSessionAsync(ServiceProvider provider, Guid accountId)
    {
        Instant signedInAt = Instant.FromUtc(2026, 11, 11, 12, 0);
        WattWise.Domain.Accounts.Session session = SessionSeeder.Start(accountId, "Firefox on Linux", signedInAt, signedInAt + Duration.FromDays(7));
        await SessionSeeder.AddAsync(provider, session);
        return session.Id;
    }

    /// <summary>Counts the identities stored for a normalized email.</summary>
    public static Task<long> IdentityCountAsync(ServiceProvider reader, string email) =>
        AccountsTestSupport.CountAsync(
            reader, $"""SELECT count(*) AS "Value" FROM app.identity_users WHERE upper(email) = {email.ToUpperInvariant()}""", AccountsTestSupport.Token);

    /// <summary>Counts the accounts that have no identity: the staged account a failed create must not leave behind.</summary>
    public static Task<long> AccountsWithoutIdentityAsync(ServiceProvider reader) =>
        AccountsTestSupport.CountAsync(
            reader,
            $"""SELECT count(*) AS "Value" FROM app.accounts a WHERE NOT EXISTS (SELECT 1 FROM app.identity_users i WHERE i.account_id = a.id)""",
            AccountsTestSupport.Token);
}
