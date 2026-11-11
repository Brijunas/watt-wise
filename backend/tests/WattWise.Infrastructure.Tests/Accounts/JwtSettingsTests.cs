using Microsoft.Extensions.Configuration;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class JwtSettingsTests
{
    [Fact]
    public void ReadFrom_reads_the_three_settings_of_the_Jwt_section()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "key",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
            })
            .Build();
        JwtSettings settings = new();

        settings.ReadFrom(configuration);

        Assert.Equal(("key", "issuer", "audience"), (settings.SigningKey, settings.Issuer, settings.Audience));
    }

    [Fact]
    public void The_key_bytes_are_the_utf8_bytes_of_the_key()
    {
        Assert.Equal([0x61, 0xC3, 0xA9], new JwtSettings { SigningKey = "aé" }.SigningKeyBytes());
    }

    [Fact]
    public void The_key_bytes_of_a_missing_key_throw()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new JwtSettings().SigningKeyBytes());

        Assert.Equal("Jwt:SigningKey is not set.", exception.Message);
    }
}
