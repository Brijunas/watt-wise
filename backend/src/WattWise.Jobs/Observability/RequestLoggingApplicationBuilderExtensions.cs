using Serilog;

namespace WattWise.Jobs.Observability;

public static class RequestLoggingApplicationBuilderExtensions
{
    /// <summary>Logs one structured line per HTTP request, at the level <see cref="RequestLogLevel"/> picks.</summary>
    public static WebApplication UseJobsRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            // AddObservability preserves the static logger, which stays empty, so name the host's
            // logger explicitly; otherwise the request lines would be dropped.
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            // Serilog also passes the elapsed time, which the level doesn't depend on.
            options.GetLevel = (context, _, exception) => RequestLogLevel.For(context, exception);
        });
        return app;
    }
}
