using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Checks a sign-in with ASP.NET Core Identity, in this order: an unknown email (hashed against a dummy so the time
/// matches), a locked-out account (the password is still hashed, so the time matches), a wrong password (counted toward
/// the lockout; the one that locks still answers as invalid credentials), and only after the right password an
/// unconfirmed email.
/// </summary>
/// <remarks>
/// Failures are counted with one UPDATE statement and not with <c>UserManager.AccessFailedAsync</c>: that reads the count,
/// adds one and saves under the row's concurrency stamp, so parallel wrong guesses clash and drop increments, and far more
/// than the allowed number would fit in a lockout window. The statement does what ASP.NET Core Identity does (count, and at
/// the limit lock and reset the count) from the options and the clock, and gives the row a new concurrency stamp.
/// </remarks>
internal sealed class IdentityPasswordSignIn(
    UserManager<AccountIdentity> users,
    IPasswordHasher<AccountIdentity> hasher,
    AppDbContext context,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider) : IPasswordSignIn
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

            return PasswordCheck.LockedOut();
        }

        if (!await users.CheckPasswordAsync(identity, password))
        {
            await CountFailureAsync(identity.Id, cancellationToken);
            return PasswordCheck.InvalidCredentials();
        }

        await ResetFailureCountAsync(identity.Id, cancellationToken);
        return await users.IsEmailConfirmedAsync(identity)
            ? PasswordCheck.Succeeded(identity.AccountId)
            : PasswordCheck.EmailNotConfirmed();
    }

    private async Task CountFailureAsync(Guid identityId, CancellationToken cancellationToken)
    {
        LockoutOptions lockout = identityOptions.Value.Lockout;
        int limit = lockout.MaxFailedAccessAttempts;
        DateTimeOffset lockedUntil = timeProvider.GetUtcNow() + lockout.DefaultLockoutTimeSpan;
        string stamp = Guid.NewGuid().ToString();

        // Every right-hand side reads the row as it was before the statement, so the count and the lock agree.
        _ = await context.Users
            .Where(user => user.Id == identityId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.AccessFailedCount, user => user.AccessFailedCount + 1 >= limit ? 0 : user.AccessFailedCount + 1)
                    .SetProperty(user => user.LockoutEnd, user => user.AccessFailedCount + 1 >= limit ? lockedUntil : user.LockoutEnd)
                    .SetProperty(user => user.ConcurrencyStamp, stamp),
                cancellationToken);
    }

    private async Task ResetFailureCountAsync(Guid identityId, CancellationToken cancellationToken)
    {
        string stamp = Guid.NewGuid().ToString();

        // Only when there is something to reset. A wrong guess counted at the same moment may be wiped with it; that is
        // harmless, since whoever signs in here has just given the right password.
        _ = await context.Users
            .Where(user => user.Id == identityId && user.AccessFailedCount > 0)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.AccessFailedCount, 0)
                    .SetProperty(user => user.ConcurrencyStamp, stamp),
                cancellationToken);
    }
}
