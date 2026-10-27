using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

namespace WattWise.Api.IntegrationTests;

/// <summary>Asserts the parts of the error contract every ProblemDetails response shares.</summary>
public static class ProblemDetailsAssertions
{
    private const string TypePrefix = "https://tools.ietf.org/html/rfc9110#section-";

    /// <summary>
    /// Checks status, media type, <c>status</c>, <c>type</c>, <c>code</c> and that <c>traceId</c> is present;
    /// returns the body. The framework only adds an RFC 9110 <c>type</c> for statuses it knows, so pass
    /// <paramref name="expectRfcType"/> false for the others.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken,
        bool expectRfcType = true)
    {
        JsonElement body = await ReadAndCheckAsync(response, expectedStatus, expectedCode, cancellationToken, expectRfcType);

        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        return body;
    }

    /// <summary>
    /// Does everything the overload without a trace id does, and also checks that <c>traceId</c> is a W3C
    /// traceparent carrying <paramref name="expectedTraceId"/> (send <see cref="TraceParent"/> to know it) and that
    /// <c>title</c> is the status's default reason phrase from the framework's table. A status without a reason
    /// phrase (599) gets no <c>title</c> from the host, so that check is skipped for it.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        ActivityTraceId expectedTraceId,
        CancellationToken cancellationToken,
        bool expectRfcType = true)
    {
        JsonElement body = await ReadAndCheckAsync(response, expectedStatus, expectedCode, cancellationToken, expectRfcType);

        string? traceId = body.GetProperty("traceId").GetString();
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", traceId);
        Assert.Equal(expectedTraceId.ToHexString(), traceId!.Split('-')[1]);

        string reasonPhrase = ReasonPhrases.GetReasonPhrase((int)expectedStatus);
        if (reasonPhrase.Length > 0)
        {
            Assert.Equal(reasonPhrase, body.GetProperty("title").GetString());
        }

        return body;
    }

    /// <summary>The value of a <c>traceparent</c> header a test sends so it knows the request's trace id.</summary>
    public static string TraceParent(ActivityTraceId traceId) =>
        $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01";

    private static async Task<JsonElement> ReadAndCheckAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken,
        bool expectRfcType)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement body = document.RootElement.Clone();

        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        if (expectRfcType)
        {
            Assert.StartsWith(TypePrefix, body.GetProperty("type").GetString(), StringComparison.Ordinal);
        }

        Assert.Equal(expectedCode, body.GetProperty("code").GetString());

        return body;
    }
}
