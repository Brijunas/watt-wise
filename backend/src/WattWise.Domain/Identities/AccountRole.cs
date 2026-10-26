namespace WattWise.Domain.Identities;

/// <summary>The link that gives an account one role.</summary>
public sealed class AccountRole
{
    // Used by EF Core.
    private AccountRole()
    {
    }

    private AccountRole(Guid accountId, Guid roleId)
    {
        AccountId = accountId;
        RoleId = roleId;
    }

    public Guid AccountId { get; private set; }

    public Guid RoleId { get; private set; }

    internal static AccountRole Create(Guid accountId, Guid roleId)
    {
        return new AccountRole(accountId, roleId);
    }
}
