using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>How long the tokens live. Fixed by security.md, so constants and not settings.</summary>
public static class TokenLifetimes
{
    public static readonly Duration AccessToken = Duration.FromMinutes(15);

    /// <summary>Sliding: every refresh token is valid this long from when it is issued.</summary>
    public static readonly Duration RefreshToken = Duration.FromDays(7);
}
