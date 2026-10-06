using System.Net;
using System.Text.Json;

namespace WattWise.Api.IntegrationTests;

public class HealthEndpointTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_is_healthy_with_valid_settings()
    {
        await using ApiFactory factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Api));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        JsonElement check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
        Assert.Equal("Healthy", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_is_unhealthy_with_a_wrong_password()
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Database:Password"] = "wrong",
        };
        await using ApiFactory factory = new(settings);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        string text = await response.Content.ReadAsStringAsync(Token);
        using JsonDocument body = JsonDocument.Parse(text);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        JsonElement check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
        Assert.DoesNotContain("28P01", text, StringComparison.Ordinal);
        Assert.DoesNotContain("authentication", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }
}
