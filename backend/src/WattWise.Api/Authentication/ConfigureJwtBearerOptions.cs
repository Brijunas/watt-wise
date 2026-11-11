using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Api.Authentication;

/// <summary>
/// Configures JWT bearer validation from <see cref="JwtSettings"/>: HS256 only, issuer, audience, lifetime and signature
/// checked, no clock skew, and the claims kept under their token names (<c>sub</c>, <c>sid</c>, <c>permission</c>).
/// Reading through options means configuration sources added later, e.g. by tests, are seen.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(IOptions<JwtSettings> settings, TimeProvider timeProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        JwtSettings jwt = settings.Value;
        options.MapInboundClaims = false;
        // A 401 says nothing about why the token failed.
        options.IncludeErrorDetails = false;
        options.TimeProvider = timeProvider;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKeyBytes()),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            // TokenValidationParameters.TimeProvider is internal, so the lifetime check is ours, on the DI TimeProvider.
            LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires),
        };
    }

    // Valid from not-before up to, but not including, the expiry. A token with no expiry is never valid.
    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        return expires is { } expiry && now < expiry && (notBefore is not { } start || start <= now);
    }
}
