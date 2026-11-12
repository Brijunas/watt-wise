namespace WattWise.Application.Accounts;

/// <summary>Ends sessions with one statement each: all of an account's (a password change or reset, sign out everywhere) or one of them.</summary>
public interface IAccountSessions
{
    /// <summary>Ends every session of the account, refresh tokens included, with one statement. Returns the number of sessions ended.</summary>
    Task<int> EndAllAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Ends one session, refresh tokens included, only when it belongs to the account. Returns the number of sessions ended: 0 when the id is unknown or another account's.</summary>
    Task<int> EndAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken);
}
