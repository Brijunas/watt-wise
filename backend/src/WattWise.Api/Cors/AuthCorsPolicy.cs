namespace WattWise.Api.Cors;

/// <summary>Name of the CORS policy for the <c>/api/v1/auth</c> endpoints: the same origins, and credentials for the refresh cookie.</summary>
public static class AuthCorsPolicy
{
    public const string Name = "WattWiseClientsWithCredentials";
}
