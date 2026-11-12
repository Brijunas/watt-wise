using Microsoft.Extensions.Options;

using WattWise.Cli.SmokeAccounts;

namespace WattWise.Cli.Tests;

public class SmokeAccountsSettingsValidatorTests
{
    private static SmokeAccountsSettings Valid() => new()
    {
        UserEmail = "user@example.com",
        UserPassword = "Aa1!user-password",
        AdminEmail = "admin@example.com",
        AdminPassword = "Aa1!admin-password",
    };

    private static string? FailureOf(SmokeAccountsSettings settings)
    {
        ValidateOptionsResult result = new SmokeAccountsSettingsValidator().Validate(null, settings);
        return result.Failed ? result.FailureMessage : null;
    }

    [Fact]
    public void Complete_settings_with_different_emails_are_valid()
    {
        Assert.Null(FailureOf(Valid()));
    }

    [Theory]
    [InlineData("UserEmail")]
    [InlineData("UserPassword")]
    [InlineData("AdminEmail")]
    [InlineData("AdminPassword")]
    public void A_missing_empty_or_whitespace_value_is_named_by_its_key(string key)
    {
        foreach (string? blank in new string?[] { null, "", "   " })
        {
            SmokeAccountsSettings settings = Valid();
            typeof(SmokeAccountsSettings).GetProperty(key)!.SetValue(settings, blank);

            Assert.Equal($"SmokeAccounts configuration is missing or invalid: SmokeAccounts:{key}.", FailureOf(settings));
        }
    }

    [Fact]
    public void Several_missing_values_are_all_named_in_one_message_in_key_order()
    {
        SmokeAccountsSettings settings = Valid();
        settings.UserEmail = " ";
        settings.AdminPassword = null;

        Assert.Equal(
            "SmokeAccounts configuration is missing or invalid: SmokeAccounts:UserEmail, SmokeAccounts:AdminPassword.",
            FailureOf(settings));
    }

    [Fact]
    public void An_absent_section_names_all_four_keys()
    {
        Assert.Equal(
            "SmokeAccounts configuration is missing or invalid: SmokeAccounts:UserEmail, SmokeAccounts:UserPassword, "
            + "SmokeAccounts:AdminEmail, SmokeAccounts:AdminPassword.",
            FailureOf(new SmokeAccountsSettings()));
    }

    [Theory]
    [InlineData("a@example.com", "a@example.com")]
    [InlineData("a@example.com", "A@EXAMPLE.COM")]
    [InlineData("a@example.com ", "a@example.com")]
    [InlineData(" a@example.com", "a@example.com ")]
    public void The_same_email_for_the_user_and_the_admin_is_rejected_ignoring_case_and_surrounding_spaces(string userEmail, string adminEmail)
    {
        SmokeAccountsSettings settings = Valid();
        settings.UserEmail = userEmail;
        settings.AdminEmail = adminEmail;

        Assert.Equal(
            "SmokeAccounts configuration is missing or invalid: SmokeAccounts:UserEmail and AdminEmail (must differ).",
            FailureOf(settings));
    }

    [Fact]
    public void A_missing_value_hides_the_must_differ_check()
    {
        SmokeAccountsSettings settings = Valid();
        settings.AdminEmail = settings.UserEmail;
        settings.UserPassword = "";

        Assert.Equal("SmokeAccounts configuration is missing or invalid: SmokeAccounts:UserPassword.", FailureOf(settings));
    }
}
