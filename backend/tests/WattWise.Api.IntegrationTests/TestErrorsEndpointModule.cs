using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WattWise.Api.Endpoints;
using WattWise.Api.ErrorHandling;
using WattWise.Application.Results;

namespace WattWise.Api.IntegrationTests;

/// <summary>Test-only endpoints that return one <see cref="Error"/> per category, throw, or fail to bind, without the mediator.</summary>
public sealed class TestErrorsEndpointModule : IEndpointModule
{
    public const string ExceptionSecret = "secret-detail-123";
    public const string DomainSecret = "domain-secret-detail-456";

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        RouteGroupBuilder errors = group.MapGroup("/test/errors").AllowAnonymous();

        errors.MapGet("/validation", () => Result<string>
            .Failure(Error.Validation(new Dictionary<string, string[]> { ["Email"] = ["'Email' must not be empty."] }))
            .ToHttpResult());
        errors.MapGet("/not-found", () => Result<string>
            .Failure(Error.NotFound("Test.Missing", "The thing does not exist."))
            .ToHttpResult());
        errors.MapGet("/unauthorized", () => Result<string>
            .Failure(Error.Unauthorized("Test.SignedOut", "Sign in first."))
            .ToHttpResult());
        errors.MapGet("/exception", IResult () => throw new InvalidOperationException(ExceptionSecret));
        errors.MapGet("/domain", IResult () => throw new TestDomainException());
        errors.MapGet("/binding", (int number) => TypedResults.Ok(number));
    }
}
