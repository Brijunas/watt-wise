namespace WattWise.Api.Authentication;

public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires the access token to carry the permission, on top of the signed-in caller the parent group already
    /// requires. Endpoints check permissions, never role names. Only permissions registered in
    /// <see cref="AuthenticationServiceCollectionExtensions.AddApiAuthentication"/> work.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(PermissionPolicies.PolicyName(permission));
    }
}
