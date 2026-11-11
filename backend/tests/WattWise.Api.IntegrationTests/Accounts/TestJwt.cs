using System.Text;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Mints access tokens the way the Api's settings say, with one claim or key changed, to see what the Api refuses.</summary>
internal static class TestJwt
{
    public const string Issuer = "wattwise-api";
    public const string Audience = "wattwise-clients";

    /// <summary>The fixture clock starts at 2026-11-11T12:00Z: a token valid from 10:00 to 13:00 is valid there, whatever the machine's own clock says.</summary>
    public static readonly DateTime ValidUntil = new(2026, 11, 11, 13, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ValidFrom = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    public static string Mint(
        string subject,
        string key = ApiTestSettings.JwtSigningKey,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        bool signed = true,
        bool withExpiry = true)
    {
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = subject, ["sid"] = Guid.NewGuid().ToString() },
            SigningCredentials = signed
                ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
                : null,
        };
        if (withExpiry)
        {
            descriptor.Expires = expires ?? ValidUntil;
            descriptor.IssuedAt = ValidFrom;
            descriptor.NotBefore = ValidFrom;
        }

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);
}
