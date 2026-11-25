using Microsoft.Extensions.Options;

namespace WattWise.Api.IntegrationTests;

public class HostSettingsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_host_with_blank_database_settings_stops_at_start()
    {
        Dictionary<string, string?> settings = new(ApiTestSettings.For(fixture.Database))
        {
            ["Database:Host"] = "",
            ["Database:Port"] = "",
            ["Database:Name"] = "",
            ["Database:Username"] = "",
            ["Database:Password"] = "",
        };
        StartupFailureCapture capture = new();
        await using TestHostFactory<Program> factory = new(settings, capture.Decorate);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        OptionsValidationException failure = capture.AssertValidationFailure(exception);
        Assert.Contains("Database configuration is missing or invalid", failure.Message, StringComparison.Ordinal);
    }
}
