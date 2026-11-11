using System.Globalization;

namespace WattWise.Api.IntegrationTests.Accounts;

public class RefreshCookieTests(AccountsApiFixture fixture) : IClassFixture<AccountsApiFixture>
{
    private async Task<HttpResponseMessage> SignInAsync()
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        string password = $"Aa1!{Guid.NewGuid():N}";
        await fixture.CreateAccountAsync(email, password);
        return await fixture.SendAsync(HttpMethod.Post, "/api/v1/auth/sign-in", body: new { email, password });
    }

    [Fact]
    public async Task The_cookie_is_httpOnly_Secure_SameSite_Strict_and_limited_to_the_auth_path()
    {
        using HttpResponseMessage response = await SignInAsync();

        SetCookieHeader cookie = SetCookieHeader.RefreshCookieOf(response)!;
        Assert.Equal(
            ("__Secure-wattwise-refresh", "", "", "strict", "/api/v1/auth"),
            (cookie.Name, cookie.Attributes["httponly"], cookie.Attributes["secure"], cookie.Attributes["samesite"], cookie.Attributes["path"]));
    }

    [Fact]
    public async Task The_cookie_expires_7_days_after_the_sign_in()
    {
        using HttpResponseMessage response = await SignInAsync();

        // The fixture's clock reads 2026-11-11T12:00Z until a test moves it, and this class moves nothing.
        Assert.Equal(
            new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
            DateTimeOffset.Parse(SetCookieHeader.RefreshCookieOf(response)!.Attributes["expires"], CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_cookie_value_is_a_43_character_Base64Url_token()
    {
        using HttpResponseMessage response = await SignInAsync();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", SetCookieHeader.RefreshCookieOf(response)!.Value);
    }

    [Fact]
    public async Task The_cookie_is_the_only_one_set()
    {
        using HttpResponseMessage response = await SignInAsync();

        Assert.Single(response.Headers.GetValues("Set-Cookie"));
    }
}
