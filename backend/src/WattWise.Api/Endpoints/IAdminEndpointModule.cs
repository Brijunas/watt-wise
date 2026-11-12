namespace WattWise.Api.Endpoints;

/// <summary>
/// A group of related admin endpoints. Modules are discovered in the Api assembly and mapped at startup.
/// The builder a module receives is the <see cref="ApiRoutes.AdminPrefix"/> group under <see cref="ApiRoutes.V1Prefix"/>,
/// which requires the <c>admin.access</c> permission, so modules map relative paths.
/// </summary>
public interface IAdminEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder group);
}
