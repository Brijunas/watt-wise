using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Fails options resolution (and host start, through ValidateOnStart) naming the invalid keys, never values.</summary>
internal sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        List<string> invalid = [];
        if (options.SigningKey is null || options.SigningKey.Length < JwtSettings.MinimumSigningKeyLength)
        {
            invalid.Add($"{JwtSettings.SectionName}:{nameof(JwtSettings.SigningKey)} (at least {JwtSettings.MinimumSigningKeyLength} characters)");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            invalid.Add($"{JwtSettings.SectionName}:{nameof(JwtSettings.Issuer)}");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            invalid.Add($"{JwtSettings.SectionName}:{nameof(JwtSettings.Audience)}");
        }

        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Jwt configuration is missing or invalid: {string.Join(", ", invalid)}.");
    }
}
