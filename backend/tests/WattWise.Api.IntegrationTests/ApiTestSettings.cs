namespace WattWise.Api.IntegrationTests;

/// <summary>The settings every Api host needs to start: the clone's <c>api</c> database and dummy <c>Smtp:*</c> values.</summary>
internal static class ApiTestSettings
{
    public static Dictionary<string, string?> For(TestDatabase database)
    {
        Dictionary<string, string?> settings = new(database.ConfigurationFor(DatabaseRole.Api));
        foreach ((string key, string? value) in SmtpTestSettings.Dummy)
        {
            settings[key] = value;
        }

        return settings;
    }
}
