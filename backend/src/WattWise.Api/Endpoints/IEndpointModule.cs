namespace WattWise.Api.Endpoints;

/// <summary>A group of related endpoints. Modules are discovered in the Api assembly and mapped at startup.</summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
