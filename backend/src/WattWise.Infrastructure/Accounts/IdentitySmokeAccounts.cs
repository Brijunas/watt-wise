using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Creates or corrects a smoke-test account through <c>UserManager</c>, one save per call. Nothing is sent. A new password is
/// hashed and stored with a new security stamp, so the sessions that started with the old one stop refreshing; nothing else
/// replaces the stamp. A failure throws: the password already passed the validator, so only a race or a broken database is left.
/// </summary>
internal sealed class IdentitySmokeAccounts(
    AppDbContext context,
    UserManager<AccountIdentity> users,
    IPasswordHasher<AccountIdentity> hasher,
    IClock clock,
    IHostEnvironment environment) : ISmokeAccounts
{
    public async Task<EnsuredAccount> EnsureAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        // A second guard behind the Cli's: smoke accounts have known passwords and must never exist elsewhere.
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Smoke accounts can be seeded only in the Development environment.");
        }

        // UserManager's calls take no CancellationToken, so stop here before they start.
        cancellationToken.ThrowIfCancellationRequested();

        AccountIdentity? identity = await users.FindByEmailAsync(normalizedEmail);
        if (identity is null)
        {
            return await CreateAsync(normalizedEmail, password);
        }

        bool confirm = !identity.EmailConfirmed;
        DateTimeOffset now = clock.GetCurrentInstant().ToDateTimeOffset();
        bool unlock = identity.AccessFailedCount > 0 || identity.LockoutEnd > now;
        bool passwordDiffers = identity.PasswordHash is null
            || hasher.VerifyHashedPassword(identity, identity.PasswordHash, password) == PasswordVerificationResult.Failed;

        if (!confirm && !unlock && !passwordDiffers)
        {
            return new EnsuredAccount(identity.AccountId, SeedOutcome.Unchanged, PasswordChanged: false);
        }

        identity.EmailConfirmed = true;
        identity.AccessFailedCount = 0;
        identity.LockoutEnd = null;

        IdentityResult result;
        if (passwordDiffers)
        {
            identity.PasswordHash = hasher.HashPassword(identity, password);
            result = await users.UpdateSecurityStampAsync(identity);
        }
        else
        {
            result = await users.UpdateAsync(identity);
        }

        ThrowIfFailed(result);
        return new EnsuredAccount(identity.AccountId, SeedOutcome.Changed, passwordDiffers);
    }

    private async Task<EnsuredAccount> CreateAsync(string normalizedEmail, string password)
    {
        Account account = Account.Create(Language.En, clock.GetCurrentInstant());
        context.Accounts.Add(account);

        AccountIdentity identity = AccountIdentity.Create(account.Id, normalizedEmail);
        identity.EmailConfirmed = true;

        // UserManager saves the staged account in the same SaveChanges.
        IdentityResult result = await users.CreateAsync(identity, password);
        // A failure throws inside the caller's transaction, which rolls back and drops the staged account.
        ThrowIfFailed(result);
        return new EnsuredAccount(account.Id, SeedOutcome.Created, PasswordChanged: false);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            // Codes only: a description could repeat what was typed.
            throw new InvalidOperationException($"Seeding the account failed: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
