using System.Net;

namespace WattWise.Api.IntegrationTests;

public class ErrorResponseTests(ApiHostFixture fixture) : IClassFixture<ApiHostFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_validation_error_is_a_400_problem_with_the_validation_code()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/validation", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
    }

    [Theory]
    [InlineData("not-found", 404, "Test.Missing")]
    [InlineData("unauthorized", 401, "Test.SignedOut")]
    public async Task A_not_found_or_unauthorized_error_is_a_problem_with_its_status_and_handler_code(
        string endpoint, int status, string code)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/test/errors/{endpoint}", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, (HttpStatusCode)status, code, Token);
    }

    // "domain" guards error-handling.md ("Domain throws, Application converts"): a DomainException no handler
    // expected is a 500, so a future handler for DomainException must not change that.
    [Theory]
    [InlineData("exception")]
    [InlineData("domain")]
    public async Task An_unhandled_exception_is_a_500_General_Unexpected(string endpoint)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/test/errors/{endpoint}", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "General.Unexpected", Token);
    }

    [Fact]
    public async Task A_500_does_not_show_the_exception_message()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/exception", Token);

        Assert.DoesNotContain(
            TestErrorsEndpointModule.ExceptionSecret, await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GET", "/api/v1/test/errors/binding?number=abc", 400, "General.BadRequest")]
    [InlineData("POST", "/api/v1/test/errors/binding?number=1", 405, "General.MethodNotAllowed")]
    [InlineData("GET", "/nope", 404, "General.NotFound")]
    public async Task Framework_produced_failures_are_problems_with_a_General_code(
        string method, string path, int status, string code)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        using HttpResponseMessage response = await client.SendAsync(request, Token);

        await ProblemDetailsAssertions.AssertProblemAsync(response, (HttpStatusCode)status, code, Token);
    }
}
