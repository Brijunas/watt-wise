using System.Net;
using System.Text.Json;

namespace WattWise.Api.IntegrationTests.Accounts;

public class SessionEndTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(SessionCredentials Account, SignedInSession Current, SignedInSession Other)> TwoSessionsAsync()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession current = await fixture.SignInAsync(account.Email, account.Password, SessionsApi.FirefoxOnLinux);
        SignedInSession other = await fixture.SignInAsync(account.Email, account.Password, SessionsApi.ChromeOnWindows);
        return (account, current, other);
    }

    [Fact]
    public async Task Ending_the_other_session_makes_its_refresh_cookie_refused()
    {
        (_, SignedInSession current, SignedInSession other) = await TwoSessionsAsync();
        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(other));

        using HttpResponseMessage refresh = await SessionsApi.RefreshAsync(fixture, other.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(refresh, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task Ending_the_other_session_leaves_the_current_one_refreshing_and_its_cookie_untouched()
    {
        (_, SignedInSession current, SignedInSession other) = await TwoSessionsAsync();

        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(other));
        using HttpResponseMessage refresh = await SessionsApi.RefreshAsync(fixture, current.Cookie);

        Assert.Equal(
            (HttpStatusCode.OK, (string?)null),
            (refresh.StatusCode, SetCookieHeader.DescribeRefreshCookieOf(ended)));
    }

    [Fact]
    public async Task Ending_the_current_session_answers_204_clears_the_cookie_and_the_cookie_is_refused()
    {
        (_, SignedInSession current, _) = await TwoSessionsAsync();

        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(current));
        using HttpResponseMessage refresh = await SessionsApi.RefreshAsync(fixture, current.Cookie);

        Assert.Equal(
            (HttpStatusCode.NoContent, SetCookieHeader.Cleared, HttpStatusCode.Unauthorized),
            (ended.StatusCode, SetCookieHeader.DescribeRefreshCookieOf(ended), refresh.StatusCode));
    }

    [Fact]
    public async Task Ending_the_current_session_leaves_only_the_other_session_stored_with_its_token()
    {
        (SessionCredentials account, SignedInSession current, _) = await TwoSessionsAsync();

        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(current));

        // device label | signed in now | last used now | refresh tokens
        Assert.Equal(["Chrome on Windows 10|true|true|1"], await fixture.SessionRowsAsync(account.Ids.AccountId));
    }

    [Fact]
    public async Task Ending_the_same_session_twice_is_a_404_the_second_time()
    {
        (_, SignedInSession current, SignedInSession other) = await TwoSessionsAsync();
        using HttpResponseMessage first = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(other));

        using HttpResponseMessage second = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(other));

        using JsonDocument problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NotFound, "Accounts.SessionNotFound"),
            (first.StatusCode, second.StatusCode, problem.RootElement.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task After_the_current_session_is_ended_its_access_token_still_lists_the_rest_with_none_current()
    {
        (_, SignedInSession current, SignedInSession other) = await TwoSessionsAsync();
        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, current.AccessToken, SessionsApi.SessionIdOf(current));

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, current.AccessToken);

        using JsonDocument list = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.OK, $"{SessionsApi.SessionIdOf(other)} current=False"),
            (ended.StatusCode, response.StatusCode, string.Join(
                ", ",
                list.RootElement.EnumerateArray().Select(
                    session => $"{session.GetProperty("id").GetString()} current={session.GetProperty("current").GetBoolean()}"))));
    }

    [Fact]
    public async Task Ending_an_unknown_session_is_a_404_Accounts_SessionNotFound()
    {
        (_, SignedInSession current, _) = await TwoSessionsAsync();

        using HttpResponseMessage response = await SessionsApi.EndAsync(fixture, current.AccessToken, Guid.NewGuid().ToString());

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.NotFound, "Accounts.SessionNotFound", Token);
    }

    [Fact]
    public async Task Ending_another_accounts_session_gives_the_same_404_body_as_an_unknown_id()
    {
        (_, SignedInSession mine, _) = await TwoSessionsAsync();
        SessionCredentials theirs = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession foreign = await fixture.SignInAsync(theirs.Email, theirs.Password);

        using HttpResponseMessage foreignResponse = await SessionsApi.EndAsync(fixture, mine.AccessToken, SessionsApi.SessionIdOf(foreign));
        using HttpResponseMessage unknownResponse = await SessionsApi.EndAsync(fixture, mine.AccessToken, Guid.NewGuid().ToString());

        Assert.Equal(
            (HttpStatusCode.NotFound, await AuthProblems.BodyWithoutTraceIdAsync(unknownResponse)),
            (foreignResponse.StatusCode, await AuthProblems.BodyWithoutTraceIdAsync(foreignResponse)));
    }

    [Fact]
    public async Task Another_accounts_session_ended_by_id_still_refreshes()
    {
        (_, SignedInSession mine, _) = await TwoSessionsAsync();
        SessionCredentials theirs = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession foreign = await fixture.SignInAsync(theirs.Email, theirs.Password);
        using HttpResponseMessage ended = await SessionsApi.EndAsync(fixture, mine.AccessToken, SessionsApi.SessionIdOf(foreign));

        using HttpResponseMessage refresh = await SessionsApi.RefreshAsync(fixture, foreign.Cookie);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.OK), (ended.StatusCode, refresh.StatusCode));
    }

    [Fact]
    public async Task Failing_to_end_a_session_does_not_clear_the_callers_cookie()
    {
        (_, SignedInSession current, _) = await TwoSessionsAsync();

        using HttpResponseMessage response = await SessionsApi.EndAsync(fixture, current.AccessToken, Guid.NewGuid().ToString());

        Assert.Null(SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Fact]
    public async Task Signing_out_everywhere_answers_204_and_clears_the_cookie()
    {
        (_, SignedInSession current, _) = await TwoSessionsAsync();

        using HttpResponseMessage response = await SessionsApi.SignOutEverywhereAsync(fixture, current.AccessToken);

        Assert.Equal(
            (HttpStatusCode.NoContent, SetCookieHeader.Cleared),
            (response.StatusCode, SetCookieHeader.DescribeRefreshCookieOf(response)));
    }

    [Fact]
    public async Task After_signing_out_everywhere_every_sessions_refresh_is_refused_the_current_one_included()
    {
        (_, SignedInSession current, SignedInSession other) = await TwoSessionsAsync();
        using HttpResponseMessage signedOut = await SessionsApi.SignOutEverywhereAsync(fixture, current.AccessToken);

        using HttpResponseMessage currentRefresh = await SessionsApi.RefreshAsync(fixture, current.Cookie);
        using HttpResponseMessage otherRefresh = await SessionsApi.RefreshAsync(fixture, other.Cookie);

        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized),
            (signedOut.StatusCode, currentRefresh.StatusCode, otherRefresh.StatusCode));
    }

    [Fact]
    public async Task Signing_out_everywhere_leaves_other_accounts_sessions()
    {
        (_, SignedInSession current, _) = await TwoSessionsAsync();
        SessionCredentials theirs = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession foreign = await fixture.SignInAsync(theirs.Email, theirs.Password);
        using HttpResponseMessage signedOut = await SessionsApi.SignOutEverywhereAsync(fixture, current.AccessToken);

        using HttpResponseMessage refresh = await SessionsApi.RefreshAsync(fixture, foreign.Cookie);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK), (signedOut.StatusCode, refresh.StatusCode));
    }
}
