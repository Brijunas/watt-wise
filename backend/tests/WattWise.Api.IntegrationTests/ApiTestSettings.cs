namespace WattWise.Api.IntegrationTests;

/// <summary>The settings every Api host needs to start: the clone's <c>api</c> database and the Jwt settings.</summary>
internal static class ApiTestSettings
{
    /// <summary>A fixed key for the tests; 64 characters, the shortest the Api accepts.</summary>
    public const string JwtSigningKey = "test-signing-key-test-signing-key-test-signing-key-test-signing-key";

    public static Dictionary<string, string?> For(TestDatabase database)
    {
        return new Dictionary<string, string?>(database.ConfigurationFor(DatabaseRole.Api))
        {
            ["Jwt:SigningKey"] = JwtSigningKey,
            ["Jwt:Issuer"] = "wattwise-api",
            ["Jwt:Audience"] = "wattwise-clients",
        };
    }
}
