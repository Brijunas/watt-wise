using Serilog.Events;

namespace WattWise.Api.Observability;

/// <summary>Chooses the level of the one log line written per HTTP request.</summary>
public static class RequestLogLevel
{
    /// <summary>
    /// Error when the request threw, Warning for a 5xx response, otherwise Information. The exception
    /// handler already logs each exception once at Error, so a 5xx request line is only a Warning.
    /// </summary>
    public static LogEventLevel For(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null)
        {
            return LogEventLevel.Error;
        }

        return context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
    }
}
