using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NodaTime;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Signs access tokens with HS256. Claims: <c>sub</c> (the account), <c>sid</c> (the session), <c>jti</c>, <c>iat</c>,
/// <c>nbf</c>, <c>exp</c>, and one <c>permission</c> per permission the account's roles grant.
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtSettings> options) : IAccessTokenIssuer
{
    public const string SessionIdClaim = "sid";
    public const string PermissionClaim = "permission";

    // Every time in the token is set explicitly from our clock; the handler must not fill any from the machine's.
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly SigningCredentials _credentials = new(new SymmetricSecurityKey(options.Value.SigningKeyBytes()), SecurityAlgorithms.HmacSha256);

    public IssuedAccessToken Issue(Guid accountId, Guid sessionId, IReadOnlyCollection<string> permissions, Instant now)
    {
        // exp is whole seconds; truncate the same way so the reported expiry is the token's.
        Instant issuedAt = Instant.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        Instant expiresAt = issuedAt + TokenLifetimes.AccessToken;

        Dictionary<string, object> claims = new()
        {
            [JwtRegisteredClaimNames.Sub] = accountId.ToString(),
            [SessionIdClaim] = sessionId.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };
        if (permissions.Count > 0)
        {
            claims[PermissionClaim] = permissions.ToArray();
        }

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            IssuedAt = issuedAt.ToDateTimeUtc(),
            NotBefore = issuedAt.ToDateTimeUtc(),
            Expires = expiresAt.ToDateTimeUtc(),
            Claims = claims,
            SigningCredentials = _credentials,
        };

        return new IssuedAccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
