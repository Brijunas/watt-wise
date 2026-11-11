namespace WattWise.Application.Accounts;

/// <summary>Ends sessions in bulk, for the events that must leave an account none (a password change or reset).</summary>
public interface IAccountSessions
{
    /// <summary>Ends every session of the account, refresh tokens included, with one statement. Returns the number of sessions ended.</summary>
    Task<int> EndAllAsync(Guid accountId, CancellationToken cancellationToken);
}
