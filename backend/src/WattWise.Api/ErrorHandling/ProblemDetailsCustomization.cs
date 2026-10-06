using System.Net;

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

    private static string StatusName(int status)
    {
        if (status == StatusCodes.Status500InternalServerError)
        {
            return "Unexpected";
        }

        return Enum.IsDefined((HttpStatusCode)status) ? ((HttpStatusCode)status).ToString() : "Error";
    }
}
