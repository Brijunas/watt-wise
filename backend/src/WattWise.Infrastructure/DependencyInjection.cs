using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Settings are read from the built configuration, not at registration, so sources added
        // later (e.g. WebApplicationFactory overrides in tests) are seen. ValidateOnStart keeps
        // startup failing fast when a key is missing.
        services.AddOptions<DatabaseSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseSettings>, DatabaseSettingsValidator>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            DatabaseSettings settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            AppDbContextOptions.Configure(options, settings.ToConnectionString());
        });
        return services;
    }
}
