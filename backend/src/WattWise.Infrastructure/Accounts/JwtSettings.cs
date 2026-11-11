using System.Text;

using Microsoft.Extensions.Configuration;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Access token settings, read from the "Jwt" section. The signing key is a secret from 1Password; the other two are names.</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>The shortest signing key accepted, in characters.</summary>
    public const int MinimumSigningKeyLength = 64;

    /// <summary>The secret the HS256 signature is made with; its UTF-8 bytes are the key.</summary>
    public string? SigningKey { get; set; }

    /// <summary>The <c>iss</c> claim.</summary>
    public string? Issuer { get; set; }

    /// <summary>The <c>aud</c> claim.</summary>
    public string? Audience { get; set; }

    /// <summary>Fills this instance from the section.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        SigningKey = section[nameof(SigningKey)];
        Issuer = section[nameof(Issuer)];
        Audience = section[nameof(Audience)];
    }

    /// <summary>The key bytes. Throws <see cref="InvalidOperationException"/> when the key is blank.</summary>
    public byte[] SigningKeyBytes()
    {
        return Encoding.UTF8.GetBytes(SigningKey ?? throw new InvalidOperationException("Jwt:SigningKey is not set."));
    }
}
