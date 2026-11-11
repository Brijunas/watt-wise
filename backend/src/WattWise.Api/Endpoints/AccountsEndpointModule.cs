using Mediator;

using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.ErrorHandling;
using WattWise.Application.Accounts;

namespace WattWise.Api.Endpoints;

/// <summary>The <c>/api/v1/auth</c> route group: sign up and email confirmation.</summary>
public sealed class AccountsEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        RouteGroupBuilder auth = group.MapGroup("/auth").WithTags("Accounts");

        auth.MapPost("/register", Register)
            .WithName("Register");
        auth.MapPost("/resend-confirmation", ResendConfirmation)
            .WithName("ResendConfirmation");
        auth.MapPost("/confirm-email", ConfirmEmail)
            .WithName("ConfirmEmail")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    // The bodies are optional so that a missing one is a validation error like an empty one.
    private static async Task<Results<Accepted, ProblemHttpResult, ValidationProblem>> Register(
        RegisterRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(
            new RegisterAccountCommand(request?.Email, request?.Password, request?.Language), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.Accepted((string?)null));
    }

    private static async Task<Results<Accepted, ProblemHttpResult, ValidationProblem>> ResendConfirmation(
        ResendConfirmationRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(new ResendConfirmationCommand(request?.Email), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.Accepted((string?)null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ConfirmEmail(
        ConfirmEmailRequest? request, ISender sender, CancellationToken cancellationToken)
    {
        Application.Results.Result<Unit> result = await sender.Send(new ConfirmEmailCommand(request?.Id, request?.Token), cancellationToken);
        return result.ToHttpResult(_ => TypedResults.NoContent());
    }
}
