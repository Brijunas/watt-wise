namespace WattWise.Domain.Accounts;

/// <summary>The link that makes a role grant one permission.</summary>
public sealed class RolePermission
{
    // Used by EF Core.
    private RolePermission()
    {
    }

    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    internal static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission(roleId, permissionId);
    }
}
