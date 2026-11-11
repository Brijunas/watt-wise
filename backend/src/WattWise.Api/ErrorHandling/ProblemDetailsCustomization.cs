using Microsoft.AspNetCore.WebUtilities;

namespace WattWise.Api.ErrorHandling;

/// <summary>
/// Applied to every ProblemDetails response. The framework already adds <c>traceId</c> and the
/// RFC 9110 <c>type</c>; this adds the <c>code</c> extension where a response doesn't carry one.
/// </summary>
public static class ProblemDetailsCustomization
{
    private const string LockedType = "https://www.rfc-editor.org/rfc/rfc4918#section-11.3";

    public static void Apply(ProblemDetailsContext context)
    {
        // The framework has no RFC 9110 type and title for 423 (it is WebDAV's), so every 423 gets them here.
        if ((context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode) == StatusCodes.Status423Locked)
        {
            context.ProblemDetails.Type ??= LockedType;
            context.ProblemDetails.Title ??= "Locked";
        }

        if (context.ProblemDetails.Extensions.ContainsKey(ProblemDetailsKeys.Code))
        {
            return;
        }

        int status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Extensions[ProblemDetailsKeys.Code] = "General." + StatusName(status);
    }

    /// <summary>The status's reason phrase without spaces and hyphens, e.g. 404 gives <c>NotFound</c>.</summary>
    private static string StatusName(int status)
    {
        if (status == StatusCodes.Status500InternalServerError)
        {
            return "Unexpected";
        }

        string reason = ReasonPhrases.GetReasonPhrase(status)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return reason.Length == 0 ? "Status" + status : reason;
    }
}
