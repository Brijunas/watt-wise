namespace WattWise.Api.Endpoints;

/// <summary>The one place the API version lives. Every endpoint module is mapped under <see cref="V1Prefix"/>.</summary>
public static class ApiRoutes
{
    public const string V1Prefix = "/api/v1";

    /// <summary>The route group, under <see cref="V1Prefix"/>, that requires the <c>admin.access</c> permission.</summary>
    public const string AdminPrefix = "/admin";
}
