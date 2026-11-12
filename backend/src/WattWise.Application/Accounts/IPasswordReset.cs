namespace WattWise.Application.Accounts;

/// <summary>Sets a new password from a reset link.</summary>
public interface IPasswordReset
{
    /// <summary>
    /// Checks the link, sets the password, uses up the link and clears the lockout, in one save. Nothing else may save in the
    /// same scope after an invalid link: the link is already marked used.
    /// </summary>
    /// <param name="accountIdentityId">The <c>AccountIdentityId</c> in the link.</param>
    /// <param name="token">The link's token.</param>
    /// <param name="newPassword">As typed; it has passed <see cref="IPasswordPolicy"/>.</param>
    /// <param name="cancellationToken">Cancels the reset before it starts.</param>
    Task<PasswordResetOutcome> ResetAsync(Guid accountIdentityId, string token, string newPassword, CancellationToken cancellationToken);
}
