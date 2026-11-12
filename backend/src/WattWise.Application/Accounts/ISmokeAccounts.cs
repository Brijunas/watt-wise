namespace WattWise.Application.Accounts;

/// <summary>Brings a smoke-test account to a known state without sending any email.</summary>
public interface ISmokeAccounts
{
    /// <summary>
    /// Makes sure the account exists with a confirmed email, the given password and no lockout, changing only what differs.
    /// A new account has the English language. The security stamp changes only when the password differs.
    /// </summary>
    /// <param name="normalizedEmail">The email as <see cref="IEmailAddressNormalizer"/> returns it.</param>
    /// <param name="password">As configured; it has passed <see cref="IPasswordPolicy"/>.</param>
    /// <param name="cancellationToken">Cancels the call before it starts.</param>
    Task<EnsuredAccount> EnsureAsync(string normalizedEmail, string password, CancellationToken cancellationToken);
}
