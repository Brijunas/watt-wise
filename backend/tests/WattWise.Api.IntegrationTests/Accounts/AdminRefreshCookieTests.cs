using System.Net;

namespace WattWise.Api.IntegrationTests.Accounts;

public class AdminRefreshCookieTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private const string Admin = AccountsApiFixture.AdminOrigin;
    private const string Frontend = AccountsApiFixture.FrontendOrigin;
    private const string FrontendCookie = SetCookieHeader.RefreshCookieName;
    private const string AdminCookie = SetCookieHeader.AdminRefreshCookieName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] SetCookieNames(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? headers)
            ? [.. headers.Select(header => SetCookieHeader.Parse(header).Name)]
            : [];

    private Task<HttpResponseMessage> RefreshAsync(string cookie, string? origin) =>
        fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie: cookie, origin: origin);

    [Fact]
    public async Task Signing_in_from_the_admin_origin_sets_only_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email = account.Email, password = account.Password }, origin: Admin);

        Assert.Equal([AdminCookie], SetCookieNames(response));
    }

    [Fact]
    public async Task Signing_in_from_the_frontend_origin_sets_only_the_frontend_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email = account.Email, password = account.Password }, origin: Frontend);

        Assert.Equal([FrontendCookie], SetCookieNames(response));
    }

    [Fact]
    public async Task An_admin_sign_in_that_carries_the_frontend_cookie_sets_only_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession frontend = await fixture.SignInAsync(account.Email, account.Password, origin: Frontend);

        using HttpResponseMessage adminSignIn = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", cookie: frontend.Cookie,
            body: new { email = account.Email, password = account.Password }, origin: Admin);

        Assert.Equal([AdminCookie], SetCookieNames(adminSignIn));
    }

    [Fact]
    public async Task An_admin_sign_in_that_carries_the_frontend_cookie_leaves_the_frontend_session_refreshable()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession frontend = await fixture.SignInAsync(account.Email, account.Password, origin: Frontend);
        using HttpResponseMessage adminSignIn = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-in", cookie: frontend.Cookie,
            body: new { email = account.Email, password = account.Password }, origin: Admin);

        using HttpResponseMessage refresh = await RefreshAsync(frontend.Cookie, Frontend);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Refreshing_from_the_admin_origin_with_the_admin_cookie_answers_200_and_rotates_only_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession admin = await fixture.SignInAsync(account.Email, account.Password, origin: Admin);

        using HttpResponseMessage response = await RefreshAsync(admin.Cookie, Admin);

        Assert.Equal((HttpStatusCode.OK, AdminCookie), (response.StatusCode, string.Join(',', SetCookieNames(response))));
    }

    [Theory]
    [InlineData(Admin, Frontend)]
    [InlineData(Frontend, Admin)]
    public async Task Refreshing_reads_only_the_cookie_of_its_own_origin(string signedInFrom, string refreshedFrom)
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession session = await fixture.SignInAsync(account.Email, account.Password, origin: signedInFrom);

        using HttpResponseMessage response = await RefreshAsync(session.Cookie, refreshedFrom);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Accounts.InvalidRefreshToken", Token);
    }

    [Fact]
    public async Task A_refused_admin_refresh_clears_the_admin_cookie_and_not_the_frontend_one()
    {
        using HttpResponseMessage response = await RefreshAsync($"{AdminCookie}=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", Admin);

        Assert.Equal(
            (SetCookieHeader.Cleared, (string?)null),
            (SetCookieHeader.DescribeRefreshCookieOf(response, AdminCookie), SetCookieHeader.DescribeRefreshCookieOf(response)));
    }

    [Fact]
    public async Task Signing_out_from_the_admin_origin_clears_the_admin_cookie_and_ends_only_that_session()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession frontend = await fixture.SignInAsync(account.Email, account.Password, origin: Frontend);
        SignedInSession admin = await fixture.SignInAsync(account.Email, account.Password, origin: Admin);

        using HttpResponseMessage signOut = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-out", cookie: $"{frontend.Cookie}; {admin.Cookie}", origin: Admin);
        using HttpResponseMessage frontendRefresh = await RefreshAsync(frontend.Cookie, Frontend);

        Assert.Equal(
            (SetCookieHeader.Cleared, (string?)null, HttpStatusCode.OK),
            (SetCookieHeader.DescribeRefreshCookieOf(signOut, AdminCookie), SetCookieHeader.DescribeRefreshCookieOf(signOut), frontendRefresh.StatusCode));
    }

    [Fact]
    public async Task Ending_the_current_session_from_the_admin_origin_clears_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession admin = await fixture.SignInAsync(account.Email, account.Password, origin: Admin);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Delete, $"/api/v1/auth/sessions/{SessionsApi.SessionIdOf(admin)}", bearer: admin.AccessToken, origin: Admin);

        Assert.Equal(
            (SetCookieHeader.Cleared, (string?)null),
            (SetCookieHeader.DescribeRefreshCookieOf(response, AdminCookie), SetCookieHeader.DescribeRefreshCookieOf(response)));
    }

    [Fact]
    public async Task Signing_out_everywhere_from_the_admin_origin_clears_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession admin = await fixture.SignInAsync(account.Email, account.Password, origin: Admin);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/sign-out-everywhere", bearer: admin.AccessToken, origin: Admin);

        Assert.Equal(
            (SetCookieHeader.Cleared, (string?)null),
            (SetCookieHeader.DescribeRefreshCookieOf(response, AdminCookie), SetCookieHeader.DescribeRefreshCookieOf(response)));
    }

    [Fact]
    public async Task Changing_the_password_from_the_admin_origin_writes_the_admin_cookie()
    {
        SessionCredentials account = await SessionsApi.NewAccountAsync(fixture);
        SignedInSession admin = await fixture.SignInAsync(account.Email, account.Password, origin: Admin);

        using HttpResponseMessage response = await fixture.SendAsync(
            HttpMethod.Post, "/api/v1/auth/change-password", bearer: admin.AccessToken,
            body: new { currentPassword = account.Password, newPassword = $"Aa1!{Guid.NewGuid():N}" }, origin: Admin);

        Assert.Equal((HttpStatusCode.OK, AdminCookie), (response.StatusCode, string.Join(',', SetCookieNames(response))));
    }
}
