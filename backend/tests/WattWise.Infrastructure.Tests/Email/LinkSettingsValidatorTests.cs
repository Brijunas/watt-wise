using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class LinkSettingsValidatorTests
{
    private readonly LinkSettingsValidator _validator = new();

    [Theory]
    [InlineData("https://app.wattwise.lt")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://staging.wattwise.lt:8443")]
    public void An_absolute_http_or_https_origin_without_a_path_passes(string url)
    {
        ValidateOptionsResult result = _validator.Validate(null, new LinkSettings { FrontendBaseUrl = url });

        Assert.Same(ValidateOptionsResult.Success, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/app")]
    [InlineData("app.wattwise.lt")]
    [InlineData("ftp://app.wattwise.lt")]
    [InlineData("https://app.wattwise.lt/")]
    [InlineData("https://app.wattwise.lt/app")]
    [InlineData("https://app.wattwise.lt/?x=1")]
    [InlineData("https://app.wattwise.lt#top")]
    public void Anything_else_fails_naming_the_key(string? url)
    {
        ValidateOptionsResult result = _validator.Validate(null, new LinkSettings { FrontendBaseUrl = url });

        Assert.Equal((true, true), (result.Failed, result.FailureMessage!.Contains("Links:FrontendBaseUrl", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_message_holds_the_key_name_and_not_the_value()
    {
        ValidateOptionsResult result = _validator.Validate(null, new LinkSettings { FrontendBaseUrl = "https://secret-host.example/path" });

        Assert.DoesNotContain("secret-host", result.FailureMessage, StringComparison.Ordinal);
    }
}
