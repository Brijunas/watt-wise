using Microsoft.Extensions.Configuration;

using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class SmtpSettingsTests
{
    private static IConfiguration Configure(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Complete_settings_have_no_missing_keys()
    {
        IReadOnlyList<string> missing = CompleteSmtpSettings.Create().MissingKeys(isDevelopment: true);

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData(null, 587, SmtpSecurity.StartTls, "u", "p", "n", "Smtp:Host")]
    [InlineData("  ", 587, SmtpSecurity.StartTls, "u", "p", "n", "Smtp:Host")]
    [InlineData("h", 0, SmtpSecurity.StartTls, "u", "p", "n", "Smtp:Port")]
    [InlineData("h", -1, SmtpSecurity.StartTls, "u", "p", "n", "Smtp:Port")]
    [InlineData("h", 65536, SmtpSecurity.StartTls, "u", "p", "n", "Smtp:Port")]
    [InlineData("h", 587, null, "u", "p", "n", "Smtp:Security")]
    [InlineData("h", 587, SmtpSecurity.None, null, "p", "n", "Smtp:Username")]
    [InlineData("h", 587, SmtpSecurity.None, "u", null, "n", "Smtp:Password")]
    [InlineData("h", 587, SmtpSecurity.None, "u", "p", "  ", "Smtp:FromName")]
    public void A_missing_value_is_the_only_key_reported(
        string? host, int port, SmtpSecurity? security, string? username, string? password, string? fromName, string key)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Host = host;
        settings.Port = port;
        settings.Security = security;
        settings.Username = username;
        settings.Password = password;
        settings.FromName = fromName;

        IReadOnlyList<string> missing = settings.MissingKeys(isDevelopment: true);

        Assert.Equal([key], missing);
    }

    [Fact]
    public void The_highest_tcp_port_is_valid()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Port = 65535;

        IReadOnlyList<string> missing = settings.MissingKeys(isDevelopment: true);

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData(SmtpSecurity.StartTls, false)]
    [InlineData(SmtpSecurity.SslOnConnect, false)]
    [InlineData(SmtpSecurity.StartTls, true)]
    [InlineData(SmtpSecurity.SslOnConnect, true)]
    [InlineData(SmtpSecurity.None, true)]
    public void An_encrypted_mode_is_valid_everywhere_and_none_only_in_development(SmtpSecurity security, bool isDevelopment)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = security;

        IReadOnlyList<string> missing = settings.MissingKeys(isDevelopment);

        Assert.Empty(missing);
    }

    [Fact]
    public void No_encryption_outside_development_is_reported_as_the_security_key()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = SmtpSecurity.None;

        IReadOnlyList<string> missing = settings.MissingKeys(isDevelopment: false);

        Assert.Equal(["Smtp:Security"], missing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("a@")]
    [InlineData("Bob <bob@wattwise.test>")]
    public void A_from_address_that_is_not_a_plain_address_with_a_domain_is_reported(string? address)
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.FromAddress = address;

        IReadOnlyList<string> missing = settings.MissingKeys(isDevelopment: true);

        Assert.Equal(["Smtp:FromAddress"], missing);
    }

    [Fact]
    public void An_invalid_from_address_has_an_empty_domain()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.FromAddress = "not-an-address";

        Assert.Equal(string.Empty, settings.FromDomain);
    }

    [Fact]
    public void ReadFrom_takes_every_value_from_the_Smtp_section_and_ignores_the_case_of_the_security_mode()
    {
        IConfiguration configuration = Configure(new()
        {
            ["Smtp:Host"] = "smtp.example.test",
            ["Smtp:Port"] = "1025",
            ["Smtp:Security"] = "none",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "password",
            ["Smtp:FromAddress"] = "noreply@wattwise.lt",
            ["Smtp:FromName"] = "Watt-Wise",
        });
        SmtpSettings settings = new();

        settings.ReadFrom(configuration);

        Assert.Equal(
            ("smtp.example.test", 1025, (SmtpSecurity?)SmtpSecurity.None, "user", "password", "noreply@wattwise.lt", "Watt-Wise", "wattwise.lt"),
            (settings.Host, settings.Port, settings.Security, settings.Username, settings.Password, settings.FromAddress, settings.FromName, settings.FromDomain));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void A_blank_or_non_numeric_port_reads_as_zero(string port)
    {
        SmtpSettings settings = new();

        settings.ReadFrom(Configure(new() { ["Smtp:Port"] = port }));

        Assert.Equal(0, settings.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bogus")]
    [InlineData("7")]
    public void A_blank_or_unknown_security_mode_reads_as_unset(string security)
    {
        SmtpSettings settings = new();

        settings.ReadFrom(Configure(new() { ["Smtp:Security"] = security }));

        Assert.Null(settings.Security);
    }
}
