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
    /// returns the body.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        JsonElement body = await ReadAndCheckAsync(response, expectedStatus, expectedCode, cancellationToken);

        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        return body;
    }

    /// <summary>
    /// Does everything the overload without a trace id does, and also checks that <c>traceId</c> is a W3C
    /// traceparent carrying <paramref name="expectedTraceId"/> (send a <c>traceparent</c> header to know it) and that
    /// <c>title</c> is the status's default reason phrase from the framework's table.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        ActivityTraceId expectedTraceId,
        CancellationToken cancellationToken)
    {
        JsonElement body = await ReadAndCheckAsync(response, expectedStatus, expectedCode, cancellationToken);

        string? traceId = body.GetProperty("traceId").GetString();
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", traceId);
        Assert.Equal(expectedTraceId.ToHexString(), traceId!.Split('-')[1]);
        Assert.Equal(ReasonPhrases.GetReasonPhrase((int)expectedStatus), body.GetProperty("title").GetString());

        return body;
    }

    private static async Task<JsonElement> ReadAndCheckAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement body = document.RootElement.Clone();

        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        Assert.StartsWith(TypePrefix, body.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());

        return body;
    }
}
