using System.Net;
using System.Text.Json;

namespace WattWise.Api.IntegrationTests;

/// <summary>Asserts the parts of the error contract every ProblemDetails response shares.</summary>
public static class ProblemDetailsAssertions
{
    private const string TypePrefix = "https://tools.ietf.org/html/rfc9110#section-";

    /// <summary>
    /// Checks status, media type, <c>status</c>, <c>type</c>, <c>code</c> and <c>traceId</c>; returns the body.
    /// The framework only adds an RFC 9110 <c>type</c> for statuses it knows, so pass
    /// <paramref name="expectRfcType"/> false for the others.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        CancellationToken cancellationToken,
        bool expectRfcType = true)
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
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        return body;
    }
}
