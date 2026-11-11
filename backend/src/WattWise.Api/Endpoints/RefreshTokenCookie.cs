using NodaTime;

namespace WattWise.Api.Endpoints;

/// <summary>
/// The refresh token's cookie: httpOnly, Secure, <c>SameSite=Strict</c> and limited to the <c>/api/v1/auth</c> endpoints.
/// Deleting it needs the same path, or the browser keeps it.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "__Secure-wattwise-refresh";

    public const string Path = ApiRoutes.V1Prefix + "/auth";

    public static string? Read(HttpRequest request)
    {
        return request.Cookies[Name];
    }

    public static void Write(HttpResponse response, string token, Instant expiresAt)
    {
        CookieOptions options = Options();
        options.Expires = expiresAt.ToDateTimeOffset();
        response.Cookies.Append(Name, token, options);
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name, Options());
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
