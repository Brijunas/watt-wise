using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WattWise.Api.Endpoints;
using WattWise.Api.ErrorHandling;
using WattWise.Application.Results;

namespace WattWise.Api.IntegrationTests;

/// <summary>Test-only endpoints that return each kind of <see cref="Error"/> or throw, without the mediator.</summary>
public sealed class TestErrorsEndpointModule : IEndpointModule
{
    public const string ExceptionSecret = "secret-detail-123";

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/test/errors");

        group.MapGet("/validation", () => Validation());
        group.MapGet("/not-found", () => NotFound());
        group.MapGet("/unauthorized", () => Unauthorized());
        group.MapGet("/ok", () => Ok());
        group.MapGet("/exception", () => ThrowException());
        group.MapGet("/domain", () => ThrowDomainException());
        group.MapGet("/binding", (int number) => TypedResults.Ok(number));
    }

    private static IResult Validation()
    {
        Dictionary<string, string[]> fieldErrors = new() { ["email"] = ["'Email' must not be empty."] };
        return Result<string>.Failure(Error.Validation(fieldErrors)).ToHttpResult();
    }

    private static IResult NotFound()
    {
        return Result<string>.Failure(Error.NotFound("Test.Missing", "The thing does not exist.")).ToHttpResult();
    }

    private static IResult Unauthorized()
    {
        return Result<string>.Failure(Error.Unauthorized("Test.SignedOut", "Sign in first.")).ToHttpResult();
    }

    private static IResult Ok()
    {
        return Result<string>.Success("fine").ToHttpResult();
    }

    private static IResult ThrowException()
    {
        throw new InvalidOperationException(ExceptionSecret);
    }

    private static IResult ThrowDomainException()
    {
        throw new TestDomainException();
    }
}
