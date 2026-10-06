using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace WattWise.Infrastructure.Observability;

public static class ObservabilityHostApplicationBuilderExtensions
{
    private const string EnvironmentAttribute = "deployment.environment.name";

    private static readonly string[] AppSources = ["WattWise.*", "Microsoft.AspNetCore", "System.Net.Http"];

    private static readonly string[] AppMeters =
    [
        "WattWise.*",
        "System.Runtime",
        "System.Net.Http",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
    ];

    /// <summary>
    /// Replaces the default logging with Serilog (compact JSON on stdout, plus OTLP when an endpoint
    /// is configured) and adds OpenTelemetry tracing and metrics for the named service.
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddOptions<ObservabilitySettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<ObservabilitySettings>, ObservabilitySettingsValidator>();

        // Decided at registration: the exporters are wired into the providers here, so unlike the
        // options above this does not see configuration sources added later. A blank or invalid
        // endpoint turns them off (an invalid one then fails ValidateOnStart).
        Uri? otlpEndpoint = ObservabilitySettings.Read(builder.Configuration).ValidOtlpEndpoint();
        string serviceVersion = ServiceVersion();
        string environment = builder.Environment.EnvironmentName.ToLowerInvariant();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (services, logger) =>
            {
                logger
                    .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new RenderedCompactJsonFormatter());
                if (otlpEndpoint is not null)
                {
                    logger.WriteTo.OpenTelemetry(options =>
                    {
                        options.Endpoint = otlpEndpoint.ToString();
                        options.Protocol = OtlpProtocol.Grpc;
                        options.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.name"] = serviceName,
                            ["service.version"] = serviceVersion,
                            [EnvironmentAttribute] = environment,
                        };
                    });
                }
            },
            // Keeps parallel hosts (tests) from sharing and closing the static Log.Logger.
            preserveStaticLogger: true);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes([new KeyValuePair<string, object>(EnvironmentAttribute, environment)]))
            .WithTracing(tracing =>
            {
                tracing.AddSource(AppSources).AddNpgsql();
                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter(options => ConfigureExporter(options, otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(AppMeters).AddNpgsqlInstrumentation();
                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter(options => ConfigureExporter(options, otlpEndpoint));
                }
            });

        return builder;
    }

    private static void ConfigureExporter(OtlpExporterOptions options, Uri endpoint)
    {
        options.Endpoint = endpoint;
        options.Protocol = OtlpExportProtocol.Grpc;
    }

    private static string ServiceVersion()
    {
        string? version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return "unknown";
        }

        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
