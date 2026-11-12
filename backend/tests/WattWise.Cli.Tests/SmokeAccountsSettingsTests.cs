using Microsoft.Extensions.Configuration;

using WattWise.Cli.SmokeAccounts;

namespace WattWise.Cli.Tests;

public class SmokeAccountsSettingsTests
{
    [Fact]
    public void Each_key_of_the_SmokeAccounts_section_fills_its_own_property()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeAccounts:UserEmail"] = "ue",
                ["SmokeAccounts:UserPassword"] = "up",
                ["SmokeAccounts:AdminEmail"] = "ae",
                ["SmokeAccounts:AdminPassword"] = "ap",
                ["Other:UserEmail"] = "wrong",
            })
            .Build();
        SmokeAccountsSettings settings = new();

        settings.ReadFrom(configuration);

        Assert.Equal(("ue", "up", "ae", "ap"), (settings.UserEmail, settings.UserPassword, settings.AdminEmail, settings.AdminPassword));
    }

    [Fact]
    public void An_absent_section_leaves_every_value_null()
    {
        SmokeAccountsSettings settings = new();

        settings.ReadFrom(new ConfigurationBuilder().Build());

        Assert.Equal((null, null, null, null), (settings.UserEmail, settings.UserPassword, settings.AdminEmail, settings.AdminPassword));
    }
}
