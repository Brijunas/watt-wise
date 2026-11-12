using System.CommandLine;
using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Cli.Commands;

/// <summary><c>admin grant &lt;email&gt;</c> and <c>admin revoke &lt;email&gt;</c>: give the Admin role to an existing confirmed account and take it away.</summary>
internal static class AdminCommand
{
    public static Command Create(string[] args)
    {
        Command admin = new("admin", "Manage who holds the Admin role.");
        admin.Add(CreateSubcommand(
            args,
            "grant",
            "Give the Admin role to the confirmed account with the email.",
            (sender, email, token) => sender.Send(new GrantAdminRoleCommand(email), token),
            change => change == RoleChange.Changed ? "Admin role granted." : "Nothing changed: the account already has the Admin role."));
        admin.Add(CreateSubcommand(
            args,
            "revoke",
            "Take the Admin role from the account with the email and end all its sessions.",
            (sender, email, token) => sender.Send(new RevokeAdminRoleCommand(email), token),
            change => change == RoleChange.Changed ? "Admin role revoked and the account's sessions ended." : "Nothing changed: the account does not have the Admin role."));
        return admin;
    }

    private static Command CreateSubcommand(
        string[] args,
        string name,
        string description,
        Func<ISender, string, CancellationToken, ValueTask<Result<RoleChange>>> send,
        Func<RoleChange, string> message)
    {
        Argument<string> emailArgument = new("email") { Description = "The account's email address." };
        Command command = new(name, description) { emailArgument };
        command.SetAction((parseResult, cancellationToken) =>
            RunAsync(args, name, parseResult.GetRequiredValue(emailArgument), send, message, cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(
        string[] args,
        string name,
        string email,
        Func<ISender, string, CancellationToken, ValueTask<Result<RoleChange>>> send,
        Func<RoleChange, string> message,
        CancellationToken cancellationToken)
    {
        // Disposing the host (on every return path) flushes the trace and metric exporters and closes the Serilog logger.
        using IHost host = CliHost.Build(args);

        string? settingsError = CliHost.ValidateSettings(host);
        if (settingsError is not null)
        {
            await Console.Error.WriteLineAsync(settingsError.AsMemory(), cancellationToken);
            return 1;
        }

        CliHost.StartTelemetry(host);

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WattWise.Cli.Admin");
        using Activity? activity = CliTelemetry.Source.StartActivity("admin " + name);
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            Result<RoleChange> result = await send(scope.ServiceProvider.GetRequiredService<ISender>(), email, cancellationToken);
            if (result.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
                await Console.Error.WriteLineAsync($"{result.Error.Code}: {result.Error.Description}".AsMemory(), cancellationToken);
                return 1;
            }

            await Console.Out.WriteLineAsync(message(result.Value).AsMemory(), cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Admin command cancelled.");
            logger.LogWarning("Admin command cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Admin command failed.");
            activity?.AddException(ex);
            logger.LogError(ex, "Admin command failed.");
            return 1;
        }
    }
}
