using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using NodaTime;

using WattWise.Api.Cors;
using WattWise.Api.Endpoints;

namespace WattWise.Api.Tests.Endpoints;

public class RefreshTokenCookieTests
{
    private const string Admin = "https://admin.example.test";
    private const string Frontend = "https://app.example.test";

    private static RefreshTokenCookie Cookie(string? adminOrigin = Admin) => new(Options.Create(new CorsSettings { AdminOrigin = adminOrigin }));

    private static DefaultHttpContext HttpFrom(string? origin)
    {
        DefaultHttpContext http = new();
        if (origin is not null)
        {
            http.Request.Headers.Origin = origin;
        }

        return http;
    }

    private static string[] SetCookies(DefaultHttpContext http) => [.. http.Response.Headers.SetCookie.Select(value => value!)];

    [Fact]
    public void Writing_sets_the_named_cookie_with_every_protection_and_the_expiry()
    {
        DefaultHttpContext http = HttpFrom(Frontend);

        Cookie().Write(http, "the-token", Instant.FromUtc(2026, 11, 18, 12, 0));

        Assert.Equal(
            ["__Secure-wattwise-refresh=the-token; expires=Wed, 18 Nov 2026 12:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Fact]
    public void Writing_for_the_admin_origin_sets_the_admin_cookie_with_every_protection_and_the_expiry()
    {
        DefaultHttpContext http = HttpFrom(Admin);

        Cookie().Write(http, "the-token", Instant.FromUtc(2026, 11, 18, 12, 0));

        Assert.Equal(
            ["__Secure-wattwise-admin-refresh=the-token; expires=Wed, 18 Nov 2026 12:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Fact]
    public void Deleting_clears_the_cookie_on_the_same_path_with_the_same_protections_and_a_past_expiry()
    {
        DefaultHttpContext http = HttpFrom(Frontend);

        Cookie().Delete(http);

        Assert.Equal(
            ["__Secure-wattwise-refresh=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Fact]
    public void Deleting_for_the_admin_origin_clears_the_admin_cookie_with_the_same_protections()
    {
        DefaultHttpContext http = HttpFrom(Admin);

        Cookie().Delete(http);

        Assert.Equal(
            ["__Secure-wattwise-admin-refresh=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/v1/auth; secure; samesite=strict; httponly"],
            SetCookies(http));
    }

    [Theory]
    [InlineData(Frontend)]
    [InlineData(null)]
    [InlineData("https://ADMIN.example.test")] // the comparison is ordinal
    [InlineData("https://admin.example.test/")] // an exact match, not a prefix
    [InlineData("https://admin.example.test:8443")]
    public void Writing_for_any_other_origin_sets_the_frontend_cookie(string? origin)
    {
        DefaultHttpContext http = HttpFrom(origin);

        Cookie().Write(http, "t", Instant.FromUtc(2026, 11, 18, 12, 0));

        Assert.StartsWith("__Secure-wattwise-refresh=t;", SetCookies(http).Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void With_no_admin_origin_configured_an_empty_origin_does_not_pick_the_admin_cookie(string? adminOrigin)
    {
        DefaultHttpContext http = HttpFrom("");

        Cookie(adminOrigin).Delete(http);

        Assert.StartsWith("__Secure-wattwise-refresh=;", SetCookies(http).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Reading_gives_the_value_of_the_named_cookie()
    {
        DefaultHttpContext http = HttpFrom(Frontend);
        http.Request.Headers.Cookie = "other=1; __Secure-wattwise-refresh=abc; __Secure-wattwise-admin-refresh=xyz";

        Assert.Equal("abc", Cookie().Read(http.Request));
    }

    [Fact]
    public void Reading_from_the_admin_origin_gives_the_admin_cookie_value()
    {
        DefaultHttpContext http = HttpFrom(Admin);
        http.Request.Headers.Cookie = "other=1; __Secure-wattwise-refresh=abc; __Secure-wattwise-admin-refresh=xyz";

        Assert.Equal("xyz", Cookie().Read(http.Request));
    }

    [Theory]
    [InlineData(Admin, "__Secure-wattwise-refresh=abc")]
    [InlineData(Frontend, "__Secure-wattwise-admin-refresh=xyz")]
    [InlineData(null, "__Secure-wattwise-admin-refresh=xyz")]
    public void Reading_ignores_the_cookie_of_the_other_origin(string? origin, string cookies)
    {
        DefaultHttpContext http = HttpFrom(origin);
        http.Request.Headers.Cookie = cookies;

        Assert.Null(Cookie().Read(http.Request));
    }

    [Fact]
    public void Reading_without_the_cookie_gives_null()
    {
        DefaultHttpContext http = HttpFrom(Frontend);
        http.Request.Headers.Cookie = "other=1";

        Assert.Null(Cookie().Read(http.Request));
    }
}
