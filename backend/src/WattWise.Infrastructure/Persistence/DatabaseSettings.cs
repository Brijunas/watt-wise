using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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

    /// <summary>Npgsql's Maximum Pool Size; keep it below the role's connection limit in bootstrap.sql.</summary>
    public int MaxPoolSize { get; set; }

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
        MaxPoolSize = int.TryParse(section["MaxPoolSize"], out int maxPoolSize) ? maxPoolSize : 0;
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
        if (MaxPoolSize <= 0) { missing.Add("MaxPoolSize"); }

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

        if (MaxPoolSize > 0)
        {
            builder.MaxPoolSize = MaxPoolSize;
        }

        if (!string.IsNullOrWhiteSpace(Options))
        {
            builder.Options = Options;
        }

        return builder.ConnectionString;
    }
}

/// <summary>Fails options resolution (and host start, through ValidateOnStart) listing the missing keys, never values.</summary>
internal sealed class DatabaseSettingsValidator : IValidateOptions<DatabaseSettings>
{
    public ValidateOptionsResult Validate(string? name, DatabaseSettings options)
    {
        IReadOnlyList<string> missing = options.MissingKeys();
        return missing.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Database configuration is missing or invalid: {string.Join(", ", missing)}.");
    }
}
