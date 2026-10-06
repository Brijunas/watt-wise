using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using OpenTelemetry;
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
            .BindConfiguration(ObservabilitySettings.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<ObservabilitySettings>, ObservabilitySettingsValidator>();

        // The exporter decision reads the bound options when the providers are built, so every
        // configuration source (including test overrides) counts. A blank endpoint turns the
        // exporters off; an invalid one throws OptionsValidationException when resolved.
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
                Uri? otlpEndpoint = OtlpEndpoint(services);
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
            .WithTracing(tracing => tracing.AddSource(AppSources).AddNpgsql())
            .WithMetrics(metrics => metrics.AddMeter(AppMeters).AddNpgsqlInstrumentation());

        // AddOtlpExporter registers services, which a deferred callback cannot do, so the exporters
        // are built by hand. The resource is configured on the providers, so it applies to them.
        builder.Services.ConfigureOpenTelemetryTracerProvider((services, tracing) =>
        {
            Uri? otlpEndpoint = OtlpEndpoint(services);
            if (otlpEndpoint is not null)
            {
                tracing.AddProcessor(new BatchActivityExportProcessor(new OtlpTraceExporter(ExporterOptions(otlpEndpoint))));
            }
        });
        builder.Services.ConfigureOpenTelemetryMeterProvider((services, metrics) =>
        {
            Uri? otlpEndpoint = OtlpEndpoint(services);
            if (otlpEndpoint is not null)
            {
                metrics.AddReader(new PeriodicExportingMetricReader(new OtlpMetricExporter(ExporterOptions(otlpEndpoint))));
            }
        });

        return builder;
    }

    private static Uri? OtlpEndpoint(IServiceProvider services)
    {
        return services.GetRequiredService<IOptions<ObservabilitySettings>>().Value.ValidOtlpEndpoint();
    }

    private static OtlpExporterOptions ExporterOptions(Uri endpoint)
    {
        return new OtlpExporterOptions { Endpoint = endpoint, Protocol = OtlpExportProtocol.Grpc };
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
