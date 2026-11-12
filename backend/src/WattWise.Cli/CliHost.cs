using Mediator;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using WattWise.Application;
using WattWise.Application.Behaviors;
using WattWise.Cli.SmokeAccounts;
using WattWise.Infrastructure;
using WattWise.Infrastructure.Observability;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Cli;

/// <summary>Builds and prepares the host that every CLI command runs in.</summary>
internal static class CliHost
{
    public static IHost Build(string[] args)
    {
        // The host is built per command, so --help works without any configuration.
        // appsettings*.json are copied next to the binary; the working directory can be anywhere.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.AddObservability("wattwise-cli");
        builder.Services.AddInfrastructure();
        builder.Services.AddApplication();
        // Keep the behavior list identical to the Api's Program.cs (the generator needs a literal list at each site).
        builder.Services.AddMediator((MediatorOptions options) =>
        {
            options.Assemblies = [typeof(ApplicationAssembly)];
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
        // Read from the built configuration and validated only when resolved: just the seed command needs them.
        builder.Services.AddOptions<SmokeAccountsSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration));
        builder.Services.AddSingleton<IValidateOptions<SmokeAccountsSettings>, SmokeAccountsSettingsValidator>();
        return builder.Build();
    }

    /// <summary>The validation message when the settings are missing or invalid, otherwise null.</summary>
    public static string? ValidateSettings(IHost host)
    {
        // The host is never started, so ValidateOnStart doesn't run; resolve the settings to fail
        // fast with the list of missing keys.
        try
        {
            _ = host.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            _ = host.Services.GetRequiredService<IOptions<ObservabilitySettings>>().Value;
            return null;
        }
        catch (OptionsValidationException ex)
        {
            return ex.Message;
        }
    }

    public static void StartTelemetry(IHost host)
    {
        // OpenTelemetry's hosted service builds the providers when the host starts. The CLI host is
        // never started, so build them here; otherwise nothing would listen and export.
        _ = host.Services.GetService<TracerProvider>();
        _ = host.Services.GetService<MeterProvider>();
    }
}
