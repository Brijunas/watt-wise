using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using NodaTime;

using Npgsql;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

internal sealed class IdentityAccountRegistration(
    AppDbContext context,
    UserManager<AccountIdentity> users,
    IPasswordHasher<AccountIdentity> hasher,
    IClock clock) : IAccountRegistration
{
    public async Task<Guid?> FindAccountIdAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(identity => identity.NormalizedEmail == normalizedEmail)
            .Select(identity => (Guid?)identity.AccountId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Guid StageAccount(Language preferredLanguage)
    {
        Account account = Account.Create(preferredLanguage, clock.GetCurrentInstant());
        context.Accounts.Add(account);
        return account.Id;
    }

    public async Task<bool> CreateIdentityAsync(Guid accountId, string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        // UserManager saves the context, so the staged account and email request go in the same SaveChanges.
        // Nothing may save after a failure here: see "ASP.NET Core Identity" in accounts.md.
        try
        {
            IdentityResult result = await users.CreateAsync(AccountIdentity.Create(accountId, normalizedEmail), password);
            if (!result.Succeeded)
            {
                // The address was taken; drop the staged account and email request so a later save can't write them.
                context.ChangeTracker.Clear();
            }

            return result.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request committed the same address between the validator's check and the insert.
            // Drop the staged account and email request, so nothing of this attempt is saved later.
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public void HashAndDiscard(string password)
    {
        _ = hasher.HashPassword(new AccountIdentity(), password);
    }
}
