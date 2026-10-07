using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Jobs.Storage;

public static class HangfireServiceCollectionExtensions
{
    /// <summary>
    /// Registers Hangfire with Postgres storage. The schema is created by <see cref="DatabaseMigrator"/>,
    /// never by Hangfire, because the runtime role cannot create objects.
    /// </summary>
    public static IServiceCollection AddHangfireStorage(this IServiceCollection services)
    {
        // The connection string is built when Hangfire is first resolved, not at registration,
        // so configuration sources added later (e.g. WebApplicationFactory overrides) are seen.
        services.AddHangfire((IServiceProvider serviceProvider, IGlobalConfiguration configuration) =>
        {
            string connectionString = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value.ToConnectionString();
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    (PostgreSqlBootstrapperOptions options) => options.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = DatabaseSchemas.Hangfire,
                        PrepareSchemaIfNecessary = false,
                        EnableLongPolling = true,
                    });
        });
        return services;
    }
}
