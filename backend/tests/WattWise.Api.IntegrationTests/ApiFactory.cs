using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Api.IntegrationTests;

/// <summary>
/// Starts the Api host with the given settings layered over its configuration. Optional
/// <paramref name="configureServices"/> runs after the host's own registrations, so tests can add or replace services.
/// </summary>
public sealed class ApiFactory(
    IReadOnlyDictionary<string, string?> settings,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
