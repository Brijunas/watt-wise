namespace WattWise.Api.Endpoints;

/// <summary>
/// A group of related endpoints. Modules are discovered in the Api assembly and mapped at startup.
/// The builder a module receives is the <see cref="ApiRoutes.V1Prefix"/> group, so modules map relative paths.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder group);
}
