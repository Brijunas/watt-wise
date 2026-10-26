using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Identity;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Identity for <see cref="AccountIdentity"/> on <see cref="AppDbContext"/>
    /// with the account rules from security.md and <see cref="EmailLinkTokenProvider"/> for email
    /// confirmation and password reset links. Needs <c>AddInfrastructure()</c>. Only the Api calls it.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock>(SystemClock.Instance);

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

                options.Tokens.EmailConfirmationTokenProvider = EmailLinkTokenProvider.ProviderName;
                options.Tokens.PasswordResetTokenProvider = EmailLinkTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddTokenProvider<EmailLinkTokenProvider>(EmailLinkTokenProvider.ProviderName);

        // Both rules see the NFKC form, the one that is hashed. AddIdentityCore registered Identity's
        // own PasswordValidator for the raw form; drop it so the raw form isn't checked as well.
        services.RemoveAll<IPasswordValidator<AccountIdentity>>();
        services.AddScoped<IPasswordValidator<AccountIdentity>, NormalizingPasswordValidator>();
        services.AddScoped<IPasswordValidator<AccountIdentity>, PasswordMaxLengthValidator>();

        // AddIdentityCore registers its own with TryAdd, so these replace them.
        services.Replace(ServiceDescriptor.Singleton<ILookupNormalizer, EmailLookupNormalizer>());
        services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<AccountIdentity>, NormalizingPasswordHasher>());

        return services;
    }
}
