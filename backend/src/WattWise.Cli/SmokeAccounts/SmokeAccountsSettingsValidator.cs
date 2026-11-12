using Microsoft.Extensions.Options;

namespace WattWise.Cli.SmokeAccounts;

/// <summary>Fails naming the missing keys, never values. Whether the email and password are acceptable is the command validator's job.</summary>
internal sealed class SmokeAccountsSettingsValidator : IValidateOptions<SmokeAccountsSettings>
{
    public ValidateOptionsResult Validate(string? name, SmokeAccountsSettings options)
    {
        List<string> invalid = [];
        Check(invalid, options.UserEmail, nameof(SmokeAccountsSettings.UserEmail));
        Check(invalid, options.UserPassword, nameof(SmokeAccountsSettings.UserPassword));
        Check(invalid, options.AdminEmail, nameof(SmokeAccountsSettings.AdminEmail));
        Check(invalid, options.AdminPassword, nameof(SmokeAccountsSettings.AdminPassword));

        if (invalid.Count == 0
            && string.Equals(options.UserEmail!.Trim(), options.AdminEmail!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            invalid.Add($"{SmokeAccountsSettings.SectionName}:{nameof(SmokeAccountsSettings.UserEmail)} and {nameof(SmokeAccountsSettings.AdminEmail)} (must differ)");
        }

        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"SmokeAccounts configuration is missing or invalid: {string.Join(", ", invalid)}.");
    }

    private static void Check(List<string> invalid, string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            invalid.Add($"{SmokeAccountsSettings.SectionName}:{key}");
        }
    }
}
