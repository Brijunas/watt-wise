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

    private static ValidateOptionsResult Validate(params string[] origins)
    {
        return new CorsSettingsValidator().Validate(null, new CorsSettings { AllowedOrigins = origins });
    }
}
