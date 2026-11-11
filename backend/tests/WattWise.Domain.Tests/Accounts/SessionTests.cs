using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Exceptions;

namespace WattWise.Domain.Tests.Accounts;

public class SessionTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);
    private static readonly Instant Expiry = Instant.FromUtc(2026, 11, 18, 12, 0);

    private static readonly byte[] FirstHash = [1, 2, 3];
    private static readonly byte[] SecondHash = [4, 5, 6];
    private static readonly byte[] ThirdHash = [7, 8, 9];

    private static Session NewSession() => Session.Start(Guid.CreateVersion7(), "Firefox on Linux", FirstHash, Now, Expiry);

    private static string Describe(RefreshToken token) =>
        $"{Convert.ToHexString(token.TokenHash)}|{token.IssuedAt}|{token.ExpiresAt}|{token.UsedAt?.ToString() ?? "unused"}";

    [Fact]
    public void Start_records_the_account_the_device_and_the_sign_in_time_as_the_last_use()
    {
        Guid accountId = Guid.CreateVersion7();

        Session session = Session.Start(accountId, "Firefox on Linux", FirstHash, Now, Expiry);

        Assert.Equal((accountId, "Firefox on Linux", Now, Now), (session.AccountId, session.DeviceLabel, session.SignedInAt, session.LastUsedAt));
    }

    [Fact]
    public void Start_makes_a_version_7_id()
    {
        Assert.Equal(7, NewSession().Id.Version);
    }

    [Fact]
    public void Start_adds_one_unused_token_that_belongs_to_the_session()
    {
        Session session = NewSession();

        RefreshToken token = Assert.Single(session.RefreshTokens);
        Assert.Equal(
            (session.Id, "010203|2026-11-11T12:00:00Z|2026-11-18T12:00:00Z|unused"),
            (token.SessionId, Describe(token)));
    }

    [Fact]
    public void Start_rejects_an_empty_account_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Session.Start(Guid.Empty, "Firefox on Linux", FirstHash, Now, Expiry));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Start_rejects_a_missing_device_label(string? label)
    {
        Assert.ThrowsAny<ArgumentException>(() => Session.Start(Guid.CreateVersion7(), label!, FirstHash, Now, Expiry));
    }

    [Fact]
    public void Start_rejects_an_empty_token_hash()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Session.Start(Guid.CreateVersion7(), "Firefox on Linux", [], Now, Expiry));
    }

    [Fact]
    public void Start_rejects_a_missing_token_hash()
    {
        Assert.Throws<ArgumentNullException>(() => Session.Start(Guid.CreateVersion7(), "Firefox on Linux", null!, Now, Expiry));
    }

    [Fact]
    public void Start_rejects_an_expiry_that_is_not_after_the_sign_in()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Session.Start(Guid.CreateVersion7(), "Firefox on Linux", FirstHash, Now, Now));
    }

    [Fact]
    public void Rotate_marks_the_presented_token_used_and_adds_the_new_one_valid_from_now()
    {
        Session session = NewSession();
        Instant later = Now + Duration.FromMinutes(20);
        Instant laterExpiry = later + Duration.FromDays(7);

        session.Rotate(FirstHash, SecondHash, later, laterExpiry);

        Assert.Equal(
            [
                "010203|2026-11-11T12:00:00Z|2026-11-18T12:00:00Z|2026-11-11T12:20:00Z",
                "040506|2026-11-11T12:20:00Z|2026-11-18T12:20:00Z|unused",
            ],
            session.RefreshTokens.Select(Describe));
    }

    [Fact]
    public void Rotate_sets_the_last_use_and_keeps_the_sign_in_time()
    {
        Session session = NewSession();
        Instant later = Now + Duration.FromMinutes(20);

        session.Rotate(FirstHash, SecondHash, later, later + Duration.FromDays(7));

        Assert.Equal((later, Now), (session.LastUsedAt, session.SignedInAt));
    }

    [Fact]
    public void Rotate_with_an_already_used_token_throws_RefreshTokenReused_and_changes_nothing()
    {
        Session session = NewSession();
        Instant later = Now + Duration.FromMinutes(20);
        session.Rotate(FirstHash, SecondHash, later, later + Duration.FromDays(7));

        RefreshTokenReusedException exception = Assert.Throws<RefreshTokenReusedException>(
            () => session.Rotate(FirstHash, ThirdHash, later + Duration.FromMinutes(1), later + Duration.FromDays(8)));

        Assert.Equal(
            ("Accounts.RefreshTokenReused", "The refresh token was already used.", 2, later),
            (exception.Code, exception.Message, session.RefreshTokens.Count, session.LastUsedAt));
    }

    [Fact]
    public void Rotate_throws_RefreshTokenExpired_at_the_expiry_instant_and_changes_nothing()
    {
        Session session = NewSession();

        RefreshTokenExpiredException exception = Assert.Throws<RefreshTokenExpiredException>(
            () => session.Rotate(FirstHash, SecondHash, Expiry, Expiry + Duration.FromDays(7)));

        Assert.Equal(
            ("Accounts.RefreshTokenExpired", "The refresh token has expired.", 1, Now),
            (exception.Code, exception.Message, session.RefreshTokens.Count, session.LastUsedAt));
    }

    [Fact]
    public void Rotate_accepts_a_token_one_tick_before_its_expiry()
    {
        Session session = NewSession();
        Instant justBefore = Expiry - Duration.FromTicks(1);

        session.Rotate(FirstHash, SecondHash, justBefore, justBefore + Duration.FromDays(7));

        Assert.Equal(2, session.RefreshTokens.Count);
    }

    [Fact]
    public void Rotate_with_a_hash_of_another_session_is_rejected()
    {
        Session session = NewSession();
        ArgumentException exception = Assert.Throws<ArgumentException>(() => session.Rotate(SecondHash, ThirdHash, Now, Expiry));

        Assert.Equal("The token does not belong to this session. (Parameter 'presentedHash')", exception.Message);
    }

    [Fact]
    public void Rotate_rejects_a_missing_presented_hash()
    {
        Session session = NewSession();

        Assert.Throws<ArgumentNullException>(() => session.Rotate(null!, SecondHash, Now, Expiry));
    }

    [Fact]
    public void Rotate_rejects_a_new_token_with_an_empty_hash()
    {
        Session session = NewSession();

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Rotate(FirstHash, [], Now + Duration.FromMinutes(1), Expiry));
    }
}
