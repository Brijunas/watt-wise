using System.CommandLine;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Cli.Commands;

internal static class MigrateCommand
{
    public static Command Create(string[] args)
    {
        Command command = new("migrate", "Apply pending EF Core migrations.");
        command.SetAction((_, cancellationToken) => RunAsync(args, cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        // The host is built per command, so --help works without any configuration.
        // appsettings*.json are copied next to the binary; the working directory can be anywhere.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddInfrastructure();
        using IHost host = builder.Build();

        // The host is never started, so ValidateOnStart doesn't run; resolve the settings to fail
        // fast with the list of missing keys.
        try
        {
            _ = host.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WattWise.Cli.Migrate");
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // No command timeout: index builds and table rewrites can run long, and the cli role
            // has no statement_timeout either. Ctrl+C still cancels.
            db.Database.SetCommandTimeout(0);

            string[] pending = [.. await db.Database.GetPendingMigrationsAsync(cancellationToken)];
            if (pending.Length == 0)
            {
                logger.LogInformation("No pending migrations.");
                return 0;
            }

            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Applied: {Migrations}", string.Join(", ", pending));
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed.");
            return 1;
        }
    }
}
