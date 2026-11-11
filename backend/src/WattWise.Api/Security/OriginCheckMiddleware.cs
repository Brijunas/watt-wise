using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using WattWise.Api.Cors;
using WattWise.Api.Endpoints;
using WattWise.Api.ErrorHandling;

namespace WattWise.Api.Security;

/// <summary>
/// Refuses an unsafe request (POST, PUT, PATCH, DELETE) under <see cref="ApiRoutes.V1Prefix"/> whose <c>Origin</c> is not
/// one of <see cref="CorsSettings.AllowedOrigins"/>, with 403 ProblemDetails, before it reaches an endpoint. A request
/// with no <c>Origin</c> (a client that isn't a browser) or with <c>Sec-Fetch-Site: same-origin</c> passes.
/// </summary>
public sealed class OriginCheckMiddleware(RequestDelegate next, IOptions<CorsSettings> settings, IProblemDetailsService problemDetails)
{
    public const string ErrorCode = "Security.OriginNotAllowed";

    private static readonly string[] UnsafeMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsChecked(context.Request) || IsAllowed(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        _ = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = "The request's origin is not allowed.",
                Extensions = { [ProblemDetailsKeys.Code] = ErrorCode },
            },
        });
    }

    private static bool IsChecked(HttpRequest request)
    {
        return request.Path.StartsWithSegments(ApiRoutes.V1Prefix, StringComparison.OrdinalIgnoreCase)
            && UnsafeMethods.Any(method => HttpMethods.Equals(request.Method, method));
    }

    private bool IsAllowed(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out StringValues origin))
        {
            return true;
        }

        // An Origin header that isn't exactly one non-empty value is refused, whatever else the request says.
        if (origin.Count != 1 || string.IsNullOrWhiteSpace(origin[0]))
        {
            return false;
        }

        if (request.Headers["Sec-Fetch-Site"] == "same-origin")
        {
            return true;
        }

        return Array.Exists(settings.Value.AllowedOrigins, allowed => string.Equals(allowed, origin[0], StringComparison.Ordinal));
    }
}
