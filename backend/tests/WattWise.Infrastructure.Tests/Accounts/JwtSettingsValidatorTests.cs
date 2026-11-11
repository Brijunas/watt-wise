using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class JwtSettingsValidatorTests
{
    private static readonly string Key64 = new('k', 64);

    private static ValidateOptionsResult Validate(string? key, string? issuer = "wattwise-api", string? audience = "wattwise-clients") =>
        new JwtSettingsValidator().Validate(null, new JwtSettings { SigningKey = key, Issuer = issuer, Audience = audience });

    [Fact]
    public void A_64_character_key_with_an_issuer_and_an_audience_is_valid()
    {
        Assert.True(Validate(Key64).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_key_is_named_with_its_minimum_length(string? key)
    {
        Assert.Equal(
            "Jwt configuration is missing or invalid: Jwt:SigningKey (at least 64 characters).",
            Validate(key).FailureMessage);
    }

    [Fact]
    public void A_63_character_key_is_too_short()
    {
        Assert.Equal(
            "Jwt configuration is missing or invalid: Jwt:SigningKey (at least 64 characters).",
            Validate(new string('k', 63)).FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_missing_issuer_is_named(string? issuer)
    {
        Assert.Equal("Jwt configuration is missing or invalid: Jwt:Issuer.", Validate(Key64, issuer).FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_missing_audience_is_named(string? audience)
    {
        Assert.Equal("Jwt configuration is missing or invalid: Jwt:Audience.", Validate(Key64, "wattwise-api", audience).FailureMessage);
    }

    [Fact]
    public void Every_invalid_setting_is_listed_and_the_message_never_contains_the_key()
    {
        Assert.Equal(
            "Jwt configuration is missing or invalid: Jwt:SigningKey (at least 64 characters), Jwt:Issuer, Jwt:Audience.",
            Validate("short-secret", null, null).FailureMessage);
    }
}
