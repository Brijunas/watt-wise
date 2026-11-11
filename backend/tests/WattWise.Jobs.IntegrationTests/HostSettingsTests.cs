using Microsoft.Extensions.Options;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class HostSettingsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_host_with_blank_database_settings_stops_at_start()
    {
        Dictionary<string, string?> settings = new(JobsTestSettings.For(fixture))
        {
            ["Database:Host"] = "",
            ["Database:Port"] = "",
            ["Database:Name"] = "",
            ["Database:Username"] = "",
            ["Database:Password"] = "",
        };
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message.Contains("Database configuration is missing or invalid", StringComparison.Ordinal));
    }
}
