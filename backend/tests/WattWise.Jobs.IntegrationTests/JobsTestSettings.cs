namespace WattWise.Jobs.IntegrationTests;

/// <summary>The settings a Jobs host needs to start: the clone's <c>jobs</c> database role, dummy <c>Smtp</c> settings and valid <c>Links</c>. <c>Email:Retry</c> comes from the host's own appsettings.json.</summary>
internal static class JobsTestSettings
{
    public const string FrontendBaseUrl = "https://app.wattwise.test";

    public static Dictionary<string, string?> For(DatabaseFixture fixture)
    {
        Dictionary<string, string?> settings = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs))
        {
            ["Links:FrontendBaseUrl"] = FrontendBaseUrl,
        };
        foreach (KeyValuePair<string, string?> smtp in SmtpTestSettings.Dummy)
        {
            settings[smtp.Key] = smtp.Value;
        }

        return settings;
    }
}
