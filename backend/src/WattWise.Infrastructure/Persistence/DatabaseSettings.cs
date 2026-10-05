using Microsoft.Extensions.Configuration;

using Npgsql;

namespace WattWise.Infrastructure.Persistence;

/// <summary>Database connection settings, read from the "Database" configuration section.</summary>
public sealed record DatabaseSettings(
    string? Host,
    int Port,
    string? Name,
    string? Username,
    string? Password,
    string? Options)
{
    public const string SectionName = "Database";

    /// <summary>Reads the section without validating it.</summary>
    public static DatabaseSettings Read(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);
        int.TryParse(section["Port"], out int port);
        return new DatabaseSettings(
            section["Host"],
            port,
            section["Name"],
            section["Username"],
            section["Password"],
            section["Options"]);
    }

    /// <summary>Reads the section and throws when a required key is missing.</summary>
    public static DatabaseSettings FromConfiguration(IConfiguration configuration)
    {
        DatabaseSettings settings = Read(configuration);

        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(settings.Host)) { missing.Add("Host"); }
        if (settings.Port <= 0) { missing.Add("Port"); }
        if (string.IsNullOrWhiteSpace(settings.Name)) { missing.Add("Name"); }
        if (string.IsNullOrWhiteSpace(settings.Username)) { missing.Add("Username"); }
        if (string.IsNullOrWhiteSpace(settings.Password)) { missing.Add("Password"); }

        if (missing.Count > 0)
        {
            string keys = string.Join(", ", missing.Select(key => $"{SectionName}:{key}"));
            throw new InvalidOperationException($"Database configuration is missing or invalid: {keys}.");
        }

        return settings;
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
