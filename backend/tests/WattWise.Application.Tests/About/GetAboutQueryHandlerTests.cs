using WattWise.Application.About;

namespace WattWise.Application.Tests.About;

public class GetAboutQueryHandlerTests
{
    [Fact]
    public async Task The_about_response_names_the_api()
    {
        GetAboutQueryHandler handler = new();

        var result = await handler.Handle(new GetAboutQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("Watt-Wise API", result.Value.Name);
    }

    // characterization: GetAboutQueryHandler.cs:16-17; no <Version> is set, so the SDK default 1.0.0+<sha>
    [Fact]
    public async Task The_about_response_carries_the_sdk_informational_version()
    {
        GetAboutQueryHandler handler = new();

        var result = await handler.Handle(new GetAboutQuery(), TestContext.Current.CancellationToken);

        Assert.StartsWith("1.0.0", result.Value.Version, StringComparison.Ordinal);
    }
}
