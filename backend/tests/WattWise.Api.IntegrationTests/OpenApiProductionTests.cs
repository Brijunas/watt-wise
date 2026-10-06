using System.Net;

namespace WattWise.Api.IntegrationTests;

public class OpenApiProductionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task Documentation_is_not_served_in_production(string path)
    {
        await using ApiFactory factory = new(
            fixture.Database.ConfigurationFor(DatabaseRole.Api), environment: "Production");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
