using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Checks a sign-in with ASP.NET Core Identity, in this order: an unknown email (hashed against a dummy so the time
/// matches), a locked-out account (the password is still hashed, so the time matches), a wrong password (counted toward
/// the lockout by <see cref="LockoutCounter"/>; the one that locks still answers as invalid credentials), and only after
/// the right password an unconfirmed email. The password is verified with the hasher and not <c>CheckPasswordAsync</c>:
/// when a hash needs upgrading, that call saves the new hash and replaces the security stamp, which would end the
/// account's other sessions. A rehash here updates the stored hash alone, so it never rotates the stamp.
/// </summary>
internal sealed class IdentityPasswordSignIn(
    UserManager<AccountIdentity> users,
    IPasswordHasher<AccountIdentity> hasher,
    AppDbContext context,
    LockoutCounter lockoutCounter) : IPasswordSignIn
{
    public async Task<PasswordCheck> CheckAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        // UserManager's lookup and checks take no CancellationToken, so stop here before they start.
        cancellationToken.ThrowIfCancellationRequested();

        AccountIdentity? identity = await users.FindByEmailAsync(normalizedEmail);
        if (identity is null)
        {
            _ = hasher.HashPassword(new AccountIdentity(), password);
            return PasswordCheck.InvalidCredentials();
        }

        if (await users.IsLockedOutAsync(identity))
        {
            // Same work as a password check, so a locked account can't be told apart by its time. The result is not used.
            if (identity.PasswordHash is null)
            {
                _ = hasher.HashPassword(identity, password);
            }
            else
            {
                _ = hasher.VerifyHashedPassword(identity, identity.PasswordHash, password);
            }

            return PasswordCheck.LockedOut(Instant.FromDateTimeOffset(identity.LockoutEnd!.Value));
        }

        PasswordVerificationResult verification = identity.PasswordHash is null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(identity, identity.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await lockoutCounter.CountFailureAsync(identity.Id, cancellationToken);
            return PasswordCheck.InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await StoreNewHashAsync(identity, password, cancellationToken);
        }

        await lockoutCounter.ResetFailureCountAsync(identity.Id, cancellationToken);
        return await users.IsEmailConfirmedAsync(identity)
            ? PasswordCheck.Succeeded(identity.AccountId, identity.SecurityStamp!)
            : PasswordCheck.EmailNotConfirmed();
    }

    /// <summary>One UPDATE of the hash and the concurrency stamp. The security stamp stays, so the account's sessions survive.</summary>
    private async Task StoreNewHashAsync(AccountIdentity identity, string password, CancellationToken cancellationToken)
    {
        string hash = hasher.HashPassword(identity, password);
        string stamp = Guid.NewGuid().ToString();
        string verifiedHash = identity.PasswordHash!;

        // Only while the row still holds the hash that was verified: a change or reset that committed since would
        // otherwise have its new hash overwritten by the old password's rehash. When nothing matches, the next sign-in retries.
        _ = await context.Users
            .Where(user => user.Id == identity.Id && user.PasswordHash == verifiedHash)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.PasswordHash, hash)
                    .SetProperty(user => user.ConcurrencyStamp, stamp),
                cancellationToken);
    }
}
