using Microsoft.Extensions.Configuration;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class EmailRetrySettingsTests
{
    [Fact]
    public void A_kinds_section_is_email_retry_and_the_kind()
    {
        Assert.Equal("Email:Retry:AccountAlreadyExists", EmailRetrySettings.SectionFor(EmailRequestKind.AccountAlreadyExists));
    }

    [Fact]
    public void Attempts_and_delays_are_read_from_the_kinds_section()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Retry:PasswordReset:Attempts"] = "4",
            ["Email:Retry:PasswordReset:Delays:0"] = "00:00:00",
            ["Email:Retry:PasswordReset:Delays:1"] = "00:01:00",
            ["Email:Retry:PasswordReset:Delays:2"] = "00:05:00",
            ["Email:Retry:PasswordReset:Delays:3"] = "00:15:00",
            ["Email:Retry:EmailConfirmation:Attempts"] = "6",
        }).Build();
        EmailRetrySettings settings = new();

        settings.ReadFrom(configuration, EmailRequestKind.PasswordReset);

        Assert.Equal((4, "0,1,5,15"), (settings.Attempts, string.Join(",", settings.Delays.Select(delay => delay.TotalMinutes))));
    }

    [Fact]
    public void A_kind_missing_from_the_configuration_reads_as_no_attempts_and_no_delays()
    {
        EmailRetrySettings settings = new() { Attempts = 9, Delays = [TimeSpan.FromMinutes(1)] };

        settings.ReadFrom(new ConfigurationBuilder().Build(), EmailRequestKind.PasswordReset);

        Assert.Equal((0, 0), (settings.Attempts, settings.Delays.Count));
    }

    [Fact]
    public void A_delay_that_is_not_a_time_span_reads_as_an_invalid_value_the_validator_rejects()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Retry:PasswordReset:Attempts"] = "1",
            ["Email:Retry:PasswordReset:Delays:0"] = "soon",
        }).Build();
        EmailRetrySettings settings = new();

        settings.ReadFrom(configuration, EmailRequestKind.PasswordReset);

        Assert.Equal(TimeSpan.MinValue, Assert.Single(settings.Delays));
    }
}
