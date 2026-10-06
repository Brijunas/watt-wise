using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationAssembly).Assembly, includeInternalTypes: true);
        return services;
    }
}
