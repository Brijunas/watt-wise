using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class JwtAccessTokenIssuerTests
{
    private const string Key = "unit-test-signing-key-unit-test-signing-key-unit-test-signing-key";

    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid SessionId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0, 0);

    private readonly JwtAccessTokenIssuer _issuer = new(Options.Create(new JwtSettings { SigningKey = Key, Issuer = "wattwise-api", Audience = "wattwise-clients" }));

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public void The_token_carries_subject_session_and_the_fixed_issuer_audience_and_algorithm()
    {
        JsonWebToken token = Read(_issuer.Issue(AccountId, SessionId, [], Now).Token);

        Assert.Equal(
            ($"{AccountId}", $"{SessionId}", "wattwise-api", "wattwise-clients", "HS256"),
            (token.Subject, token.GetPayloadValue<string>("sid"), token.Issuer, Assert.Single(token.Audiences), token.Alg));
    }

    [Fact]
    public void The_token_is_issued_now_and_expires_15_minutes_later()
    {
        IssuedAccessToken issued = _issuer.Issue(AccountId, SessionId, [], Now);

        JsonWebToken token = Read(issued.Token);
        // 2026-11-11T12:00:00Z is 1794398400 seconds since the epoch; 15 minutes is 900.
        Assert.Equal(
            (1794398400L, 1794399300L, Now + Duration.FromMinutes(15)),
            (new DateTimeOffset(token.IssuedAt).ToUnixTimeSeconds(), new DateTimeOffset(token.ValidTo).ToUnixTimeSeconds(), issued.ExpiresAt));
    }

    [Fact]
    public void The_token_is_valid_from_the_instant_it_is_issued_by_the_given_clock_not_the_machines()
    {
        JsonWebToken token = Read(_issuer.Issue(AccountId, SessionId, [], Now).Token);

        // 2026-11-11T12:00:00Z; the machine's own time is never 2026-11-11T12:00:00 exactly, so a defaulted nbf shows here.
        Assert.Equal(1794398400L, new DateTimeOffset(token.ValidFrom).ToUnixTimeSeconds());
    }

    [Fact]
    public void The_reported_expiry_is_the_tokens_own_whole_second_expiry()
    {
        IssuedAccessToken issued = _issuer.Issue(AccountId, SessionId, [], Now + Duration.FromMilliseconds(900));

        Assert.Equal(Instant.FromUtc(2026, 11, 11, 12, 15, 0), issued.ExpiresAt);
    }

    [Fact]
    public void There_is_one_permission_claim_per_permission()
    {
        JsonWebToken token = Read(_issuer.Issue(AccountId, SessionId, ["a.read", "b.write"], Now).Token);

        Assert.Equal(["a.read", "b.write"], token.GetPayloadValue<string[]>("permission"));
    }

    [Fact]
    public void With_no_permissions_the_claim_is_left_out()
    {
        JsonWebToken token = Read(_issuer.Issue(AccountId, SessionId, [], Now).Token);

        Assert.False(token.TryGetPayloadValue<object>("permission", out _));
    }

    [Fact]
    public void Two_tokens_for_the_same_session_have_different_ids()
    {
        string first = Read(_issuer.Issue(AccountId, SessionId, [], Now).Token).Id;
        string second = Read(_issuer.Issue(AccountId, SessionId, [], Now).Token).Id;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task The_token_is_signed_with_the_configured_key_and_fails_with_another()
    {
        string token = _issuer.Issue(AccountId, SessionId, [], Now).Token;
        static TokenValidationParameters Parameters(string key) => new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        };

        TokenValidationResult right = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters(Key));
        TokenValidationResult wrong = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters(new string('x', 64)));

        Assert.Equal((true, false), (right.IsValid, wrong.IsValid));
    }

    [Fact]
    public void The_issued_token_text_is_hidden_in_ToString()
    {
        Assert.Equal("IssuedAccessToken", _issuer.Issue(AccountId, SessionId, [], Now).ToString());
    }
}
