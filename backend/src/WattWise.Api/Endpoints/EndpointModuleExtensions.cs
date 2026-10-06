using Microsoft.Extensions.DependencyInjection.Extensions;

using WattWise.Api.Cors;

namespace WattWise.Api.Endpoints;

public static class EndpointModuleExtensions
{
    /// <summary>Registers every concrete <see cref="IEndpointModule"/> in the Api assembly.</summary>
    public static IServiceCollection AddEndpointModules(this IServiceCollection services)
    {
        IEnumerable<Type> moduleTypes = typeof(IEndpointModule).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpointModule)));

        foreach (Type moduleType in moduleTypes)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEndpointModule), moduleType));
        }

        return services;
    }

    /// <summary>
    /// Maps every <see cref="IEndpointModule"/> registered in DI (so tests can add modules too) under
    /// <see cref="ApiRoutes.V1Prefix"/>, with the CORS policy applied to the whole group.
    /// </summary>
    public static WebApplication MapEndpointModules(this WebApplication app)
    {
        RouteGroupBuilder api = app.MapGroup(ApiRoutes.V1Prefix).RequireCors(CorsPolicy.Name);
        foreach (IEndpointModule module in app.Services.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(api);
        }

        return app;
    }
}
