using Microsoft.Extensions.Options;

using WattWise.Api.Cors;

namespace WattWise.Api.Tests.Cors;

public class CorsSettingsValidatorTests
{
    [Theory]
    [InlineData("a.example")] // no scheme: Uri.TryCreate fails
    [InlineData("ftp://a.example")] // scheme only
    [InlineData("https://a.example/b")] // path only
    [InlineData("https://a.example/")] // trailing slash only (the path "/" is accepted)
    [InlineData("https://a.example?x=1")] // query only
    [InlineData("https://a.example#f")] // characterization: CorsSettingsValidator.cs:44
    [InlineData("https://user@a.example")] // characterization: CorsSettingsValidator.cs:45
    public void A_rejected_origin_form_fails_validation(string origin)
    {
        ValidateOptionsResult result = Validate(origin);

        Assert.True(result.Failed);
    }

    [Fact]
    public void A_null_origin_is_rejected()
    {
        ValidateOptionsResult result = Validate([null!]);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.example.com")]
    public void A_valid_origin_passes(string origin)
    {
        ValidateOptionsResult result = Validate(origin);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_empty_origin_list_passes()
    {
        ValidateOptionsResult result = Validate();

        Assert.True(result.Succeeded);
    }

    // characterization: CorsSettingsValidator.cs:19,26 (the message text)
    [Fact]
    public void The_failure_names_each_invalid_origin_by_its_index()
    {
        ValidateOptionsResult result = Validate("https://ok.example", "https://a.example/b", "ftp://c.example");

        Assert.Equal(
            "Cors configuration has invalid origins (each must be an absolute http or https origin without path, query or fragment): Cors:AllowedOrigins:1 ('https://a.example/b'), Cors:AllowedOrigins:2 ('ftp://c.example').",
            result.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_admin_origin_passes(string? adminOrigin)
    {
        ValidateOptionsResult result = ValidateAdmin(adminOrigin, "https://app.example");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_admin_origin_in_the_allowed_list_passes()
    {
        ValidateOptionsResult result = ValidateAdmin("https://admin.example", "https://app.example", "https://admin.example");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_malformed_admin_origin_fails_naming_the_setting_and_value()
    {
        ValidateOptionsResult result = ValidateAdmin("https://admin.example/path", "https://app.example");

        Assert.Equal(
            "Cors:AdminOrigin ('https://admin.example/path') must be an absolute http or https origin without path, query or fragment.",
            result.FailureMessage);
    }

    [Fact]
    public void An_admin_origin_missing_from_the_allowed_list_fails_naming_the_setting_and_value()
    {
        ValidateOptionsResult result = ValidateAdmin("https://admin.example", "https://app.example");

        Assert.Equal(
            "Cors:AdminOrigin ('https://admin.example') must be one of Cors:AllowedOrigins.",
            result.FailureMessage);
    }

    [Fact]
    public void The_admin_origin_is_matched_to_the_allowed_list_case_sensitively()
    {
        ValidateOptionsResult result = ValidateAdmin("https://admin.example", "https://ADMIN.example");

        Assert.True(result.Failed);
    }

    private static ValidateOptionsResult ValidateAdmin(string? adminOrigin, params string[] allowed)
    {
        return new CorsSettingsValidator().Validate(null, new CorsSettings { AllowedOrigins = allowed, AdminOrigin = adminOrigin });
    }

    private static ValidateOptionsResult Validate(params string[] origins)
    {
        return new CorsSettingsValidator().Validate(null, new CorsSettings { AllowedOrigins = origins });
    }
}
