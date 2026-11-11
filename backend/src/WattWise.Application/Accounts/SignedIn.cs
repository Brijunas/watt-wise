using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>The tokens a sign-in or a refresh hands out.</summary>
public sealed record SignedIn(string AccessToken, Instant AccessTokenExpiresAt, string RefreshToken, Instant RefreshTokenExpiresAt)
{
    // A record would print the tokens.
    public override string ToString() => nameof(SignedIn);
}
