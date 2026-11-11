namespace WattWise.Api.IntegrationTests.Security;

public class AuthCorsTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(string? Origin, string? Credentials)> PreflightAsync(string path, string origin, string method = "POST")
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        return (HeaderOrNull(response, AllowOrigin), HeaderOrNull(response, AllowCredentials));
    }

    private static string? HeaderOrNull(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(',', values) : null;

    [Theory]
    [InlineData("/api/v1/auth/sign-in")]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/v1/auth/sign-out")]
    public async Task A_preflight_to_an_auth_endpoint_from_a_configured_origin_allows_credentials(string path)
    {
        (string? origin, string? credentials) = await PreflightAsync(path, ApiHostFixture.AllowedOrigin);

        Assert.Equal((ApiHostFixture.AllowedOrigin, "true"), (origin, credentials));
    }

    [Theory]
    [InlineData("/api/v1/auth/sign-in")]
    [InlineData("/api/v1/auth/refresh")]
    public async Task A_preflight_to_an_auth_endpoint_from_another_origin_allows_nothing(string path)
    {
        (string? origin, string? credentials) = await PreflightAsync(path, "https://evil.example.test");

        Assert.Equal((null, null), (origin, credentials));
    }

    [Fact]
    public async Task A_preflight_to_an_endpoint_outside_auth_still_allows_no_credentials()
    {
        (string? origin, string? credentials) = await PreflightAsync("/api/v1/me", ApiHostFixture.AllowedOrigin, "GET");

        Assert.Equal((ApiHostFixture.AllowedOrigin, null), (origin, credentials));
    }
}
