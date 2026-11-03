using System.Text.Json;

namespace WattWise.Api.IntegrationTests;

public class HealthEndpointTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_with_the_right_credentials_is_200_healthy_with_the_database_check()
    {
        await using TestHostFactory<Program> factory = new(ApiTestSettings.For(fixture.Database));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal("200 Healthy database:Healthy", await Summary(response));
    }

    [Fact]
    public async Task Health_with_a_wrong_database_password_is_503_unhealthy_json()
    {
        Dictionary<string, string?> settings = new(ApiTestSettings.For(fixture.Database))
        {
            ["Database:Password"] = "wrong",
        };
        await using TestHostFactory<Program> factory = new(settings);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal("503 Unhealthy database:Unhealthy application/json", $"{await Summary(response)} {response.Content.Headers.ContentType?.MediaType}");
    }

    private static async Task<string> Summary(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        return $"{(int)response.StatusCode} {body.RootElement.GetProperty("status").GetString()} "
            + $"{check.GetProperty("name").GetString()}:{check.GetProperty("status").GetString()}";
    }
}
