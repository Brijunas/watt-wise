using Serilog.Events;

namespace WattWise.Api.Observability;

/// <summary>Chooses the level of the one log line written per HTTP request.</summary>
public static class RequestLogLevel
{
    /// <summary>
    /// Error when the request threw, Warning for a 5xx response, otherwise Information, except that a
    /// /health request below 500 is Debug (the container health check calls it every 30 seconds). The
    /// exception handler already logs each exception once at Error, so a 5xx request line is only a Warning.
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
