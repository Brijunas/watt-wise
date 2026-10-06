using Microsoft.AspNetCore.WebUtilities;

namespace WattWise.Api.ErrorHandling;

/// <summary>
/// Applied to every ProblemDetails response. The framework already adds <c>traceId</c> and the
/// RFC 9110 <c>type</c>; this adds the <c>code</c> extension where a response doesn't carry one.
/// </summary>
public static class ProblemDetailsCustomization
{
    private const string CodeKey = "code";

    public static void Apply(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Extensions.ContainsKey(CodeKey))
        {
            return;
        }

        int status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Extensions[CodeKey] = "General." + StatusName(status);
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
