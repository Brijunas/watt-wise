using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using WattWise.Api.Authentication;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Api.Tests.Authentication;

public class ConfigureJwtBearerOptionsTests
{
    private static readonly string Key = new('k', 64);
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static JwtBearerOptions Configure(DateTimeOffset? now = null, string? name = null)
    {
        JwtBearerOptions options = new();
        new ConfigureJwtBearerOptions(
            Options.Create(new JwtSettings { SigningKey = Key, Issuer = "the-issuer", Audience = "the-audience" }),
            new FixedTimeProvider(now ?? Now)).Configure(name ?? JwtBearerDefaults.AuthenticationScheme, options);
        return options;
    }

    [Fact]
    public void Validation_requires_a_signed_token_with_the_configured_issuer_audience_and_key_and_an_expiry()
    {
        TokenValidationParameters parameters = Configure().TokenValidationParameters;

        Assert.Equal(
            (true, "the-issuer", true, "the-audience", true, true, true, true),
            (
                parameters.ValidateIssuer, parameters.ValidIssuer, parameters.ValidateAudience, parameters.ValidAudience,
                parameters.ValidateIssuerSigningKey, parameters.RequireSignedTokens, parameters.RequireExpirationTime, parameters.ValidateLifetime));
    }

    [Fact]
    public void The_signing_key_is_the_utf8_bytes_of_the_configured_key()
    {
        SymmetricSecurityKey key = Assert.IsType<SymmetricSecurityKey>(Configure().TokenValidationParameters.IssuerSigningKey);

        Assert.Equal(Encoding.UTF8.GetBytes(Key), key.Key);
    }

    [Fact]
    public void Only_HS256_is_a_valid_algorithm()
    {
        Assert.Equal([SecurityAlgorithms.HmacSha256], Configure().TokenValidationParameters.ValidAlgorithms);
    }

    [Fact]
    public void There_is_no_clock_skew()
    {
        Assert.Equal(TimeSpan.Zero, Configure().TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void Claims_keep_their_token_names_and_a_401_hides_the_reason()
    {
        JwtBearerOptions options = Configure();

        Assert.Equal((false, false), (options.MapInboundClaims, options.IncludeErrorDetails));
    }

    [Fact]
    public void The_options_read_the_time_from_the_given_time_provider()
    {
        FixedTimeProvider time = new(Now);
        JwtBearerOptions options = new();
        new ConfigureJwtBearerOptions(Options.Create(new JwtSettings { SigningKey = Key, Issuer = "i", Audience = "a" }), time)
            .Configure(JwtBearerDefaults.AuthenticationScheme, options);

        Assert.Same(time, options.TimeProvider);
    }

    [Fact]
    public void Another_scheme_name_is_left_alone()
    {
        JwtBearerOptions options = Configure(name: "Other");

        Assert.Equal((true, null), (options.MapInboundClaims, options.TokenValidationParameters.ValidIssuer));
    }

    private static bool Valid(DateTime? notBefore, DateTime? expires)
    {
        Func<DateTime?, DateTime?, SecurityToken?, TokenValidationParameters, bool> validator = Configure().TokenValidationParameters.LifetimeValidator is { } v
            ? (nbf, exp, token, parameters) => v(nbf, exp, token!, parameters)
            : throw new InvalidOperationException();
        return validator(notBefore, expires, null, new TokenValidationParameters());
    }

    [Fact]
    public void A_token_is_valid_one_second_before_it_expires()
    {
        Assert.True(Valid(Now.UtcDateTime.AddHours(-1), Now.UtcDateTime.AddSeconds(1)));
    }

    [Fact]
    public void A_token_is_not_valid_at_the_instant_it_expires()
    {
        Assert.False(Valid(Now.UtcDateTime.AddHours(-1), Now.UtcDateTime));
    }

    [Fact]
    public void A_token_is_valid_at_the_instant_it_becomes_valid()
    {
        Assert.True(Valid(Now.UtcDateTime, Now.UtcDateTime.AddHours(1)));
    }

    [Fact]
    public void A_token_is_not_valid_one_second_before_it_becomes_valid()
    {
        Assert.False(Valid(Now.UtcDateTime.AddSeconds(1), Now.UtcDateTime.AddHours(1)));
    }

    [Fact]
    public void A_token_with_no_not_before_is_valid_until_it_expires()
    {
        Assert.True(Valid(null, Now.UtcDateTime.AddHours(1)));
    }

    [Fact]
    public void A_token_with_no_expiry_is_never_valid()
    {
        Assert.False(Valid(Now.UtcDateTime.AddHours(-1), null));
    }
}
