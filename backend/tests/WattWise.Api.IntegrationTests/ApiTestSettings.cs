namespace WattWise.Api.IntegrationTests;

/// <summary>The settings every Api host needs to start: the clone's <c>api</c> database.</summary>
internal static class ApiTestSettings
{
    public static Dictionary<string, string?> For(TestDatabase database)
    {
        return new Dictionary<string, string?>(database.ConfigurationFor(DatabaseRole.Api));
    }
}
