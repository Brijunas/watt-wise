using System.Net;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock, so it has a fixture (and clock) of its own; every test signs in after any earlier move.</summary>
public class RefreshTokenExpiryTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SignedInSession> SignedInAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        await fixture.CreateAccountAsync(email, password);
        return await fixture.SignInAsync(email, password);
    }

    private Task<HttpResponseMessage> RefreshAsync(string cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie);

    [Fact]
    public async Task The_refresh_cookie_still_works_one_second_before_7_days_have_passed()
    {
        SignedInSession session = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromDays(7) - Duration.FromSeconds(1));

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_refresh_cookie_is_refused_once_7_days_have_passed()
    {
        SignedInSession session = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromDays(7));

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task An_expired_refresh_clears_the_cookie()
    {
        SignedInSession session = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromDays(7));

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Fact]
    public async Task Each_refresh_slides_the_expiry_so_the_new_cookie_lasts_7_days_from_the_refresh()
    {
        SignedInSession session = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromDays(6));
        using HttpResponseMessage first = await RefreshAsync(session.Cookie);
        fixture.Clock.Advance(Duration.FromDays(6));

        using HttpResponseMessage second = await RefreshAsync(SetCookieHeader.RefreshCookieOf(first)!.NameAndValue);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task The_new_cookie_expires_7_days_after_the_refresh_not_after_the_sign_in()
    {
        SignedInSession session = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromDays(3));
        Instant refreshedAt = fixture.Clock.GetCurrentInstant();

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        Assert.Equal(
            (refreshedAt + Duration.FromDays(7)).ToDateTimeOffset(),
            DateTimeOffset.Parse(SetCookieHeader.RefreshCookieOf(response)!.Attributes["expires"], System.Globalization.CultureInfo.InvariantCulture));
    }
}
