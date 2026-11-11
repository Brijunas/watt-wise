using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The query port: answers the permissions and account it was given.</summary>
internal sealed class FakeAccountsQueries : IAccountsQueries
{
    public string[] Permissions { get; set; } = [];

    public Me? Me { get; set; }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Permissions);

    public Task<Me?> GetMeAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(Me);
}
