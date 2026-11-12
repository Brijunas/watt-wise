using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using NodaTime;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Queries;
using WattWise.Infrastructure.Persistence.Repositories;
using WattWise.Infrastructure.Persistence.Schema;
using WattWise.Infrastructure.Time;

namespace WattWise.Infrastructure;

public static class DependencyInjection
{
    private static int _mailKitTelemetryConfigured;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Settings are read from the built configuration, not at registration, so sources added
        // later (e.g. WebApplicationFactory overrides in tests) are seen. ValidateOnStart keeps
        // startup failing fast when a key is missing.
        services.AddOptions<DatabaseSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseSettings>, DatabaseSettingsValidator>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            DatabaseSettings settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            AppDbContextOptions.Configure(options, settings.ToConnectionString());
        });

        services.AddSingleton<IClock>(SystemClock.Instance);
        // One clock for everything: ASP.NET Core Identity's lockout and the Api's token validation read the time through it.
        services.Replace(ServiceDescriptor.Singleton<TimeProvider, ClockTimeProvider>());
        services.AddSchemaSteps();
        services.AddIdentity();
        services.AddEmail();
        services.AddEmailOutbox();
        services.AddAccounts();
        return services;
    }

    /// <summary>
    /// Registers email sending. The settings are not validated at start, so hosts that never send email
    /// (Jobs, Cli) run without them; the Api, the only host that sends email, requires them at start.
    /// </summary>
    private static IServiceCollection AddEmail(this IServiceCollection services)
    {
        // Read from the built configuration like the database settings above; validated when first resolved.
        services.AddOptions<SmtpSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration));
        services.AddSingleton<IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();

        // MailKit's own spans and metrics (mailkit.net.smtp, mailkit.net.socket); the sources are listed in the observability setup.
        // Configured once per process, however many hosts or providers call this.
        if (Interlocked.Exchange(ref _mailKitTelemetryConfigured, 1) == 0)
        {
            MailKit.Telemetry.SmtpClient.Configure();
        }

        // Singleton: the parsed templates are cached and the sender holds no per-request state (a client is created per send).
        services.AddSingleton<EmailRenderer>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    /// <summary>
    /// Registers the email outbox: the write port, the worker's port and the retry and link settings. Like the
    /// <c>Smtp</c> settings they are read from the built configuration and not validated at start here; the Jobs host,
    /// the only one that sends, adds <c>ValidateOnStart</c>.
    /// </summary>
    private static IServiceCollection AddEmailOutbox(this IServiceCollection services)
    {
        foreach (EmailRequestKind kind in Enum.GetValues<EmailRequestKind>())
        {
            services.AddOptions<EmailRetrySettings>(kind.ToString())
                .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration, kind));
        }

        services.AddSingleton<IValidateOptions<EmailRetrySettings>, EmailRetrySettingsValidator>();

        services.AddOptions<LinkSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration));
        services.AddSingleton<IValidateOptions<LinkSettings>, LinkSettingsValidator>();

        services.AddSingleton<IEmailRetrySettings, ConfiguredEmailRetrySettings>();
        services.AddScoped<IEmailRequests, EmailRequests>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        return services;
    }

    /// <summary>Registers the account ports: registration, confirmation, sign-in and sessions. The Api validates the Jwt settings at start; the other hosts don't need them.</summary>
    private static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddOptions<JwtSettings>()
            .Configure<IConfiguration>((settings, configuration) => settings.ReadFrom(configuration));
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenFactory, RefreshTokenFactory>();
        services.AddSingleton<IDeviceLabels, UserAgentDeviceLabels>();
        services.AddScoped<LockoutCounter>();
        services.AddScoped<IPasswordSignIn, IdentityPasswordSignIn>();
        services.AddScoped<IPasswordReset, IdentityPasswordReset>();
        services.AddScoped<IPasswordChange, IdentityPasswordChange>();
        services.AddScoped<IAccountSessions, AccountSessions>();
        services.AddScoped<IAccountRoles, AccountRoles>();
        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IAccountsQueries, AccountsQueries>();
        services.AddScoped<IExpiredSessions, ExpiredSessions>();

        services.AddSingleton<IEmailAddressNormalizer, IdentityEmailAddressNormalizer>();
        services.AddScoped<IPasswordPolicy, IdentityPasswordPolicy>();
        services.AddScoped<IAccountRegistration, IdentityAccountRegistration>();
        services.AddScoped<IEmailConfirmation, IdentityEmailConfirmation>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="DatabaseMigrator"/> and its steps. The registration order is the run order:
    /// EF Core first, so later steps can rely on the app schema being current.
    /// </summary>
    private static IServiceCollection AddSchemaSteps(this IServiceCollection services)
    {
        services.AddScoped<ISchemaStep, EfCoreMigrationsStep>();
        services.AddScoped<ISchemaStep, HangfireStorageStep>();
        services.AddScoped<DatabaseMigrator>();
        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity for <see cref="AccountIdentity"/> with the account rules from security.md,
    /// and <see cref="EmailLinkTokenProvider"/> for the email confirmation and password reset links.
    /// </summary>
    private static IServiceCollection AddIdentity(this IServiceCollection services)
    {
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

        // Both rules see the NFKC form, the one that is hashed. AddIdentityCore registered
        // ASP.NET Core Identity's own PasswordValidator for the raw form; drop it so the raw form isn't checked as well.
        services.RemoveAll<IPasswordValidator<AccountIdentity>>();
        services.AddScoped<IPasswordValidator<AccountIdentity>, NormalizingPasswordValidator>();
        services.AddScoped<IPasswordValidator<AccountIdentity>, PasswordMaxLengthValidator>();

        // AddIdentityCore registers its own with TryAdd, so these replace them.
        services.Replace(ServiceDescriptor.Singleton<ILookupNormalizer, EmailLookupNormalizer>());
        services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<AccountIdentity>, NormalizingPasswordHasher>());

        return services;
    }
}
