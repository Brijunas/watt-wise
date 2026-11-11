using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Changes a password in two steps. <see cref="CheckCurrentAsync"/> follows the order of a sign-in for a known account: a
/// locked-out account is refused, a wrong password is counted toward the lockout and the right one resets the count, all
/// through <see cref="LockoutCounter"/> on an identity read without tracking, so no tracked copy keeps a stale concurrency
/// stamp. It verifies with the hasher and not <c>CheckPasswordAsync</c>, which saves a new hash and stamp when the hash is
/// old. <see cref="ChangeAsync"/> loads the identity tracked and calls <c>ChangePasswordAsync</c>, which verifies again, sets
/// the hash, replaces the security stamp and saves once; the outstanding reset link is deleted in that save.
/// </summary>
internal sealed class IdentityPasswordChange(
    UserManager<AccountIdentity> users,
    IPasswordHasher<AccountIdentity> hasher,
    AppDbContext context,
    LockoutCounter lockoutCounter) : IPasswordChange
{
    public async Task<CurrentPasswordCheck> CheckCurrentAsync(Guid accountId, string currentPassword, CancellationToken cancellationToken)
    {
        AccountIdentity? identity = await context.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.AccountId == accountId, cancellationToken);
        if (identity is null)
        {
            return CurrentPasswordCheck.NotFound;
        }

        if (await users.IsLockedOutAsync(identity))
        {
            return CurrentPasswordCheck.LockedOut;
        }

        PasswordVerificationResult verification = identity.PasswordHash is null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(identity, identity.PasswordHash, currentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            await lockoutCounter.CountFailureAsync(identity.Id, cancellationToken);
            return CurrentPasswordCheck.WrongPassword;
        }

        await lockoutCounter.ResetFailureCountAsync(identity.Id, cancellationToken);
        return CurrentPasswordCheck.Verified;
    }

    public async Task<PasswordChangeOutcome> ChangeAsync(Guid accountId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        AccountIdentity? identity = await context.Users.SingleOrDefaultAsync(user => user.AccountId == accountId, cancellationToken);
        if (identity is null)
        {
            return PasswordChangeOutcome.NotFound();
        }

        // Staged now and saved by ChangePasswordAsync, so a change and the end of its link are one save. A failed change
        // saves nothing, and the transaction runner drops the staged deletion.
        EmailLinkToken? resetLink = await context.EmailLinkTokens.FindAsync(
            [identity.Id, UserManager<AccountIdentity>.ResetPasswordTokenPurpose], cancellationToken);
        if (resetLink is not null)
        {
            context.EmailLinkTokens.Remove(resetLink);
        }

        IdentityResult result = await users.ChangePasswordAsync(identity, currentPassword, newPassword);
        if (result.Succeeded)
        {
            return PasswordChangeOutcome.Changed(
                identity.SecurityStamp ?? throw new InvalidOperationException("ASP.NET Core Identity left no security stamp after a password change."));
        }

        // The validator has already checked the new password, so what is left is a wrong password or a lost race.
        string mismatch = users.ErrorDescriber.PasswordMismatch().Code;
        string concurrency = users.ErrorDescriber.ConcurrencyFailure().Code;
        if (result.Errors.Any(error => error.Code == mismatch))
        {
            return PasswordChangeOutcome.WrongPassword();
        }

        if (result.Errors.All(error => error.Code == concurrency))
        {
            return PasswordChangeOutcome.Conflict();
        }

        throw new InvalidOperationException("The password change was rejected for a reason other than the current password.");
    }
}
