using Microsoft.AspNetCore.Http;

using Serilog.Events;

using WattWise.Api.Observability;

namespace WattWise.Api.Tests.Observability;

public class RequestLogLevelTests
{
    // observability.md "One log line per request": Error when an exception escaped the pipeline, Warning for any 5xx,
    // Debug for a /health request below 500 (path matched exactly, ignoring case), Information otherwise.
    // 499 and 500 are the boundary pair.
    public static TheoryData<Exception?, string, int, LogEventLevel> Cases => new()
    {
        { new InvalidOperationException("boom"), "/about", 200, LogEventLevel.Error },
        { null, "/about", 500, LogEventLevel.Warning },
        { null, "/about", 499, LogEventLevel.Information },
        { null, "/health", 200, LogEventLevel.Debug },
        { null, "/HEALTH", 200, LogEventLevel.Debug },
        { null, "/health", 499, LogEventLevel.Debug },
        { null, "/health", 503, LogEventLevel.Warning },
        { new InvalidOperationException("boom"), "/health", 200, LogEventLevel.Error },
        { null, "/health/x", 200, LogEventLevel.Information },
        { null, "/health/", 200, LogEventLevel.Information },
        { null, "/healthz", 200, LogEventLevel.Information },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_request_line_level_follows_the_exception_the_status_and_the_health_path(Exception? exception, string path, int status, LogEventLevel expected)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.StatusCode = status;

        Assert.Equal(expected, RequestLogLevel.For(context, exception));
    }
}
