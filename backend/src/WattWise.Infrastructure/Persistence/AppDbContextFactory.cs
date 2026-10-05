using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WattWise.Infrastructure.Persistence;

/// <summary>Used by the EF Core tools (dotnet ef) only.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Settings come from Database__* environment variables only. Without them (no `op run`)
        // the connection string is partial, which is fine for `migrations add`: it never connects.
        // Commands that connect must run through `op run --env-file backend/src/WattWise.Cli/.env.development`.
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        DatabaseSettings settings = DatabaseSettings.Read(configuration);

        // Npgsql refuses a blank host even when nothing connects, so use a placeholder that is never used.
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            settings.Host = "design-time-only";
        }

        string connectionString = settings.ToConnectionString();

        DbContextOptionsBuilder<AppDbContext> builder = new();
        AppDbContextOptions.Configure(builder, connectionString);
        return new AppDbContext(builder.Options);
    }
}
