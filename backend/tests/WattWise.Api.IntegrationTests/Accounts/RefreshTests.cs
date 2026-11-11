using System.Net;
using System.Text.Json;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock (the rotation time), so it has a fixture (and clock) of its own.</summary>
public class RefreshTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    private async Task<(AccountSeeder.Ids Ids, SignedInSession Session)> SignedInAsync()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        return (ids, await fixture.SignInAsync(email, password));
    }

    private Task<HttpResponseMessage> RefreshAsync(string? cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie);

    [Fact]
    public async Task Refreshing_answers_200_with_the_access_token_and_its_expiry_and_nothing_else()
    {
        (_, SignedInSession session) = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromMinutes(10));

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.OK, "accessToken,accessTokenExpiresAt"),
            (response.StatusCode, string.Join(',', body.RootElement.EnumerateObject().Select(property => property.Name))));
    }

    [Fact]
    public async Task The_new_access_token_expires_15_minutes_after_the_refresh()
    {
        (_, SignedInSession session) = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromMinutes(10));
        DateTimeOffset expected = fixture.Clock.GetCurrentInstant().Plus(Duration.FromMinutes(15)).ToDateTimeOffset();

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(expected, body.RootElement.GetProperty("accessTokenExpiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_new_access_token_works()
    {
        (_, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage refreshed = await RefreshAsync(session.Cookie);
        using JsonDocument body = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync(Token));

        using HttpResponseMessage me = await fixture.SendAsync(
            HttpMethod.Get, "/api/v1/me", bearer: body.RootElement.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_refresh_marks_the_old_token_used_adds_a_new_one_and_keeps_the_session()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        Assert.Equal(
            ("used,unused", 1),
            (string.Join(',', await fixture.RefreshTokenStatesAsync(ids.AccountId)), (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }

    [Fact]
    public async Task A_refresh_sets_the_sessions_last_use_to_now()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();
        fixture.Clock.Advance(Duration.FromMinutes(5));

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        // device | signed_in = now | last_used = now | tokens: only the last use moved.
        Assert.Equal(["Unknown device|false|true|2"], await fixture.SessionRowsAsync(ids.AccountId));
    }

    [Fact]
    public async Task The_refreshed_access_token_keeps_the_session_and_carries_the_permissions_the_account_has_by_then()
    {
        string prefix = Guid.NewGuid().ToString("N");
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();
        await fixture.GrantAsync(ids.AccountId, $"{prefix}-role", $"{prefix}.later");
        string sessionId = await fixture.ScalarAsync($"SELECT id::text AS \"Value\" FROM app.sessions WHERE account_id = {ids.AccountId}");

        using HttpResponseMessage response = await RefreshAsync(session.Cookie);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Microsoft.IdentityModel.JsonWebTokens.JsonWebToken token = TestJwt.Read(body.RootElement.GetProperty("accessToken").GetString()!);
        Assert.Equal(
            (sessionId, $"{prefix}.later"),
            (token.GetPayloadValue<string>("sid"), string.Join(',', token.GetPayloadValue<string[]>("permission"))));
    }

    [Fact]
    public async Task Without_a_cookie_refresh_answers_401_InvalidRefreshToken()
    {
        using HttpResponseMessage response = await RefreshAsync(null);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task Without_a_cookie_a_failed_refresh_clears_the_cookie()
    {
        using HttpResponseMessage response = await RefreshAsync(null);

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Fact]
    public async Task An_unknown_cookie_value_answers_401_InvalidRefreshToken()
    {
        using HttpResponseMessage response = await RefreshAsync($"{SetCookieHeader.RefreshCookieName}={new string('A', 43)}");

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task An_unknown_cookie_value_clears_the_cookie()
    {
        using HttpResponseMessage response = await RefreshAsync($"{SetCookieHeader.RefreshCookieName}={new string('A', 43)}");

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Fact]
    public async Task Replaying_the_old_cookie_answers_401_InvalidRefreshToken()
    {
        (_, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage first = await RefreshAsync(session.Cookie);

        using HttpResponseMessage replay = await RefreshAsync(session.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(replay, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task Replaying_the_old_cookie_clears_the_cookie()
    {
        (_, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage first = await RefreshAsync(session.Cookie);

        using HttpResponseMessage replay = await RefreshAsync(session.Cookie);

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(replay));
    }

    [Fact]
    public async Task Replaying_the_old_cookie_ends_the_session_and_deletes_its_tokens()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage first = await RefreshAsync(session.Cookie);
        using HttpResponseMessage replay = await RefreshAsync(session.Cookie);

        Assert.Equal(
            ("", ""),
            (string.Join(',', await fixture.SessionRowsAsync(ids.AccountId)), string.Join(',', await fixture.RefreshTokenStatesAsync(ids.AccountId))));
    }

    [Fact]
    public async Task After_a_replay_the_new_cookie_fails_too()
    {
        (_, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage first = await RefreshAsync(session.Cookie);
        using HttpResponseMessage replay = await RefreshAsync(session.Cookie);

        using HttpResponseMessage withNewCookie = await RefreshAsync(SetCookieHeader.RefreshCookieOf(first)!.NameAndValue);

        await ProblemDetailsAssertions.AssertProblemAsync(withNewCookie, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task A_replay_leaves_the_accounts_other_sessions_alone()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        SignedInSession first = await fixture.SignInAsync(email, password);
        SignedInSession second = await fixture.SignInAsync(email, password);
        using HttpResponseMessage refreshed = await RefreshAsync(first.Cookie);
        using HttpResponseMessage replay = await RefreshAsync(first.Cookie);

        using HttpResponseMessage other = await RefreshAsync(second.Cookie);

        Assert.Equal((HttpStatusCode.OK, 1), (other.StatusCode, (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }

    [Fact]
    public async Task Two_refreshes_at_once_with_the_same_cookie_let_exactly_one_succeed_and_end_the_session()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(RefreshAsync(session.Cookie), RefreshAsync(session.Cookie));

        Assert.Equal(
            ("200,401", ""),
            (string.Join(',', responses.Select(response => (int)response.StatusCode).Order()), string.Join(',', await fixture.SessionRowsAsync(ids.AccountId))));
    }

    [Fact]
    public async Task Signing_in_again_with_the_old_cookie_ends_the_old_session_and_its_cookie_stops_refreshing()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        SignedInSession first = await fixture.SignInAsync(email, password);

        using HttpResponseMessage second = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", cookie: first.Cookie, body: new { email, password });

        using HttpResponseMessage refresh = await RefreshAsync(first.Cookie);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized, 1), (second.StatusCode, refresh.StatusCode, (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }

    [Fact]
    public async Task Signing_in_with_another_accounts_cookie_keeps_that_session_and_starts_a_new_one()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        (AccountSeeder.Ids otherIds, SignedInSession other) = await SignedInAsync();

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", cookie: other.Cookie, body: new { email, password });

        Assert.Equal(
            (HttpStatusCode.OK, 1, 1),
            (response.StatusCode, (await fixture.SessionRowsAsync(otherIds.AccountId)).Length, (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }

    [Fact]
    public async Task Signing_in_with_an_unknown_cookie_deletes_nothing_and_starts_a_new_session()
    {
        string email = UniqueEmail();
        string password = ValidPassword();
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        (AccountSeeder.Ids otherIds, _) = await SignedInAsync();

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", cookie: $"{SetCookieHeader.RefreshCookieName}={new string('A', 43)}", body: new { email, password });

        Assert.Equal(
            (HttpStatusCode.OK, 1, 1),
            (response.StatusCode, (await fixture.SessionRowsAsync(otherIds.AccountId)).Length, (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }

    [Fact]
    public async Task A_failed_sign_in_with_the_old_cookie_leaves_its_session_alone()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();
        string email = await fixture.ScalarAsync($"SELECT normalized_email AS \"Value\" FROM app.identity_users WHERE account_id = {ids.AccountId}");

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", cookie: session.Cookie, body: new { email, password = ValidPassword() });

        Assert.Equal((HttpStatusCode.Unauthorized, 1), (response.StatusCode, (await fixture.SessionRowsAsync(ids.AccountId)).Length));
    }
}
