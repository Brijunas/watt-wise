using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Observability;

namespace WattWise.Infrastructure.Tests.Observability;

public class ObservabilitySettingsValidatorTests
{
    private readonly ObservabilitySettingsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("http://127.0.0.1:4317")]
    [InlineData("https://collector")]
    [InlineData("http://host:4317/")] // characterization: ObservabilitySettings.cs:24 (a path of "/" is accepted)
    public void A_blank_or_valid_origin_passes(string? endpoint)
    {
        ValidateOptionsResult result = _validator.Validate(null, new ObservabilitySettings { OtlpEndpoint = endpoint });

        Assert.Same(ValidateOptionsResult.Success, result);
    }

    // characterization: ObservabilitySettingsValidator.cs:20 (message text)
    [Theory]
    [InlineData("host:4317")]
    [InlineData("ftp://h:4317")]
    [InlineData("http://h/path")]
    [InlineData("http://h?x=1")]
    [InlineData("http://h#f")]
    [InlineData("http://u:p@h")]
    [InlineData("not a uri")]
    public void An_invalid_endpoint_fails_showing_the_value(string endpoint)
    {
        ValidateOptionsResult result = _validator.Validate(null, new ObservabilitySettings { OtlpEndpoint = endpoint });

        Assert.Equal(
            $"Observability configuration is invalid: Observability:OtlpEndpoint ('{endpoint}') must be an absolute http or https origin without path, query, fragment or user info.",
            result.FailureMessage);
    }
}
