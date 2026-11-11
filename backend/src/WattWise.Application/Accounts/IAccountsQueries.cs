namespace WattWise.Application.Accounts;

public interface IAccountsQueries
{
    /// <summary>The names of the permissions the account's roles grant, sorted and without duplicates.</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>The account's details, or <c>null</c> when there is no such account.</summary>
    Task<Me?> GetMeAsync(Guid accountId, CancellationToken cancellationToken);
}
