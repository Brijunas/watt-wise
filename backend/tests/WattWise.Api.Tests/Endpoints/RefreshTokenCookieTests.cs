using Microsoft.AspNetCore.Http;

using NodaTime;

using WattWise.Api.Endpoints;

namespace WattWise.Api.Tests.Endpoints;

public class RefreshTokenCookieTests
{
    private static string[] SetCookies(DefaultHttpContext http) => [.. http.Response.Headers.SetCookie.Select(value => value!)];

    [Fact]
    public void Writing_sets_the_named_cookie_with_every_protection_and_the_expiry()
    {
        DefaultHttpContext http = new();

        RefreshTokenCookie.Write(http.Response, "the-token", Instant.FromUtc(2026, 11, 18, 12, 0));

        Assert.Equal(
            ["__Secure-wattwise-refresh=the-token; expires=Wed, 18 Nov 2026 12:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Fact]
    public void Deleting_clears_the_cookie_on_the_same_path_with_the_same_protections_and_a_past_expiry()
    {
        DefaultHttpContext http = new();

        RefreshTokenCookie.Delete(http.Response);

        Assert.Equal(
            ["__Secure-wattwise-refresh=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Fact]
    public void Reading_gives_the_value_of_the_named_cookie()
    {
        DefaultHttpContext http = new();
        http.Request.Headers.Cookie = "other=1; __Secure-wattwise-refresh=abc; another=2";

        Assert.Equal("abc", RefreshTokenCookie.Read(http.Request));
    }

    [Fact]
    public void Reading_without_the_cookie_gives_null()
    {
        DefaultHttpContext http = new();
        http.Request.Headers.Cookie = "other=1";

        Assert.Null(RefreshTokenCookie.Read(http.Request));
    }
}
