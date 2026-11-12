using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Takes the <c>Admin</c> role from the account with the email and ends all its sessions, so the permission is gone within one access-token lifetime.</summary>
public sealed record RevokeAdminRoleCommand(string? Email) : ICommand<Result<RoleChange>>
{
    // A record would print the address.
    public override string ToString() => nameof(RevokeAdminRoleCommand);
}
