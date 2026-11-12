using System.Security.Claims;

using Mediator;

using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.ErrorHandling;
using WattWise.Application.Accounts;

namespace WattWise.Api.Endpoints;

/// <summary><c>GET /api/v1/me</c>: the signed-in account's own details.</summary>
public sealed class MeEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", GetMe)
            .WithName("GetMe")
            .WithTags("Accounts")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Me>, ProblemHttpResult, ValidationProblem>> GetMe(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        if (!AccessTokenClaims.TryGetAccountId(user, out Guid accountId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }

        return (await sender.Send(new GetMeQuery(accountId), cancellationToken)).ToHttpResult();
    }
}
