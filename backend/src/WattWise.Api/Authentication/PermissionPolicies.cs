using Microsoft.AspNetCore.Authorization;

using WattWise.Domain.Accounts;

namespace WattWise.Api.Authentication;

/// <summary>One authorization policy per permission: the access token's <c>permission</c> claim must hold the permission's name.</summary>
internal static class PermissionPolicies
{
    /// <summary>The claim the access token carries each permission in.</summary>
    public const string ClaimType = "permission";

    /// <summary>Every permission an endpoint may require.</summary>
    private static readonly string[] Permissions = [PermissionNames.AdminAccess];

    public static string PolicyName(string permission)
    {
        return "permission:" + permission;
    }

    public static void AddTo(AuthorizationOptions options)
    {
        foreach (string permission in Permissions)
        {
            options.AddPolicy(PolicyName(permission), policy => policy.RequireClaim(ClaimType, permission));
        }
    }
}
