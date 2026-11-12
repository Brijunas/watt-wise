using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Gives the confirmed account with the email the <c>Admin</c> role.</summary>
public sealed record GrantAdminRoleCommand(string? Email) : ICommand<Result<RoleChange>>
{
    // A record would print the address.
    public override string ToString() => nameof(GrantAdminRoleCommand);
}
