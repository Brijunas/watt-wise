using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class SmtpSettingsValidatorTests
{
    private readonly SmtpSettingsValidator _validator = new(new TestHostEnvironment("Development"));

    [Fact]
    public void Complete_settings_pass()
    {
        ValidateOptionsResult result = _validator.Validate(null, CompleteSmtpSettings.Create());

        Assert.Same(ValidateOptionsResult.Success, result);
    }

    [Fact]
    public void Every_missing_key_is_listed_in_order()
    {
        ValidateOptionsResult result = _validator.Validate(null, new SmtpSettings());

        Assert.Equal(
            "Smtp configuration is missing or invalid: Smtp:Host, Smtp:Port, Smtp:Security, Smtp:Username, Smtp:Password, Smtp:FromAddress, Smtp:FromName.",
            result.FailureMessage);
    }

    [Fact]
    public void The_message_names_the_missing_key_and_prints_no_value()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Host = null;
        ValidateOptionsResult result = _validator.Validate(null, settings);

        Assert.Equal("Smtp configuration is missing or invalid: Smtp:Host.", result.FailureMessage);
    }

    [Fact]
    public void No_encryption_fails_outside_development_naming_the_security_key()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = SmtpSecurity.None;
        SmtpSettingsValidator validator = new(new TestHostEnvironment("Production"));

        ValidateOptionsResult result = validator.Validate(null, settings);

        Assert.Equal("Smtp configuration is missing or invalid: Smtp:Security.", result.FailureMessage);
    }

    [Fact]
    public void No_encryption_passes_in_development()
    {
        SmtpSettings settings = CompleteSmtpSettings.Create();
        settings.Security = SmtpSecurity.None;

        ValidateOptionsResult result = _validator.Validate(null, settings);

        Assert.Same(ValidateOptionsResult.Success, result);
    }
}
