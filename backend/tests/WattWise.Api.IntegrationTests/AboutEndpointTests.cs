using System.Text.Json;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.About;
using WattWise.Application.Behaviors;
using WattWise.Application.Results;

namespace WattWise.Api.IntegrationTests;

public class AboutEndpointTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_about_endpoint_answers_200_with_name_and_version_in_camel_case()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/about", Token);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            "200 name,version",
            $"{(int)response.StatusCode} {string.Join(',', body.RootElement.EnumerateObject().Select(property => property.Name).Order())}");
    }

    [Fact]
    public void The_host_runs_the_about_use_case_through_logging_then_validation()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();

        IPipelineBehavior<GetAboutQuery, Result<AboutResponse>>[] behaviors = [.. scope.ServiceProvider
            .GetServices<IPipelineBehavior<GetAboutQuery, Result<AboutResponse>>>()];

        Assert.Equal(
            [typeof(LoggingBehavior<GetAboutQuery, Result<AboutResponse>>), typeof(ValidationBehavior<GetAboutQuery, Result<AboutResponse>>)],
            behaviors.Select(behavior => behavior.GetType()));
    }
}
