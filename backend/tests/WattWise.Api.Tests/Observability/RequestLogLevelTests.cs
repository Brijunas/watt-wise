using Microsoft.AspNetCore.Http;

using Serilog.Events;

using WattWise.Api.Observability;

namespace WattWise.Api.Tests.Observability;

public class RequestLogLevelTests
{
    // observability.md "One log line per request": Error when an exception escaped the pipeline, Warning for any 5xx,
    // Information otherwise. 499 and 500 are the boundary pair.
    public static TheoryData<Exception?, int, LogEventLevel> Cases => new()
    {
        { new InvalidOperationException("boom"), 200, LogEventLevel.Error },
        { null, 500, LogEventLevel.Warning },
        { null, 499, LogEventLevel.Information },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_request_line_level_follows_the_exception_and_the_status(Exception? exception, int status, LogEventLevel expected)
    {
        DefaultHttpContext context = new();
        context.Response.StatusCode = status;

        Assert.Equal(expected, RequestLogLevel.For(context, exception));
    }
}
