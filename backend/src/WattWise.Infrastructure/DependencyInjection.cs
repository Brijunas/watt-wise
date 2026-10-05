using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = DatabaseSettings.FromConfiguration(configuration).ToConnectionString();

        services.AddDbContext<AppDbContext>(options => AppDbContextOptions.Configure(options, connectionString));
        return services;
    }
}
