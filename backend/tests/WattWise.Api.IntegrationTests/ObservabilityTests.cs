using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Serilog.Events;
using Serilog.Formatting.Compact;

namespace WattWise.Api.IntegrationTests;

public class ObservabilityTests(ErrorsApiFixture fixture) : IClassFixture<ErrorsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_request_produces_one_log_line_and_one_trace()
    {
        const string path = "/api/v1/test/errors/ok";
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LogEvent requestLine = await Poll.UntilAsync(
            () => fixture.Logs.Snapshot().FirstOrDefault(logEvent => ScalarString(logEvent, "RequestPath") == path),
            $"the request log line for {path}",
            Token);
        ActivityTraceId traceId = requestLine.TraceId ?? throw new InvalidOperationException("The log event has no trace id.");
        ActivitySpanId spanId = requestLine.SpanId ?? throw new InvalidOperationException("The log event has no span id.");

        LogEvent only = Assert.Single(EventsOf(traceId));
        Assert.Equal(LogEventLevel.Information, only.Level);
        Assert.Equal("GET", ScalarString(only, "RequestMethod"));
        Assert.Equal(200, ScalarValue(only, "StatusCode"));

        using JsonDocument compact = JsonDocument.Parse(RenderCompact(only));
        Assert.Equal(traceId.ToHexString(), compact.RootElement.GetProperty("@tr").GetString());
        Assert.Equal(spanId.ToHexString(), compact.RootElement.GetProperty("@sp").GetString());

        // The server span ends after the response is sent, so wait for it.
        Activity server = await Poll.UntilAsync(
            () => SpansOf(traceId).FirstOrDefault(span => span.Kind == ActivityKind.Server),
            "the server span of the request",
            Token);
        IReadOnlyList<Activity> spans = SpansOf(traceId);
        Assert.All(spans, span => Assert.Equal(traceId, span.TraceId));
        Assert.Single(spans, span => span.Kind == ActivityKind.Server);
        Assert.Equal(default(ActivitySpanId), server.ParentSpanId);
        Assert.Equal(spanId, server.SpanId);
    }

    [Fact]
    public async Task Unhandled_exception_logs_one_error_and_a_warning_request_line_correlated_with_the_traceId()
    {
        const string path = "/api/v1/test/errors/exception";
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path, Token);

        JsonElement body = await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "General.Unexpected", Token);
        LogEvent requestLine = await Poll.UntilAsync(
            () => fixture.Logs.Snapshot().FirstOrDefault(logEvent => ScalarString(logEvent, "RequestPath") == path),
            $"the request log line for {path}",
            Token);
        ActivityTraceId traceId = requestLine.TraceId ?? throw new InvalidOperationException("The log event has no trace id.");
        ActivitySpanId spanId = requestLine.SpanId ?? throw new InvalidOperationException("The log event has no span id.");

        IReadOnlyList<LogEvent> events = EventsOf(traceId);
        Assert.Equal(2, events.Count);
        LogEvent error = Assert.Single(events, logEvent => logEvent.Level == LogEventLevel.Error);
        Assert.IsType<InvalidOperationException>(error.Exception);
        LogEvent warning = Assert.Single(events, logEvent => logEvent.Level == LogEventLevel.Warning);
        Assert.Equal(500, ScalarValue(warning, "StatusCode"));
        Assert.Equal(path, ScalarString(warning, "RequestPath"));

        string problemTraceId = body.GetProperty("traceId").GetString()!;
        Assert.Contains(traceId.ToHexString(), problemTraceId, StringComparison.Ordinal);
        Assert.Contains(spanId.ToHexString(), problemTraceId, StringComparison.Ordinal);
        Assert.Equal(spanId, warning.SpanId);
    }

    private static string RenderCompact(LogEvent logEvent)
    {
        using StringWriter writer = new();
        new RenderedCompactJsonFormatter().Format(logEvent, writer);
        return writer.ToString();
    }

    private static object? ScalarValue(LogEvent logEvent, string name)
    {
        return logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
    }

    private static string? ScalarString(LogEvent logEvent, string name)
    {
        return ScalarValue(logEvent, name) as string;
    }

    private IReadOnlyList<LogEvent> EventsOf(ActivityTraceId traceId)
    {
        return [.. fixture.Logs.Snapshot().Where(logEvent => logEvent.TraceId == traceId)];
    }

    private IReadOnlyList<Activity> SpansOf(ActivityTraceId traceId)
    {
        return [.. fixture.Spans.Snapshot().Where(span => span.TraceId == traceId)];
    }
}
