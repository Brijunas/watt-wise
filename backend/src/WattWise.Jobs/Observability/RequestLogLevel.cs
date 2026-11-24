using Serilog.Events;

namespace WattWise.Jobs.Observability;

/// <summary>Chooses the level of the one log line written per HTTP request.</summary>
public static class RequestLogLevel
{
    /// <summary>
    /// Error when the request threw, Warning for a 5xx response without an exception, otherwise
    /// Information, except that a /health request below 500 is Debug (the container health check calls
    /// it every 30 seconds). Unlike the Api, Jobs has no exception handler: an exception escaping the
    /// dashboard is logged here at Error and once more by the developer exception page or Kestrel.
    /// </summary>
    public static LogEventLevel For(HttpContext context, Exception? exception)
    {
        if (exception is not null)
        {
            return LogEventLevel.Error;
        }

        if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Warning;
        }

        return context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            ? LogEventLevel.Debug
            : LogEventLevel.Information;
    }
}
