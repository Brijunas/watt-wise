using NodaTime;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The query port: answers the permissions and account it was given.</summary>
internal sealed class FakeAccountsQueries : IAccountsQueries
{
    public string[] Permissions { get; set; } = [];

    public Me? Me { get; set; }

    /// <summary>What <see cref="GetSessionsAsync"/> answers.</summary>
    public List<SessionListItem> Sessions { get; } = [];

    /// <summary>The account and time the last <see cref="GetSessionsAsync"/> call was made with.</summary>
    public (Guid AccountId, Instant Now)? SessionsRequest { get; private set; }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Permissions);

    /// <summary>The account's current security stamp; sessions started by the fake password check carry <c>"stamp"</c>.</summary>
    public string? SecurityStamp { get; set; } = "stamp";

    public Task<string?> GetSecurityStampAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(SecurityStamp);

    public Task<IReadOnlyList<SessionListItem>> GetSessionsAsync(Guid accountId, Instant now, CancellationToken cancellationToken)
    {
        SessionsRequest = (accountId, now);
        return Task.FromResult<IReadOnlyList<SessionListItem>>(Sessions);
    }

    public Task<Me?> GetMeAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(Me);
}
