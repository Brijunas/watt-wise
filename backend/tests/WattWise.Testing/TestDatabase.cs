using Npgsql;

namespace WattWise.Testing;

/// <summary>One database cloned from the migrated template, with the settings to reach it.</summary>
public sealed class TestDatabase(
    string name,
    string host,
    int port,
    string adminConnectionString,
    IReadOnlyDictionary<DatabaseRole, string> passwords)
{
    public string Name { get; } = name;

    /// <summary>The <c>Database:*</c> configuration keys for connecting as <paramref name="role"/>.</summary>
    public IReadOnlyDictionary<string, string?> ConfigurationFor(DatabaseRole role) =>
        new Dictionary<string, string?>
        {
            ["Database:Host"] = host,
            ["Database:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Database:Name"] = Name,
            ["Database:Username"] = role.ToString().ToLowerInvariant(),
            ["Database:Password"] = passwords[role],
            ["Database:Options"] = role == DatabaseRole.Cli ? "-c role=owner" : string.Empty,
            ["Database:MaxPoolSize"] = "5",
        };

    public async Task DropAsync()
    {
        // FORCE drops the database even if a pooled connection is still open.
        NpgsqlConnection.ClearAllPools();
        await using NpgsqlConnection connection = new(adminConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"DROP DATABASE \"{Name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
