using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>An access token and when it expires.</summary>
public sealed record IssuedAccessToken(string Token, Instant ExpiresAt)
{
    // A record would print the token.
    public override string ToString() => nameof(IssuedAccessToken);
}
