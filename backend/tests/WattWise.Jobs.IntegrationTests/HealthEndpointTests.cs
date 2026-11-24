using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class HealthEndpointTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // The Jobs host connects to PostgreSQL at start (Hangfire), so a wrong password stops it before /health can answer.
    // To reach an unhealthy report the app's context alone is pointed at a server nothing listens on.
    private const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=wattwise;Username=jobs;Password=wrong;Timeout=2";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_with_the_right_credentials_is_200_healthy_with_the_database_check()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal("200 Healthy database:Healthy application/json", $"{await Summary(response)} {response.Content.Headers.ContentType?.MediaType}");
    }

    [Fact]
    public async Task Health_with_an_unreachable_database_is_503_unhealthy_json()
    {
        await using TestHostFactory<Program> factory = UnhealthyFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        Assert.Equal("503 Unhealthy database:Unhealthy application/json", $"{await Summary(response)} {response.Content.Headers.ContentType?.MediaType}");
    }

    [Fact]
    public async Task An_unhealthy_report_carries_no_exception_text_because_health_is_public()
    {
        await using TestHostFactory<Program> factory = new(
            JobsTestSettings.For(fixture),
            services => services.AddHealthChecks().AddCheck("leaky", () => throw new InvalidOperationException("secret-marker-text")));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", Token);

        string body = await response.Content.ReadAsStringAsync(Token);
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement leaky = json.RootElement.GetProperty("checks").EnumerateArray().Single(check => check.GetProperty("name").GetString() == "leaky");
        Assert.Equal("False Null", $"{body.Contains("secret-marker-text", StringComparison.Ordinal)} {leaky.GetProperty("description").ValueKind}");
    }

    private TestHostFactory<Program> UnhealthyFactory()
    {
        return new TestHostFactory<Program>(
            JobsTestSettings.For(fixture),
            services => services.AddDbContext<AppDbContext>(options => AppDbContextOptions.Configure(options, UnreachableConnectionString)));
    }

    private static async Task<string> Summary(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        return $"{(int)response.StatusCode} {body.RootElement.GetProperty("status").GetString()} "
            + $"{check.GetProperty("name").GetString()}:{check.GetProperty("status").GetString()}";
    }
}
