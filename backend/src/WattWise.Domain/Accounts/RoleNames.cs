namespace WattWise.Domain.Accounts;

/// <summary>The names of the roles the migrations seed, and the fixed ids they are seeded with.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";

    /// <summary>The seeded id of the <see cref="Admin"/> role.</summary>
    public static readonly Guid AdminId = new("0192e5a0-0000-7000-8000-000000000001");
}
