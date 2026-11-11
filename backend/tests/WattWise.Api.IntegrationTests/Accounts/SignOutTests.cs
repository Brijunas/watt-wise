using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class SignOutTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(AccountSeeder.Ids Ids, SignedInSession Session)> SignedInAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        return (ids, await fixture.SignInAsync(email, password));
    }

    private Task<HttpResponseMessage> SignOutAsync(string? cookie) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-out", cookie: cookie);

    [Fact]
    public async Task Signing_out_answers_204_and_deletes_the_session_with_its_tokens()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();

        using HttpResponseMessage response = await SignOutAsync(session.Cookie);

        Assert.Equal(
            (HttpStatusCode.NoContent, "", ""),
            (response.StatusCode, string.Join(',', await fixture.SessionRowsAsync(ids.AccountId)), string.Join(',', await fixture.RefreshTokenStatesAsync(ids.AccountId))));
    }

    [Fact]
    public async Task After_signing_out_the_cookie_no_longer_refreshes()
    {
        (_, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage signedOut = await SignOutAsync(session.Cookie);

        using HttpResponseMessage response = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: session.Cookie);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task Signing_out_with_a_cookie_that_was_already_exchanged_still_ends_the_session()
    {
        (AccountSeeder.Ids ids, SignedInSession session) = await SignedInAsync();
        using HttpResponseMessage refreshed = await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: session.Cookie);

        using HttpResponseMessage response = await SignOutAsync(session.Cookie);

        Assert.Empty(await fixture.SessionRowsAsync(ids.AccountId));
    }

    [Fact]
    public async Task Signing_out_clears_the_cookie_on_its_path_with_the_same_protections_and_a_past_expiry()
    {
        (_, SignedInSession session) = await SignedInAsync();

        using HttpResponseMessage response = await SignOutAsync(session.Cookie);

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Fact]
    public async Task Signing_out_with_no_cookie_still_sends_the_clearing_cookie()
    {
        using HttpResponseMessage response = await SignOutAsync(null);

        Assert.Equal(SetCookieHeader.Cleared, SetCookieHeader.DescribeRefreshCookieOf(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("__Secure-wattwise-refresh=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Signing_out_with_no_cookie_or_an_unknown_one_still_answers_204(string? cookie)
    {
        using HttpResponseMessage response = await SignOutAsync(cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Signing_out_leaves_the_accounts_other_sessions()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        AccountSeeder.Ids ids = await fixture.CreateAccountAsync(email, password);
        SignedInSession first = await fixture.SignInAsync(email, password);
        await fixture.SignInAsync(email, password);

        using HttpResponseMessage response = await SignOutAsync(first.Cookie);

        Assert.Single(await fixture.SessionRowsAsync(ids.AccountId));
    }
}
