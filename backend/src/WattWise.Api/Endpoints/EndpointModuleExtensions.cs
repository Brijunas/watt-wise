using Microsoft.Extensions.DependencyInjection.Extensions;

using WattWise.Api.Authentication;
using WattWise.Api.Cors;
using WattWise.Domain.Accounts;

namespace WattWise.Api.Endpoints;

public static class EndpointModuleExtensions
{
    /// <summary>Registers every concrete <see cref="IEndpointModule"/> and <see cref="IAdminEndpointModule"/> in the Api assembly.</summary>
    public static IServiceCollection AddEndpointModules(this IServiceCollection services)
    {
        AddModules<IEndpointModule>(services);
        AddModules<IAdminEndpointModule>(services);
        services.TryAddSingleton<RefreshTokenCookie>();
        return services;
    }

    /// <summary>
    /// Maps every <see cref="IEndpointModule"/> registered in DI (so tests can add modules too) under
    /// <see cref="ApiRoutes.V1Prefix"/>, with the CORS policy applied to the whole group.
    /// The group requires an authenticated caller; an endpoint opts out with <c>AllowAnonymous()</c>.
    /// Every <see cref="IAdminEndpointModule"/> is mapped in the <see cref="ApiRoutes.AdminPrefix"/> subgroup, which
    /// also requires the <c>admin.access</c> permission.
    /// </summary>
    public static WebApplication MapEndpointModules(this WebApplication app)
    {
        RouteGroupBuilder api = app.MapGroup(ApiRoutes.V1Prefix).RequireCors(CorsPolicy.Name).RequireAuthorization();
        foreach (IEndpointModule module in app.Services.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(api);
        }

        RouteGroupBuilder admin = api.MapGroup(ApiRoutes.AdminPrefix).RequirePermission(PermissionNames.AdminAccess);
        foreach (IAdminEndpointModule module in app.Services.GetServices<IAdminEndpointModule>())
        {
            module.MapEndpoints(admin);
        }

        return app;
    }

    private static void AddModules<TModule>(IServiceCollection services)
    {
        IEnumerable<Type> moduleTypes = typeof(TModule).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(TModule)));

        foreach (Type moduleType in moduleTypes)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(TModule), moduleType));
        }
    }
}
