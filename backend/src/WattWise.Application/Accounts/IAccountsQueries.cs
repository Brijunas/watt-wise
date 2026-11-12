using NodaTime;

namespace WattWise.Application.Accounts;

public interface IAccountsQueries
{
    /// <summary>The names of the permissions the account's roles grant, sorted and without duplicates.</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>The security stamp of the account's sign-in record; <c>null</c> when there is none.</summary>
    Task<string?> GetSecurityStampAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>The account's details, or <c>null</c> when there is no such account.</summary>
    Task<Me?> GetMeAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>The account's sessions that still have a refresh token valid at <paramref name="now"/>, most recently used first, then by id.</summary>
    Task<IReadOnlyList<SessionListItem>> GetSessionsAsync(Guid accountId, Instant now, CancellationToken cancellationToken);
}
