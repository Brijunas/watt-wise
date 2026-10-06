using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace WattWise.Api.Cors;

public static class CorsServiceCollectionExtensions
{
    /// <summary>Registers the validated <see cref="CorsSettings"/> and the <see cref="CorsPolicy.Name"/> policy built from them.</summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        services.AddOptions<CorsSettings>()
            .BindConfiguration(CorsSettings.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        services.AddCors();
        services.AddSingleton<IConfigureOptions<CorsOptions>, ConfigureCorsOptions>();
        return services;
    }
}
