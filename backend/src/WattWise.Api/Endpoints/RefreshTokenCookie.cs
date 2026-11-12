using Microsoft.Extensions.Options;

using NodaTime;

using WattWise.Api.Cors;

namespace WattWise.Api.Endpoints;

/// <summary>
/// The refresh token's cookie: httpOnly, Secure, <c>SameSite=Strict</c> and limited to the <c>/api/v1/auth</c> endpoints.
/// Deleting it needs the same path, or the browser keeps it. The admin app has its own cookie, so it has its own session:
/// a request whose <c>Origin</c> is <see cref="CorsSettings.AdminOrigin"/> uses <see cref="AdminName"/>, every other
/// request (the frontend, no <c>Origin</c>, a non-browser client) uses <see cref="Name"/>.
/// </summary>
internal sealed class RefreshTokenCookie(IOptions<CorsSettings> settings)
{
    public const string Name = "__Secure-wattwise-refresh";

    public const string AdminName = "__Secure-wattwise-admin-refresh";

    public const string Path = ApiRoutes.V1Prefix + "/auth";

    public string? Read(HttpRequest request)
    {
        return request.Cookies[NameFor(request)];
    }

    public void Write(HttpContext http, string token, Instant expiresAt)
    {
        CookieOptions options = Options();
        options.Expires = expiresAt.ToDateTimeOffset();
        http.Response.Cookies.Append(NameFor(http.Request), token, options);
    }

    public void Delete(HttpContext http)
    {
        http.Response.Cookies.Delete(NameFor(http.Request), Options());
    }

    private string NameFor(HttpRequest request)
    {
        string? adminOrigin = settings.Value.AdminOrigin;
        return !string.IsNullOrEmpty(adminOrigin) && string.Equals(request.Headers.Origin.ToString(), adminOrigin, StringComparison.Ordinal)
            ? AdminName
            : Name;
    }

    private static CookieOptions Options()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        };
    }
}
