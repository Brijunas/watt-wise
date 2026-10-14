using System.Net;
using System.Text.Json;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.About;
using WattWise.Application.Behaviors;
using WattWise.Application.Results;

namespace WattWise.Api.IntegrationTests;

public class AboutEndpointTests(ErrorsApiFixture fixture) : IClassFixture<ErrorsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task About_returns_the_api_name_and_a_version()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/about", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("Watt-Wise API", body.RootElement.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("version").GetString()));
    }

    [Fact]
    public void The_host_runs_a_use_case_through_logging_then_validation()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();

        IPipelineBehavior<GetAboutQuery, Result<AboutResponse>>[] behaviors = [.. scope.ServiceProvider
            .GetServices<IPipelineBehavior<GetAboutQuery, Result<AboutResponse>>>()];

        Assert.Equal(
            [typeof(LoggingBehavior<GetAboutQuery, Result<AboutResponse>>), typeof(ValidationBehavior<GetAboutQuery, Result<AboutResponse>>)],
            behaviors.Select(behavior => behavior.GetType()));
    }
}
