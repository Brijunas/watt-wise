using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using WattWise.Application.Email;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class HostSettingsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_host_with_blank_database_settings_stops_at_start()
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs))
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
    public async Task A_host_starts_without_smtp_settings_and_only_fails_when_it_sends_email()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => factory.Services.GetRequiredService<IEmailSender>());

        Assert.StartsWith("Smtp configuration is missing or invalid: ", exception.Message, StringComparison.Ordinal);
    }
}
