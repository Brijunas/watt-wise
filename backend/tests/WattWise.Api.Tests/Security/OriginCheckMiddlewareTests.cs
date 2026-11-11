using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using WattWise.Api.Cors;
using WattWise.Api.Security;

namespace WattWise.Api.Tests.Security;

public class OriginCheckMiddlewareTests
{
    private sealed class RecordingProblemDetails : IProblemDetailsService
    {
        public List<string?> Codes { get; } = [];

        public List<string?> Details { get; } = [];

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Codes.Add(context.ProblemDetails.Extensions["code"]?.ToString());
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Codes.Add(context.ProblemDetails.Extensions["code"]?.ToString());
            Details.Add($"{context.ProblemDetails.Status}:{context.ProblemDetails.Detail}");
            return ValueTask.FromResult(true);
        }
    }

    private const string Allowed = "https://app.example.test";

    private static async Task<(bool Reached, int Status, string Codes)> RunAsync(
        string method, string path, string[]? origins, string? fetchSite = null)
    {
        bool reached = false;
        RecordingProblemDetails problems = new();
        OriginCheckMiddleware middleware = new(
            _ =>
            {
                reached = true;
                return Task.CompletedTask;
            },
            Options.Create(new CorsSettings { AllowedOrigins = [Allowed] }),
            problems);
        DefaultHttpContext http = new();
        http.Request.Method = method;
        http.Request.Path = path;
        if (origins is not null)
        {
            http.Request.Headers["Origin"] = origins;
        }

        if (fetchSite is not null)
        {
            http.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        }

        await middleware.InvokeAsync(http);

        return (reached, http.Response.StatusCode, string.Join(",", problems.Codes));
    }

    [Fact]
    public async Task The_refusal_is_a_403_problem_that_says_the_origin_is_not_allowed()
    {
        RecordingProblemDetails problems = new();
        OriginCheckMiddleware middleware = new(
            _ => Task.CompletedTask, Options.Create(new CorsSettings { AllowedOrigins = [Allowed] }), problems);
        DefaultHttpContext http = new();
        http.Request.Method = "POST";
        http.Request.Path = "/api/v1/things";
        http.Request.Headers["Origin"] = "https://evil.example.test";

        await middleware.InvokeAsync(http);

        Assert.Equal(["403:The request's origin is not allowed."], problems.Details);
    }

    private static readonly (bool, int, string) Refused = (false, 403, "Security.OriginNotAllowed");
    private static readonly (bool, int, string) Passed = (true, 200, "");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("https://evil.example.test")]
    [InlineData("https://app.example.test/")]
    [InlineData("https://APP.example.test")]
    [InlineData("http://app.example.test")]
    public async Task An_unsafe_request_with_an_empty_null_foreign_or_differently_formed_Origin_gets_403_and_stops(string origin)
    {
        Assert.Equal(Refused, await RunAsync("POST", "/api/v1/auth/sign-in", [origin]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_Origin_is_refused_even_with_same_origin_fetch_metadata(string origin)
    {
        Assert.Equal(Refused, await RunAsync("POST", "/api/v1/things", [origin], "same-origin"));
    }

    [Fact]
    public async Task Two_Origin_values_are_refused_even_when_both_are_configured()
    {
        Assert.Equal(Refused, await RunAsync("POST", "/api/v1/things", [Allowed, Allowed]));
    }

    [Fact]
    public async Task Two_Origin_values_are_refused_even_with_same_origin_fetch_metadata()
    {
        Assert.Equal(Refused, await RunAsync("POST", "/api/v1/things", [Allowed, "https://evil.example.test"], "same-origin"));
    }

    [Theory]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task Fetch_metadata_other_than_same_origin_does_not_excuse_a_foreign_Origin(string fetchSite)
    {
        Assert.Equal(Refused, await RunAsync("POST", "/api/v1/things", ["https://evil.example.test"], fetchSite));
    }

    [Fact]
    public async Task Same_origin_fetch_metadata_lets_a_single_foreign_Origin_through()
    {
        Assert.Equal(Passed, await RunAsync("POST", "/api/v1/things", ["https://evil.example.test"], "same-origin"));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("post")]
    public async Task An_unsafe_method_from_a_configured_origin_goes_on(string method)
    {
        Assert.Equal(Passed, await RunAsync(method, "/api/v1/things/1", [Allowed]));
    }

    [Fact]
    public async Task An_unsafe_request_with_no_Origin_goes_on()
    {
        Assert.Equal(Passed, await RunAsync("PATCH", "/api/v1/things/1", null));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task A_safe_method_with_a_foreign_Origin_goes_on(string method)
    {
        Assert.Equal(Passed, await RunAsync(method, "/api/v1/things/1", ["https://evil.example.test"]));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v10/things")]
    [InlineData("/api")]
    public async Task A_path_outside_the_api_prefix_goes_on_with_a_foreign_Origin(string path)
    {
        Assert.Equal(Passed, await RunAsync("POST", path, ["https://evil.example.test"]));
    }

    [Fact]
    public async Task The_api_prefix_is_matched_without_regard_to_case()
    {
        Assert.Equal(Refused, await RunAsync("POST", "/API/V1/things", ["https://evil.example.test"]));
    }
}
