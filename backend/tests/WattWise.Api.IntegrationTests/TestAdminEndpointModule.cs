using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WattWise.Api.Endpoints;

namespace WattWise.Api.IntegrationTests;

/// <summary>A test-only admin endpoint, <c>GET /api/v1/admin/test</c>, that counts the requests reaching it.</summary>
public sealed class TestAdminEndpointModule : IAdminEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/test", Count);
    }

    private static IResult Count(ProbeCalls calls)
    {
        calls.Increment();
        return Results.Ok();
    }
}
