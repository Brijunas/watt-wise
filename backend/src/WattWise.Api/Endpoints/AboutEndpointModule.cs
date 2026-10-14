using Mediator;

using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Api.ErrorHandling;
using WattWise.Application.About;

namespace WattWise.Api.Endpoints;

public sealed class AboutEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/about", GetAbout)
            .WithName("GetAbout")
            .WithTags("About");
    }

    private static async Task<Results<Ok<AboutResponse>, ProblemHttpResult, ValidationProblem>> GetAbout(ISender sender, CancellationToken cancellationToken)
    {
        return (await sender.Send(new GetAboutQuery(), cancellationToken)).ToHttpResult();
    }
}
