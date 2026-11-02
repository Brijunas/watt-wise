using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Serilog.Events;

namespace WattWise.Api.IntegrationTests;

public class RequestLoggingTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_request_writes_exactly_one_information_line_with_method_path_and_status()
    {
        ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        using HttpResponseMessage response = await GetAsync("/api/v1/about", traceId, ActivitySpanId.CreateRandom());

        await WaitForRequestLineAsync(traceId);

        Assert.Equal(
            ["Information GET /api/v1/about 200"],
            EventsOf(traceId).Select(logEvent =>
                $"{logEvent.Level} {ScalarValue(logEvent, "RequestMethod")} {ScalarValue(logEvent, "RequestPath")} {ScalarValue(logEvent, "StatusCode")}"));
    }

    [Fact]
    public async Task A_request_produces_one_server_span_under_the_callers_span_that_the_log_line_is_correlated_with()
    {
        ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        ActivitySpanId callerSpanId = ActivitySpanId.CreateRandom();
        using HttpResponseMessage response = await GetAsync("/api/v1/about", traceId, callerSpanId);
        LogEvent requestLine = await WaitForRequestLineAsync(traceId);

        // The server span ends after the response is sent, so wait for it.
        await Poll.UntilAsync(
            () => SpansOf(traceId).FirstOrDefault(span => span.Kind == ActivityKind.Server),
            "the server span of the request",
            Token);

        Activity[] servers = [.. SpansOf(traceId).Where(span => span.Kind == ActivityKind.Server)];
        Assert.Equal(
            $"1 {callerSpanId.ToHexString()} {requestLine.SpanId?.ToHexString()}",
            $"{servers.Length} {servers[0].ParentSpanId.ToHexString()} {servers[0].SpanId.ToHexString()}");
    }

    [Fact]
    public async Task An_unhandled_exception_logs_exactly_one_error_event_with_the_exception()
    {
        ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        using HttpResponseMessage response = await GetAsync("/api/v1/test/errors/exception", traceId, ActivitySpanId.CreateRandom());
        await WaitForRequestLineAsync(traceId);

        Assert.Equal(
            [$"{nameof(InvalidOperationException)}: {TestErrorsEndpointModule.ExceptionSecret}"],
            EventsOf(traceId).Where(logEvent => logEvent.Level == LogEventLevel.Error)
                .Select(logEvent => $"{logEvent.Exception?.GetType().Name}: {logEvent.Exception?.Message}"));
    }

    [Fact]
    public async Task The_request_line_of_a_500_is_a_warning_with_status_500_in_the_problem_trace_id()
    {
        ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        using HttpResponseMessage response = await GetAsync("/api/v1/test/errors/exception", traceId, ActivitySpanId.CreateRandom());
        JsonElement body = await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "General.Unexpected", traceId, Token);
        LogEvent requestLine = await WaitForRequestLineAsync(traceId);

        // The problem's traceId is a traceparent: 00-<trace id>-<span id>-<flags>.
        Assert.Equal(
            $"Warning 500 {body.GetProperty("traceId").GetString()!.Split('-')[2]}",
            $"{requestLine.Level} {ScalarValue(requestLine, "StatusCode")} {requestLine.SpanId?.ToHexString()}");
    }

    private async Task<HttpResponseMessage> GetAsync(string path, ActivityTraceId traceId, ActivitySpanId callerSpanId)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{callerSpanId.ToHexString()}-01");

        return await client.SendAsync(request, Token);
    }

    private LogEvent? RequestLineOf(ActivityTraceId traceId)
    {
        return EventsOf(traceId).FirstOrDefault(logEvent => logEvent.Properties.ContainsKey("StatusCode"));
    }

    private Task<LogEvent> WaitForRequestLineAsync(ActivityTraceId traceId)
    {
        return Poll.UntilAsync(() => RequestLineOf(traceId), "the request log line", Token);
    }

    private static object? ScalarValue(LogEvent logEvent, string name)
    {
        return logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value
            : null;
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
