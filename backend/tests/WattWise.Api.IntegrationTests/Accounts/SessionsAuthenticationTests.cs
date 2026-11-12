using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>
/// A call with no access token is refused by the authentication middleware, not by a handler: only the middleware's
/// challenge carries <c>WWW-Authenticate: Bearer</c>, so the header also proves no handler ran. How the tokens themselves
/// are validated is pinned by <see cref="MeEndpointTests"/> and <see cref="ChangePasswordAuthenticationTests"/>.
/// </summary>
public class SessionsAuthenticationTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> StatusCodeAndChallengeAsync(HttpResponseMessage response)
    {
        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
        return string.Join(", ", response.Headers.WwwAuthenticate.Select(challenge => challenge.ToString()));
    }

    [Fact]
    public async Task Listing_the_sessions_with_no_token_is_a_401_challenging_for_Bearer()
    {
        using HttpResponseMessage response = await SessionsApi.ListAsync(fixture, null);

        Assert.Equal("Bearer", await StatusCodeAndChallengeAsync(response));
    }

    [Fact]
    public async Task Ending_a_session_with_no_token_is_a_401_challenging_for_Bearer()
    {
        using HttpResponseMessage response = await SessionsApi.EndAsync(fixture, null, Guid.NewGuid().ToString());

        Assert.Equal("Bearer", await StatusCodeAndChallengeAsync(response));
    }

    [Fact]
    public async Task Signing_out_everywhere_with_no_token_is_a_401_challenging_for_Bearer()
    {
        using HttpResponseMessage response = await SessionsApi.SignOutEverywhereAsync(fixture, null);

        Assert.Equal("Bearer", await StatusCodeAndChallengeAsync(response));
    }
}
