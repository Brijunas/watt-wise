namespace WattWise.Application.Accounts;

/// <summary>Gives accounts roles and takes them away, one statement each.</summary>
public interface IAccountRoles
{
    /// <summary>The account with the normalized email, or null when there is none.</summary>
    Task<RoleHolder?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Gives the account the role. Returns false when it already had it.</summary>
    Task<bool> AddAsync(Guid accountId, string roleName, CancellationToken cancellationToken);

    /// <summary>Takes the role from the account. Returns false when it did not have it.</summary>
    Task<bool> RemoveAsync(Guid accountId, string roleName, CancellationToken cancellationToken);
}
