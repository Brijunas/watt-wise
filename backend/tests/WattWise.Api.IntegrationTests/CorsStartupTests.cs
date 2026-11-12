using Microsoft.Extensions.Options;

namespace WattWise.Api.IntegrationTests;

public class CorsStartupTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_malformed_origin_stops_the_host_naming_the_setting()
    {
        Dictionary<string, string?> settings = new(ApiTestSettings.For(fixture.Database))
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.test/path",
        };
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message.Contains("Cors:AllowedOrigins", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_admin_origin_that_is_not_allowed_stops_the_host_naming_the_setting()
    {
        Dictionary<string, string?> settings = new(ApiTestSettings.For(fixture.Database))
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.test",
            ["Cors:AdminOrigin"] = "https://admin.example.test",
        };
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message.Contains("Cors:AdminOrigin", StringComparison.Ordinal));
    }
}
