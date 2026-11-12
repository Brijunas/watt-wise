using System.CommandLine;
using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Cli.SmokeAccounts;

namespace WattWise.Cli.Commands;

/// <summary><c>seed smoke-accounts</c>: keeps the two fixed smoke-test accounts (a user and an admin) in a known state. Development only.</summary>
internal static class SeedCommand
{
    public static Command Create(string[] args)
    {
        Command seed = new("seed", "Seed Development data.");
        Command smokeAccounts = new("smoke-accounts", "Create or correct the smoke-test user and admin accounts from the SmokeAccounts settings. Development only; changes only what differs.");
        smokeAccounts.SetAction((_, cancellationToken) => RunAsync(args, cancellationToken));
        seed.Add(smokeAccounts);
        return seed;
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        // Disposing the host (on every return path) flushes the trace and metric exporters and closes the Serilog logger.
        using IHost host = CliHost.Build(args);

        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await Console.Error.WriteLineAsync("seed smoke-accounts runs only in the Development environment.".AsMemory(), cancellationToken);
            return 1;
        }

        string? settingsError = CliHost.ValidateSettings(host);
        SmokeAccountsSettings? smoke = null;
        if (settingsError is null)
        {
            try
            {
                smoke = host.Services.GetRequiredService<IOptions<SmokeAccountsSettings>>().Value;
            }
            catch (OptionsValidationException ex)
            {
                settingsError = ex.Message;
            }
        }

        if (settingsError is not null || smoke is null)
        {
            await Console.Error.WriteLineAsync((settingsError ?? "SmokeAccounts configuration is missing.").AsMemory(), cancellationToken);
            return 1;
        }

        CliHost.StartTelemetry(host);

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WattWise.Cli.Seed");
        using Activity? activity = CliTelemetry.Source.StartActivity("seed smoke-accounts");
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            (string Label, SeedSmokeAccountCommand Command)[] accounts =
            [
                ("user", new SeedSmokeAccountCommand(smoke.UserEmail, smoke.UserPassword, Admin: false)),
                ("admin", new SeedSmokeAccountCommand(smoke.AdminEmail, smoke.AdminPassword, Admin: true)),
            ];

            // Each account is seeded and reported in turn; a failure stops the run, so nothing after it is touched.
            foreach ((string label, SeedSmokeAccountCommand command) in accounts)
            {
                Result<SeedOutcome> result = await sender.Send(command, cancellationToken);
                if (result.IsFailure)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
                    await Console.Error.WriteLineAsync($"{label}: {result.Error.Code}: {result.Error.Description}".AsMemory(), cancellationToken);
                    return 1;
                }

                await Console.Out.WriteLineAsync($"{label}: {result.Value.ToString().ToLowerInvariant()}".AsMemory(), cancellationToken);
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Seed command cancelled.");
            logger.LogWarning("Seed command cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Seed command failed.");
            // The exception itself is not logged: a database error can carry the email.
            logger.LogError("Seed command failed: {ExceptionType}.", ex.GetType().Name);
            return 1;
        }
    }
}
