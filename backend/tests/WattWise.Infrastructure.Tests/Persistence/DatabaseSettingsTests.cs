using Microsoft.Extensions.Configuration;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Tests.Persistence;

public class DatabaseSettingsTests
{
    private static IConfiguration Configure(string port) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Host"] = "db-host",
            ["Database:Port"] = port,
            ["Database:Name"] = "db-name",
            ["Database:Username"] = "db-user",
            ["Database:Password"] = "db-pass",
            ["Database:Options"] = "-c x=1",
        })
        .Build();

    [Fact]
    public void Read_takes_every_value_from_the_Database_section()
    {
        DatabaseSettings settings = DatabaseSettings.Read(Configure("5433"));

        Assert.Equal(
            ("db-host", 5433, "db-name", "db-user", "db-pass", "-c x=1"),
            (settings.Host, settings.Port, settings.Name, settings.Username, settings.Password, settings.Options));
    }
}
