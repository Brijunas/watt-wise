namespace WattWise.Domain.Accounts;

/// <summary>The names of the permissions the migrations seed, and the fixed ids they are seeded with. Endpoints check these, never role names.</summary>
public static class PermissionNames
{
    public const string AdminAccess = "admin.access";

    /// <summary>The seeded id of the <see cref="AdminAccess"/> permission.</summary>
    public static readonly Guid AdminAccessId = new("0192e5a0-0000-7000-8000-000000000002");
}
