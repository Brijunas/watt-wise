using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>Registers JWT bearer authentication and authorization, and stops the host at start when the Jwt settings are invalid.</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtSettings>().ValidateOnStart();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        return services;
    }
}
