using Microsoft.Extensions.Options;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class RequiredSettingsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData("Smtp:Host", "", "Smtp configuration is missing or invalid")]
    [InlineData("Smtp:Password", "", "Smtp configuration is missing or invalid")]
    [InlineData("Smtp:FromAddress", "", "Smtp configuration is missing or invalid")]
    [InlineData("Links:FrontendBaseUrl", "", "Links:FrontendBaseUrl")]
    [InlineData("Links:FrontendBaseUrl", "https://app.wattwise.test/", "Links:FrontendBaseUrl")]
    [InlineData("Email:Retry:PasswordReset:Attempts", "0", "Email:Retry:PasswordReset")]
    [InlineData("Email:Retry:AccountAlreadyExists:Attempts", "5", "Email:Retry:AccountAlreadyExists")]
    [InlineData("Email:Retry:EmailConfirmation:Delays:0", "5", "Email:Retry:EmailConfirmation")]
    public async Task A_host_with_a_missing_or_invalid_setting_stops_at_start_naming_it(string key, string value, string expectedInMessage)
    {
        Dictionary<string, string?> settings = new(JobsTestSettings.For(fixture)) { [key] = value };
        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException && e.Message.Contains(expectedInMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_host_without_any_smtp_settings_stops_at_start()
    {
        Dictionary<string, string?> settings = new(JobsTestSettings.For(fixture));
        foreach (string key in settings.Keys.Where(key => key.StartsWith("Smtp:", StringComparison.Ordinal)).ToList())
        {
            settings[key] = "";
        }

        await using TestHostFactory<Program> factory = new(settings);

        Exception? exception = Record.Exception(() => factory.CreateClient());

        Assert.Contains(
            ExceptionChain.Of(exception!),
            e => e is OptionsValidationException
                && e.Message.StartsWith("Smtp configuration is missing or invalid: ", StringComparison.Ordinal));
    }
}
