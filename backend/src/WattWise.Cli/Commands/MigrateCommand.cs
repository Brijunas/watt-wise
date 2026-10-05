using System.CommandLine;

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
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed.");
            return 1;
        }
    }
}
