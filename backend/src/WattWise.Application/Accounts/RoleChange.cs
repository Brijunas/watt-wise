namespace WattWise.Application.Accounts;

/// <summary>What a grant or revoke did.</summary>
public enum RoleChange
{
    /// <summary>The account's roles changed.</summary>
    Changed,

    /// <summary>The account already was in the asked state.</summary>
    Unchanged,
}
