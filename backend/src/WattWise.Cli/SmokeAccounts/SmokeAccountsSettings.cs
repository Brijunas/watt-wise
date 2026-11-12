using Microsoft.Extensions.Configuration;

namespace WattWise.Cli.SmokeAccounts;

/// <summary>The two smoke-test accounts' credentials, read from the "SmokeAccounts" section. Development secrets from 1Password; only <c>seed smoke-accounts</c> needs them.</summary>
internal sealed class SmokeAccountsSettings
{
    public const string SectionName = "SmokeAccounts";

    public string? UserEmail { get; set; }

    public string? UserPassword { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    /// <summary>Fills this instance from the section.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        UserEmail = section[nameof(UserEmail)];
        UserPassword = section[nameof(UserPassword)];
        AdminEmail = section[nameof(AdminEmail)];
        AdminPassword = section[nameof(AdminPassword)];
    }
}
