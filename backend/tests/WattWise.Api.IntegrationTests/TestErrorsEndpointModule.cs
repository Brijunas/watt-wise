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

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        RouteGroupBuilder errors = group.MapGroup("/test/errors");

        errors.MapGet("/validation", () => Validation());
        errors.MapGet("/validation-keys", () => ValidationKeys());
        errors.MapGet("/not-found", () => NotFound());
        errors.MapGet("/unauthorized", () => Unauthorized());
        errors.MapGet("/ok", () => Ok());
        errors.MapGet("/exception", () => ThrowException());
        errors.MapGet("/domain", () => ThrowDomainException());
        errors.MapGet("/status/{code:int}", (int code) => Results.StatusCode(code));
        errors.MapGet("/binding", (int number) => TypedResults.Ok(number));
    }

    private static IResult Validation()
    {
        Dictionary<string, string[]> fieldErrors = new() { ["Email"] = ["'Email' must not be empty."] };
        return Result<string>.Failure(Error.Validation(fieldErrors)).ToHttpResult();
    }

    private static IResult ValidationKeys()
    {
        Dictionary<string, string[]> fieldErrors = new()
        {
            ["Email"] = ["Invalid."],
            ["IPAddress"] = ["Invalid."],
            ["Address.StreetName"] = ["Invalid."],
            ["Items[0].Name"] = ["Invalid."],
        };
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
