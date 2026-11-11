using System.Net;

using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Api.IntegrationTests.Authentication;

public class AuthenticationRequirementTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_endpoint_of_the_group_without_AllowAnonymous_answers_401_with_no_token()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/protected", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, HttpStatusCode.Unauthorized, "General.Unauthorized", Token);
    }

    [Fact]
    public async Task A_refused_request_never_reaches_the_protected_endpoint()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        int before = fixture.Factory.Services.GetRequiredService<ProbeCalls>().Count;

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/protected", Token);

        Assert.Equal(before, fixture.Factory.Services.GetRequiredService<ProbeCalls>().Count);
    }

    [Theory]
    [InlineData("/api/v1/auth/register", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/auth/resend-confirmation", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/auth/confirm-email", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/auth/sign-in", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/auth/sign-out", HttpStatusCode.NoContent)]
    public async Task The_auth_endpoints_are_reachable_with_no_token(string path, HttpStatusCode expected)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(path, new { }, Token);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task The_about_endpoint_is_reachable_with_no_token()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/about", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
