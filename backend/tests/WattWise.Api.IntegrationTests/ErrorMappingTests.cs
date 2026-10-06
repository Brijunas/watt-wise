using System.Net;
using System.Text.Json;

using Serilog.Events;

namespace WattWise.Api.IntegrationTests;

public class ErrorMappingTests(ErrorsApiFixture fixture) : IClassFixture<ErrorsApiFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Validation_error_is_400_with_field_errors()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/validation", Token);

        JsonElement body = await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        string?[] messages = body.GetProperty("errors").GetProperty("email")
            .EnumerateArray().Select(message => message.GetString()).ToArray();
        Assert.Contains("'Email' must not be empty.", messages);
    }

    [Fact]
    public async Task Validation_field_keys_are_camel_cased_per_segment_keeping_indexers()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/validation-keys", Token);

        JsonElement body = await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Validation.Failed", Token);
        string[] keys = body.GetProperty("errors").EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(["address.streetName", "email", "ipAddress", "items[0].name"], keys);
    }

    [Fact]
    public async Task Not_found_error_is_404_with_the_handlers_code_and_description()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/not-found", Token);

        JsonElement body = await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.NotFound, "Test.Missing", Token);
        Assert.Equal("The thing does not exist.", body.GetProperty("detail").GetString());
        Assert.Equal("Not Found", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unauthorized_error_is_401_with_the_handlers_code()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/unauthorized", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.Unauthorized, "Test.SignedOut", Token);
    }

    [Fact]
    public async Task Successful_result_is_200_with_the_value()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/ok", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("fine", document.RootElement.GetString());
    }

    [Fact]
    public async Task Unhandled_exception_is_500_without_its_message_and_logged_once_as_an_error()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/exception", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "General.Unexpected", Token);
        Assert.DoesNotContain(
            TestErrorsEndpointModule.ExceptionSecret, await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        IReadOnlyList<LogEvent> errors = fixture.Logs.Snapshot()
            .Where(logEvent => logEvent.Level == LogEventLevel.Error
                && logEvent.Exception is InvalidOperationException { Message: TestErrorsEndpointModule.ExceptionSecret })
            .ToList();
        Assert.Single(errors);
    }

    [Fact]
    public async Task Unhandled_domain_exception_is_500()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/domain", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "General.Unexpected", Token);
        Assert.DoesNotContain("domain-secret-detail-456", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Binding_failure_is_400_problem_details()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/test/errors/binding?number=abc", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "General.BadRequest", Token);
    }

    [Fact]
    public async Task Wrong_method_is_405_problem_details()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync("/api/v1/test/errors/ok", content: null, Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.MethodNotAllowed, "General.MethodNotAllowed", Token);
    }

    // 422 gets an RFC 4918 type and 599 none, so neither carries the RFC 9110 type the helper checks by default.
    [Theory]
    [InlineData(422, "General.UnprocessableEntity", false)]
    [InlineData(599, "General.Status599", false)]
    public async Task Other_statuses_get_a_code_from_their_reason_phrase(int status, string expectedCode, bool expectRfcType)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/test/errors/status/{status}", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, (HttpStatusCode)status, expectedCode, Token, expectRfcType);
    }

    [Fact]
    public async Task Unknown_route_is_404_problem_details()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/nope", Token);

        await ProblemDetailsAssertions.AssertProblemAsync(
            response, HttpStatusCode.NotFound, "General.NotFound", Token);
    }
}
