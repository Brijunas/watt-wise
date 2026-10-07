using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Testing;

/// <summary>
/// Starts a web host (the Api or Jobs <c>Program</c>) with the given settings layered over its configuration.
/// Optional <paramref name="configureServices"/> runs after the host's own registrations, so tests can add or
/// replace services. When <paramref name="environment"/> is set, the host runs in that environment instead of
/// the default (Development).
/// </summary>
public sealed class TestHostFactory<TEntryPoint>(
    IReadOnlyDictionary<string, string?> settings,
    Action<IServiceCollection>? configureServices = null,
    string? environment = null) : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (environment is not null)
        {
            builder.UseEnvironment(environment);
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
