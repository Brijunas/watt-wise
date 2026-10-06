using Serilog;

namespace WattWise.Api.Observability;

public static class RequestLoggingApplicationBuilderExtensions
{
    /// <summary>Logs one structured line per HTTP request, at the level <see cref="RequestLogLevel"/> picks.</summary>
    public static WebApplication UseApiRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            // AddObservability preserves the static logger, which stays empty, so name the host's
            // logger explicitly; otherwise the request lines would be dropped.
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            options.GetLevel = RequestLogLevel.For;
        });
        return app;
    }
}
