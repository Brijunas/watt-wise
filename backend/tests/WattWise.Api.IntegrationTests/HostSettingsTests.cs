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
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message.Contains("Database configuration is missing or invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_host_with_blank_smtp_settings_stops_at_start()
    {
        Dictionary<string, string?> settings = ApiTestSettings.For(fixture.Database);
        foreach (string key in SmtpTestSettings.Dummy.Keys)
        {
            settings[key] = "";
        }

        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException
                && e.Message == "Smtp configuration is missing or invalid: Smtp:Host, Smtp:Port, Smtp:Security, Smtp:Username, Smtp:Password, Smtp:FromAddress, Smtp:FromName.");
    }

    [Fact]
    public async Task A_host_without_the_smtp_password_stops_at_start_naming_only_that_key()
    {
        Dictionary<string, string?> settings = ApiTestSettings.For(fixture.Database);
        settings["Smtp:Password"] = "";
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message == "Smtp configuration is missing or invalid: Smtp:Password.");
    }
}
