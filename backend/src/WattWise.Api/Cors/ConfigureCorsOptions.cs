using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace WattWise.Api.Cors;

/// <summary>
/// Builds the <see cref="CorsPolicy.Name"/> policy from <see cref="CorsSettings"/>. Reading through
/// options (not builder.Configuration) means configuration sources added later, e.g. by tests, are seen.
/// </summary>
internal sealed class ConfigureCorsOptions(IOptions<CorsSettings> settings) : IConfigureOptions<CorsOptions>
{
    public void Configure(CorsOptions options)
    {
        options.AddPolicy(CorsPolicy.Name, policy => policy
            .WithOrigins(settings.Value.AllowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
    }
}
