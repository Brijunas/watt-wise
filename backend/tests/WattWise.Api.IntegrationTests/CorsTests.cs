namespace WattWise.Api.IntegrationTests;

public class CorsTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ApiHostFixture.AllowedOrigin, ApiHostFixture.AllowedOrigin)]
    [InlineData("https://evil.example.test", null)]
    public async Task Only_the_allowed_origin_is_echoed_in_Access_Control_Allow_Origin(string origin, string? expected)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/about");
        request.Headers.Add("Origin", origin);

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.Equal(expected, HeaderOrNull(response, AllowOrigin));
    }

    [Theory]
    [InlineData("Access-Control-Allow-Methods", "DELETE,GET,PATCH,POST,PUT")]
    [InlineData("Access-Control-Allow-Headers", "Authorization,Content-Type")]
    [InlineData("Access-Control-Max-Age", "600")] // 10 minutes x 60 s
    public async Task A_preflight_is_answered_with_the_documented_methods_headers_and_max_age(string header, string expected)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Preflight();

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        string? actual = HeaderOrNull(response, header);
        Assert.Equal(expected, actual is null ? null : string.Join(',', actual.Split(',').Select(part => part.Trim()).Order()));
    }

    [Fact]
    public async Task A_preflight_asking_for_an_unlisted_header_is_not_allowed()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Preflight();
        request.Headers.Remove("Access-Control-Request-Headers");
        request.Headers.Add("Access-Control-Request-Headers", "X-Other");

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        // ASP.NET Core still answers the preflight; it lists the allowed headers and never echoes the requested one.
        Assert.DoesNotContain("X-Other", HeaderOrNull(response, "Access-Control-Allow-Headers") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_policy_does_not_allow_credentials()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Preflight();

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.Null(HeaderOrNull(response, AllowCredentials));
    }

    [Fact]
    public async Task Health_is_outside_the_cors_policy()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", ApiHostFixture.AllowedOrigin);

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.Null(HeaderOrNull(response, AllowOrigin));
    }

    private static HttpRequestMessage Preflight()
    {
        HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/about");
        request.Headers.Add("Origin", ApiHostFixture.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Content-Type");
        return request;
    }

    private static string? HeaderOrNull(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(',', values) : null;
    }
}
