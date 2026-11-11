namespace WattWise.Application.Accounts;

/// <summary>Changes the password of a signed-in account. The two steps run apart so a wrong guess is counted even though the change is rolled back.</summary>
public interface IPasswordChange
{
    /// <summary>
    /// Checks that the account is not locked out and that <paramref name="currentPassword"/> is right. A wrong password is
    /// counted toward the lockout and a right one resets the count, each with one statement that commits at once. Call it
    /// outside <see cref="ITransactionRunner"/>.
    /// </summary>
    Task<CurrentPasswordCheck> CheckCurrentAsync(Guid accountId, string currentPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the new password in one save and deletes the account's outstanding reset link with it. Call it inside
    /// <see cref="ITransactionRunner"/>; nothing else may save in the same scope after a failure.
    /// </summary>
    Task<PasswordChangeOutcome> ChangeAsync(Guid accountId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}
