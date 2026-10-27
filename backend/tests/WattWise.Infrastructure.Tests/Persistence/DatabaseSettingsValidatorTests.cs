using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Tests.Persistence;

public class DatabaseSettingsValidatorTests
{
    private readonly DatabaseSettingsValidator _validator = new();

    private static DatabaseSettings Complete() => new()
    {
        Host = "h",
        Port = 5432,
        Name = "n",
        Username = "u",
        Password = "p",
    };

    [Fact]
    public void Complete_settings_pass()
    {
        ValidateOptionsResult result = _validator.Validate(null, Complete());

        Assert.Same(ValidateOptionsResult.Success, result);
    }

    // characterization: DatabaseSettingsValidator.cs:13 (message format), DatabaseSettings.cs:49-55 (key order)
    [Theory]
    [InlineData(null, 5432, "n", "u", "p", "Database:Host")]
    [InlineData("  ", 5432, "n", "u", "p", "Database:Host")]
    [InlineData("h", 0, "n", "u", "p", "Database:Port")]
    [InlineData("h", -1, "n", "u", "p", "Database:Port")]
    [InlineData("h", 5432, null, "u", "p", "Database:Name")]
    [InlineData("h", 5432, "n", null, "p", "Database:Username")]
    [InlineData("h", 5432, "n", "u", null, "Database:Password")]
    public void A_missing_value_fails_naming_exactly_that_key(string? host, int port, string? name, string? username, string? password, string key)
    {
        DatabaseSettings settings = new() { Host = host, Port = port, Name = name, Username = username, Password = password };

        ValidateOptionsResult result = _validator.Validate(null, settings);

        Assert.Equal($"Database configuration is missing or invalid: {key}.", result.FailureMessage);
    }

    // characterization: DatabaseSettingsValidator.cs:13 (message format), DatabaseSettings.cs:49-55 (key order)
    [Fact]
    public void Several_missing_keys_are_all_listed_in_order()
    {
        ValidateOptionsResult result = _validator.Validate(null, new DatabaseSettings());

        Assert.Equal(
            "Database configuration is missing or invalid: Database:Host, Database:Port, Database:Name, Database:Username, Database:Password.",
            result.FailureMessage);
    }

    // characterization: DatabaseSettingsValidator.cs:13 (message format)
    [Fact]
    public void A_non_numeric_port_fails_naming_the_port_key()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "h",
                ["Database:Port"] = "abc",
                ["Database:Name"] = "n",
                ["Database:Username"] = "u",
                ["Database:Password"] = "p",
            })
            .Build();

        ValidateOptionsResult result = _validator.Validate(null, DatabaseSettings.Read(configuration));

        Assert.Equal("Database configuration is missing or invalid: Database:Port.", result.FailureMessage);
    }
}
