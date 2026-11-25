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
        StartupFailureCapture capture = new();
        await using TestHostFactory<Program> factory = new(settings, capture.Decorate);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        OptionsValidationException failure = capture.AssertValidationFailure(exception);
        Assert.Contains("Cors:AllowedOrigins", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_admin_origin_that_is_not_allowed_stops_the_host_naming_the_setting()
    {
        Dictionary<string, string?> settings = new(ApiTestSettings.For(fixture.Database))
        {
            ["Cors:AllowedOrigins:0"] = "https://app.example.test",
            ["Cors:AdminOrigin"] = "https://admin.example.test",
        };
        StartupFailureCapture capture = new();
        await using TestHostFactory<Program> factory = new(settings, capture.Decorate);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        OptionsValidationException failure = capture.AssertValidationFailure(exception);
        Assert.Contains("Cors:AdminOrigin", failure.Message, StringComparison.Ordinal);
    }
}
