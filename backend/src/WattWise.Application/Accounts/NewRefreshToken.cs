namespace WattWise.Application.Accounts;

/// <summary>A new refresh token and the hash that is stored for it.</summary>
public sealed record NewRefreshToken(string Token, byte[] Hash)
{
    // A record would print the token.
    public override string ToString() => nameof(NewRefreshToken);
}
