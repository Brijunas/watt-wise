using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Identity;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Identity for <see cref="AccountIdentity"/> on <see cref="AppDbContext"/>
    /// with the account rules from security.md, and Data Protection with its keys in the database,
    /// encrypted with the certificate in <c>DataProtection:Certificate</c>. Needs
    /// <c>AddInfrastructure()</c>. Only the Api calls it.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        // Read from the built configuration, like DatabaseSettings, so sources added after
        // registration (test overrides) are seen; ValidateOnStart stops the host when it is unusable.
        services.AddOptions<DataProtectionSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataProtectionSettings>, DataProtectionSettingsValidator>();
        services.AddSingleton<DataProtectionCertificate>();

        services.AddIdentityCore<AccountIdentity>(options =>
            {
                options.Password.RequiredLength = 14;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // The user name is the email.

                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<PasswordMaxLengthValidator>();

        // AddIdentityCore registers its own with TryAdd, so these replace them.
        services.Replace(ServiceDescriptor.Singleton<ILookupNormalizer, EmailLookupNormalizer>());
        services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<AccountIdentity>, NormalizingPasswordHasher>());

        services.AddDataProtection()
            .SetApplicationName("wattwise")
            .PersistKeysToDbContext<AppDbContext>();
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>, DataProtectionKeyManagementConfiguration>();

        return services;
    }
}
