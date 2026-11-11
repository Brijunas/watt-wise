using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class UserAgentDeviceLabelsTests
{
    private readonly UserAgentDeviceLabels _labels = new();

    [Theory]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0", "Firefox on Linux")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36",
        "Chrome on Windows 10")]
    public void A_browser_user_agent_gives_the_browser_on_the_platform(string userAgent, string expected)
    {
        Assert.Equal(expected, _labels.FromUserAgent(userAgent));
    }

    [Fact]
    public void A_recognised_agent_without_a_platform_is_labelled_by_its_name_alone()
    {
        Assert.Equal("Googlebot", _labels.FromUserAgent("Googlebot/2.1 (+http://www.google.com/bot.html)"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a user agent")]
    public void A_missing_or_unrecognised_user_agent_gives_Unknown_device(string? userAgent)
    {
        Assert.Equal("Unknown device", _labels.FromUserAgent(userAgent));
    }
}
