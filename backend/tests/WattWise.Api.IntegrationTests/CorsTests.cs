using System.Net;

namespace WattWise.Api.IntegrationTests;

public class CorsTests(ErrorsApiFixture fixture) : IClassFixture<ErrorsApiFixture>
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string Url = "/api/v1/test/errors/ok";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preflight_from_an_allowed_origin_is_answered_with_that_origin()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Preflight(ErrorsApiFixture.AllowedOrigin);

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal([ErrorsApiFixture.AllowedOrigin], response.Headers.GetValues(AllowOriginHeader));
    }

    [Fact]
    public async Task Preflight_from_another_origin_gets_no_allow_origin_header()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Preflight("https://evil.example.test");

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Simple_request_from_an_allowed_origin_gets_the_allow_origin_header()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, Url);
        request.Headers.Add("Origin", ErrorsApiFixture.AllowedOrigin);

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([ErrorsApiFixture.AllowedOrigin], response.Headers.GetValues(AllowOriginHeader));
    }

    [Fact]
    public async Task Simple_request_from_another_origin_gets_no_allow_origin_header()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, Url);
        request.Headers.Add("Origin", "https://evil.example.test");

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        HttpRequestMessage request = new(HttpMethod.Options, Url);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }
}
