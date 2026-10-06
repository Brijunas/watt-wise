using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>Maps every <see cref="IEndpointModule"/> registered in DI, so tests can add modules too.</summary>
    public static WebApplication MapEndpointModules(this WebApplication app)
    {
        foreach (IEndpointModule module in app.Services.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
