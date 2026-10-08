using Microsoft.Extensions.Configuration;

using Npgsql;

namespace WattWise.Infrastructure.Persistence;

/// <summary>Database connection settings, read from the "Database" configuration section.</summary>
/// <remarks>A class rather than a record, so ToString never prints the password.</remarks>
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    public string? Host { get; set; }

    public int Port { get; set; }

    public string? Name { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Options { get; set; }

    /// <summary>Reads the section without validating it.</summary>
    public static DatabaseSettings Read(IConfiguration configuration)
    {
        DatabaseSettings settings = new();
        settings.ReadFrom(configuration);
        return settings;
    }

    /// <summary>Fills this instance from the section. Blank or non-numeric numbers stay 0.</summary>
    public void ReadFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        Host = section["Host"];
        Port = int.TryParse(section["Port"], out int port) ? port : 0;
        Name = section["Name"];
        Username = section["Username"];
        Password = section["Password"];
        Options = section["Options"];
    }

    /// <summary>The keys that are missing or invalid, as "Database:Key"; empty when the settings are usable.</summary>
    public IReadOnlyList<string> MissingKeys()
    {
        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(Host)) { missing.Add("Host"); }
        if (Port <= 0) { missing.Add("Port"); }
        if (string.IsNullOrWhiteSpace(Name)) { missing.Add("Name"); }
        if (string.IsNullOrWhiteSpace(Username)) { missing.Add("Username"); }
        if (string.IsNullOrWhiteSpace(Password)) { missing.Add("Password"); }

        return [.. missing.Select(key => $"{SectionName}:{key}")];
    }

    public string ToConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = Host ?? string.Empty,
            Database = Name ?? string.Empty,
            Username = Username ?? string.Empty,
            Password = Password ?? string.Empty,
        };
        if (Port > 0)
        {
            builder.Port = Port;
        }

        if (!string.IsNullOrWhiteSpace(Options))
        {
            builder.Options = Options;
        }

        return builder.ConnectionString;
    }
}
