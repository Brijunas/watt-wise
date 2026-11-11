using System.Net;

using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Api.IntegrationTests.Security;

/// <summary>Wiring only: the variants of the rule itself are pinned in <c>OriginCheckMiddlewareTests</c> (Api.Tests).</summary>
public class OriginCheckTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private const string Foreign = "https://evil.example.test";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProbeCalls Calls => fixture.Factory.Services.GetRequiredService<ProbeCalls>();

    private async Task<HttpResponseMessage> SendAsync(string method, string path, string? origin, string? fetchSite = null)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        return await client.SendAsync(request, Token);
    }

    [Fact]
    public async Task An_unsafe_request_from_a_foreign_origin_gets_403_OriginNotAllowed()
    {
        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/test/probe", Foreign);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Security.OriginNotAllowed", Token);
    }

    [Fact]
    public async Task A_refused_origin_never_reaches_the_endpoint()
    {
        int before = Calls.Count;

        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/test/probe", Foreign);

        Assert.Equal(before, Calls.Count);
    }

    [Theory]
    [InlineData(ApiHostFixture.AllowedOrigin, null)]
    [InlineData(Foreign, "same-origin")]
    [InlineData(null, null)]
    public async Task An_unsafe_request_from_a_configured_origin_with_same_origin_or_with_no_origin_reaches_the_endpoint(string? origin, string? fetchSite)
    {
        int before = Calls.Count;

        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/test/probe", origin, fetchSite);

        Assert.Equal((HttpStatusCode.OK, before + 1), (response.StatusCode, Calls.Count));
    }

    [Fact]
    public async Task A_foreign_origin_is_refused_before_authentication_asks_for_a_token()
    {
        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/test/protected", Foreign);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Security.OriginNotAllowed", Token);
    }

    [Fact]
    public async Task Same_site_fetch_metadata_with_a_foreign_origin_is_refused()
    {
        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/test/probe", Foreign, "same-site");

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Forbidden, "Security.OriginNotAllowed", Token);
    }

    [Fact]
    public async Task A_sign_in_from_the_configured_origin_carries_the_CORS_headers_with_credentials()
    {
        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/auth/sign-in", ApiHostFixture.AllowedOrigin);

        Assert.Equal(
            (ApiHostFixture.AllowedOrigin, "true"),
            (string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin")), string.Join(",", response.Headers.GetValues("Access-Control-Allow-Credentials"))));
    }

    [Fact]
    public async Task A_sign_in_from_a_foreign_origin_is_403_with_no_CORS_headers()
    {
        using HttpResponseMessage response = await SendAsync("POST", "/api/v1/auth/sign-in", Foreign);

        Assert.Equal(
            (HttpStatusCode.Forbidden, false, false),
            (response.StatusCode, response.Headers.Contains("Access-Control-Allow-Origin"), response.Headers.Contains("Access-Control-Allow-Credentials")));
    }
}
