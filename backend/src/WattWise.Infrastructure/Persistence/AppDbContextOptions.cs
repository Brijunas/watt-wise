using Microsoft.EntityFrameworkCore;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Persistence;

/// <summary>The single place where AppDbContext is configured; used by DI and the design-time factory.</summary>
public static class AppDbContextOptions
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
    {
        return options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNodaTime()
                    .MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.App))
            .UseSnakeCaseNamingConvention();
    }
}
