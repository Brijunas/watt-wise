using System.Text.Json;
using System.Text.Json.Nodes;

namespace WattWise.Api.IntegrationTests.Accounts;

/// <summary>Reads what the account tests compare in ProblemDetails responses.</summary>
internal static class AuthProblems
{
    /// <summary>The body without <c>traceId</c>, which differs per request.</summary>
    public static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonObject body = JsonNode.Parse(json)!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString();
    }

    /// <summary>The names in <c>errors</c>, lowercased and sorted, so the test does not depend on the casing policy.</summary>
    public static async Task<string[]> ErrorFieldsAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name.ToLowerInvariant()).Order()];
    }
}
