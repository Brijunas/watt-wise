using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WattWise.Api.Endpoints;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// Test-only endpoints that count the requests reaching them: <c>/test/probe</c> answers every method and is anonymous,
/// <c>/test/protected</c> (GET and POST) has no <c>AllowAnonymous</c>, so it shows what the group requires by default.
/// </summary>
public sealed class TestProbeEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapMethods("/test/probe", ["GET", "POST", "PUT", "PATCH", "DELETE"], Count).AllowAnonymous();
        group.MapMethods("/test/protected", ["GET", "POST"], Count);
    }

    private static IResult Count(ProbeCalls calls)
    {
        calls.Increment();
        return Results.Ok();
    }
}
