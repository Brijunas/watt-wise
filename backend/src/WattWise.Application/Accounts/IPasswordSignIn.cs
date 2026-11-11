namespace WattWise.Application.Accounts;

/// <summary>Checks an email and password, counting failures toward the lockout.</summary>
public interface IPasswordSignIn
{
    /// <param name="normalizedEmail">The address in the form accounts are stored in.</param>
    /// <param name="password">As typed.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<PasswordCheck> CheckAsync(string normalizedEmail, string password, CancellationToken cancellationToken);
}
