using Microsoft.AspNetCore.Identity;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Resets a password with <c>UserManager.ResetPasswordAsync</c>, which checks the link, sets the hash, replaces the security
/// stamp and saves once. The lockout is cleared on the tracked identity, so it rides the same save. After a failure nothing
/// else may save in this scope: the link is already marked used.
/// </summary>
internal sealed class IdentityPasswordReset(UserManager<AccountIdentity> users) : IPasswordReset
{
    public async Task<PasswordResetOutcome> ResetAsync(Guid accountIdentityId, string token, string newPassword, CancellationToken cancellationToken)
    {
        // UserManager's calls take no CancellationToken, so stop here before they start.
        cancellationToken.ThrowIfCancellationRequested();

        AccountIdentity? identity = await users.FindByIdAsync(accountIdentityId.ToString());
        if (identity is null)
        {
            return PasswordResetOutcome.InvalidLink();
        }

        identity.LockoutEnd = null;
        identity.AccessFailedCount = 0;

        IdentityResult result = await users.ResetPasswordAsync(identity, token, newPassword);
        if (result.Succeeded)
        {
            return PasswordResetOutcome.Reset(identity.AccountId);
        }

        // The validator has already checked the password, so what is left is a bad link or a link used twice at once.
        string invalidToken = users.ErrorDescriber.InvalidToken().Code;
        string concurrency = users.ErrorDescriber.ConcurrencyFailure().Code;
        if (result.Errors.All(error => error.Code == invalidToken || error.Code == concurrency))
        {
            return PasswordResetOutcome.InvalidLink();
        }

        throw new InvalidOperationException("The password reset was rejected for a reason other than the link.");
    }
}
