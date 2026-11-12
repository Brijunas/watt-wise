using System.Globalization;

using WattWise.Application.Results;

namespace WattWise.Api.ErrorHandling;

/// <summary>Writes the <c>Retry-After</c> header for a failed result whose error says when to try again.</summary>
public static class RetryAfterHeader
{
    /// <summary>
    /// Sets <c>Retry-After</c> to the whole seconds of the error's <see cref="Error.RetryAfter"/>, rounded up and at least 1
    /// (the delta-seconds form). A successful result, or an error without <c>RetryAfter</c>, leaves the response alone.
    /// </summary>
    public static void Set<T>(HttpResponse response, Result<T> result)
    {
        if (result.IsSuccess || result.Error.RetryAfter is not { } retryAfter)
        {
            return;
        }

        long seconds = Math.Max(1, (long)Math.Ceiling(retryAfter.TotalSeconds));
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
    }
}
