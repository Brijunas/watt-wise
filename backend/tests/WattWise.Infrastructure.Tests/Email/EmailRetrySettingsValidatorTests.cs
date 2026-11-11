using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.Tests.Email;

public class EmailRetrySettingsValidatorTests
{
    private readonly EmailRetrySettingsValidator _validator = new();

    [Theory]
    [InlineData("PasswordReset", 4, "0,1,5,15")]
    [InlineData("EmailConfirmation", 6, "0,1,5,15,60,240")]
    [InlineData("AccountAlreadyExists", 3, "0,5,30")]
    [InlineData("PasswordReset", 1, "0")]
    public void Matching_delays_that_start_at_zero_and_are_not_negative_pass(string kind, int attempts, string delayMinutes)
    {
        ValidateOptionsResult result = _validator.Validate(kind, Settings(attempts, delayMinutes));

        Assert.Same(ValidateOptionsResult.Success, result);
    }

    [Theory]
    [InlineData(3, "0,5")]
    [InlineData(2, "0,5,30")]
    [InlineData(1, "")]
    public void A_delay_list_that_does_not_match_the_attempts_fails_naming_the_delays_key(int attempts, string delayMinutes)
    {
        ValidateOptionsResult result = _validator.Validate("AccountAlreadyExists", Settings(attempts, delayMinutes));

        Assert.Equal((true, true), (result.Failed, result.FailureMessage!.Contains("Email:Retry:AccountAlreadyExists:Delays", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_negative_delay_fails_naming_the_delays_key()
    {
        ValidateOptionsResult result = _validator.Validate("EmailConfirmation", Settings(3, "0,-1,5"));

        Assert.Equal((true, true), (result.Failed, result.FailureMessage!.Contains("Email:Retry:EmailConfirmation:Delays", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_first_delay_that_is_not_zero_fails_naming_the_delays_key()
    {
        ValidateOptionsResult result = _validator.Validate("PasswordReset", Settings(3, "1,5,15"));

        Assert.Equal((true, true), (result.Failed, result.FailureMessage!.Contains("Email:Retry:PasswordReset:Delays", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fewer_than_one_attempt_fails_naming_the_attempts_key(int attempts)
    {
        ValidateOptionsResult result = _validator.Validate("PasswordReset", new EmailRetrySettings { Attempts = attempts, Delays = [] });

        Assert.Equal((true, true), (result.Failed, result.FailureMessage!.Contains("Email:Retry:PasswordReset:Attempts", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_kind_missing_from_the_configuration_fails()
    {
        ValidateOptionsResult result = _validator.Validate("AccountAlreadyExists", new EmailRetrySettings());

        Assert.True(result.Failed);
    }

    [Fact]
    public void The_message_holds_key_names_and_no_values()
    {
        ValidateOptionsResult result = _validator.Validate("PasswordReset", Settings(3, "4321,5,15"));

        Assert.DoesNotContain("4321", result.FailureMessage, StringComparison.Ordinal);
    }

    private static EmailRetrySettings Settings(int attempts, string delayMinutes) => new()
    {
        Attempts = attempts,
        Delays = [.. delayMinutes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(minutes => TimeSpan.FromMinutes(int.Parse(minutes, System.Globalization.CultureInfo.InvariantCulture)))],
    };

    [Fact]
    public void Without_an_options_name_the_message_names_the_section_with_a_kind_placeholder()
    {
        ValidateOptionsResult result = _validator.Validate(null, new EmailRetrySettings { Attempts = 0, Delays = [] });

        Assert.Equal("Email:Retry:<Kind>:Attempts must be at least one.", result.FailureMessage);
    }
}
