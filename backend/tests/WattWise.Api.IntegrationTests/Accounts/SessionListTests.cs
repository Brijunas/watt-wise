using System.Net;
using System.Text.Json;

using NodaTime;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Moves the fixture's clock (the second sign-in), so it has a fixture (and clock) of its own.</summary>
public class SessionListTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_sign_ins_list_as_two_sessions_with_their_labels_and_times_and_only_the_callers_is_current()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        Instant first = fixture.Clock.GetCurrentInstant();
        SignedInSession firefox = await fixture.SignInAsync(account.Email, account.Password, SessionsApi.FirefoxOnLinux);
        fixture.Clock.Advance(Duration.FromMinutes(5));
        Instant second = fixture.Clock.GetCurrentInstant();
        SignedInSession chrome = await fixture.SignInAsync(account.Email, account.Password, SessionsApi.ChromeOnWindows);

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, chrome.AccessToken);

        Assert.Equal(
            [
                $"{SessionsApi.SessionIdOf(chrome)}|Chrome on Windows 10|{SessionsApi.Stamp(second)}|{SessionsApi.Stamp(second)}|True",
                $"{SessionsApi.SessionIdOf(firefox)}|Firefox on Linux|{SessionsApi.Stamp(first)}|{SessionsApi.Stamp(first)}|False",
            ],
            await SessionsApi.ListedAsync(response));
    }

    [Fact]
    public async Task The_answer_is_a_200_array_of_objects_with_exactly_the_five_documented_properties()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession session = await fixture.SignInAsync(account.Email, account.Password);

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, session.AccessToken);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            (HttpStatusCode.OK, "id,deviceLabel,signedInAt,lastUsedAt,current"),
            (response.StatusCode, string.Join(',', body.RootElement.EnumerateArray().Single().EnumerateObject().Select(property => property.Name))));
    }

    [Fact]
    public async Task Asking_with_the_other_sessions_token_marks_that_one_current_instead()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession first = await fixture.SignInAsync(account.Email, account.Password);
        SignedInSession second = await fixture.SignInAsync(account.Email, account.Password);

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, first.AccessToken);

        Assert.Equal(
            [SessionsApi.SessionIdOf(first)],
            (await SessionsApi.ListedAsync(response)).Where(line => line.EndsWith("|True", StringComparison.Ordinal)).Select(line => line.Split('|')[0]));
    }

    [Fact]
    public async Task Another_accounts_sessions_are_not_listed()
    {
        SessionCredentials mine = await SessionsApi.NewAccountAsync(fixture);
        SessionCredentials theirs = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession own = await fixture.SignInAsync(mine.Email, mine.Password);
        await fixture.SignInAsync(theirs.Email, theirs.Password);

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, own.AccessToken);

        Assert.Equal(
            [SessionsApi.SessionIdOf(own)],
            (await SessionsApi.ListedAsync(response)).Select(line => line.Split('|')[0]));
    }

    [Fact]
    public async Task A_session_ended_by_refresh_token_reuse_is_no_longer_listed()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession leaked = await fixture.SignInAsync(account.Email, account.Password);
        SignedInSession other = await fixture.SignInAsync(account.Email, account.Password);
        using HttpResponseMessage exchanged = await SessionsApi.RefreshAsync(fixture, leaked.Cookie);
        using HttpResponseMessage reused = await SessionsApi.RefreshAsync(fixture, leaked.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, other.AccessToken);

        Assert.Equal(
            [SessionsApi.SessionIdOf(other)],
            (await SessionsApi.ListedAsync(response)).Select(line => line.Split('|')[0]));
    }
}
