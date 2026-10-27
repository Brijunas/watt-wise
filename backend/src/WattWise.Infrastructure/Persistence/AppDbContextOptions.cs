using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
                    .MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.Migrations))
            .UseSnakeCaseNamingConvention()
            // AppDbContext ignores ASP.NET Core Identity's user-claims entity on purpose (authorization lives on the account).
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.MappedEntityTypeIgnoredWarning));
    }
}
