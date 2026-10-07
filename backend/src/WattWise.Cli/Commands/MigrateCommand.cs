using System.CommandLine;
using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WattWise.Infrastructure.Persistence.Schema;

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
        // Disposing the host (on every return path) flushes the trace and metric exporters and
        // closes the Serilog logger, so the last log lines and the migrate span are not lost.
        using IHost host = CliHost.Build(args);

        string? settingsError = CliHost.ValidateSettings(host);
        if (settingsError is not null)
        {
            await Console.Error.WriteLineAsync(settingsError.AsMemory(), cancellationToken);
            return 1;
        }

        CliHost.StartTelemetry(host);

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WattWise.Cli.Migrate");
        using Activity? activity = CliTelemetry.Source.StartActivity("migrate");
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ctrl+C or SIGTERM: not a failure of the migration itself. Each EF Core migration runs in
            // its own transaction, so the ones applied before the cancel stay applied.
            activity?.SetStatus(ActivityStatusCode.Error, "Migration cancelled.");
            logger.LogWarning("Migration cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Migration failed.");
            activity?.AddException(ex);
            logger.LogError(ex, "Migration failed.");
            return 1;
        }
    }
}
