using WattWise.Application.About;

namespace WattWise.Application.Tests.About;

public class GetAboutQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_the_api_name_and_a_version()
    {
        GetAboutQueryHandler handler = new();

        var result = await handler.Handle(new GetAboutQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Watt-Wise API", result.Value.Name);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Version));
    }
}
