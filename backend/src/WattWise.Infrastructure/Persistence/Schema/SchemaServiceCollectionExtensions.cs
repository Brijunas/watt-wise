using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Infrastructure.Persistence.Schema;

public static class SchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DatabaseMigrator"/> and its steps. The registration order is the run order:
    /// EF Core first, so later steps can rely on the app schema being current.
    /// </summary>
    public static IServiceCollection AddSchemaSteps(this IServiceCollection services)
    {
        services.AddScoped<ISchemaStep, EfCoreMigrationsStep>();
        services.AddScoped<ISchemaStep, HangfireStorageStep>();
        services.AddScoped<DatabaseMigrator>();
        return services;
    }
}
