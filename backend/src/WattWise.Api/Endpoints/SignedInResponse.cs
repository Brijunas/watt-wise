namespace WattWise.Api.Endpoints;

/// <summary>What sign-in and refresh answer with. The refresh token travels in a cookie, never in the body.</summary>
/// <param name="AccessToken">The JWT to send as a Bearer header.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires, as an ISO 8601 instant.</param>
public sealed record SignedInResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt)
{
    // A record would print the token.
    public override string ToString() => nameof(SignedInResponse);
}
