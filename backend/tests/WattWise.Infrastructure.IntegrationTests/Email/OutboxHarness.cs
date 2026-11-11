using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;
using NodaTime.Testing;

using WattWise.Application.Email;
using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.IntegrationTests.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>
/// What the outbox tests share: services for the <c>jobs</c> role (the outbox) and the <c>api</c> role (accounts and link
/// redemption) on one database clone, driven by one <see cref="FakeClock"/>, plus seeding and reading of
/// <c>email_requests</c> rows. Every call runs in a scope of its own, as the Jobs host does.
/// </summary>
internal sealed class OutboxHarness : IAsyncDisposable
{
    public const string FrontendBaseUrl = "https://app.wattwise.test";

    private OutboxHarness(DatabaseFixture fixture, bool withLinks, Action<IServiceCollection>? configureJobs)
    {
        Clock = new FakeClock(Instant.FromUtc(2026, 11, 11, 12, 0));
        Jobs = fixture.BuildServices(
            DatabaseRole.Jobs,
            services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
                configureJobs?.Invoke(services);
                if (!withLinks)
                {
                    return;
                }

                IConfiguration original = (IConfiguration)services.Single(descriptor => descriptor.ServiceType == typeof(IConfiguration)).ImplementationInstance!;
                IConfiguration configured = new ConfigurationBuilder()
                    .AddConfiguration(original)
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Links:FrontendBaseUrl"] = FrontendBaseUrl })
                    .Build();
                services.Replace(ServiceDescriptor.Singleton(configured));
            });
        Api = fixture.BuildAccountServices(Clock);
    }

    /// <summary>The attempts each kind allows, as the retry policy's defaults say.</summary>
    public static IReadOnlyDictionary<EmailRequestKind, int> MaxAttempts { get; } = new Dictionary<EmailRequestKind, int>
    {
        [EmailRequestKind.PasswordReset] = 4,
        [EmailRequestKind.EmailConfirmation] = 6,
        [EmailRequestKind.AccountAlreadyExists] = 3,
    };

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public FakeClock Clock { get; }

    public ServiceProvider Jobs { get; }

    public ServiceProvider Api { get; }

    /// <summary>Services on a clone; <paramref name="withLinks"/> false leaves <c>Links:FrontendBaseUrl</c> unset, to provoke a bug while a link is built; <paramref name="configureJobs"/> adds or replaces services of the outbox's provider.</summary>
    public static OutboxHarness Create(DatabaseFixture fixture, bool withLinks = true, Action<IServiceCollection>? configureJobs = null) =>
        new(fixture, withLinks, configureJobs);

    public async ValueTask DisposeAsync()
    {
        await Jobs.DisposeAsync();
        await Api.DisposeAsync();
    }

    /// <summary>Creates an account and its AccountIdentity (no password); <paramref name="confirmed"/> sets the email confirmed.</summary>
    public async Task<OutboxAccount> AddAccountAsync(Language language = Language.Lt, bool confirmed = false)
    {
        string email = AccountsTestSupport.UniqueEmail();
        await using AsyncServiceScope scope = Api.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = Account.Create(language, Clock.GetCurrentInstant());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);

        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = AccountIdentity.Create(account.Id, email);
        identity.EmailConfirmed = confirmed;
        IdentityResult result = await users.CreateAsync(identity);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return new OutboxAccount(account.Id, identity.Id, identity.Email!);
    }

    /// <summary>Inserts one outbox row; by default it is pending, due now and created now.</summary>
    public async Task<Guid> SeedAsync(
        EmailRequestKind kind,
        Guid? accountId = null,
        string? email = null,
        EmailRequestStatus status = EmailRequestStatus.Pending,
        Instant? createdAt = null,
        Instant? nextAttemptAt = null,
        int attemptCount = 0,
        Instant? lastAttemptAt = null,
        string? lastError = null,
        Guid? id = null,
        Instant? sentAt = null)
    {
        await using AsyncServiceScope scope = Jobs.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Instant now = Clock.GetCurrentInstant();
        EmailRequest request = new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Kind = kind,
            AccountId = accountId,
            Email = email,
            Status = status,
            AttemptCount = attemptCount,
            NextAttemptAt = nextAttemptAt ?? now,
            LastAttemptAt = lastAttemptAt,
            LastError = lastError,
            CreatedAt = createdAt ?? now,
            SentAt = sentAt,
        };
        db.EmailRequests.Add(request);
        await db.SaveChangesAsync(Token);
        return request.Id;
    }

    /// <summary>One <see cref="IEmailOutbox.ClaimAsync"/> call in a scope of its own.</summary>
    public async Task<IReadOnlyList<ReadyEmail>> ClaimAsync(int batchSize = 20)
    {
        await using AsyncServiceScope scope = Jobs.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailOutbox>().ClaimAsync(batchSize, Token);
    }

    /// <summary>Runs <paramref name="action"/> on the outbox in a scope of its own.</summary>
    public async Task<T> WithOutboxAsync<T>(Func<IEmailOutbox, Task<T>> action)
    {
        await using AsyncServiceScope scope = Jobs.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IEmailOutbox>());
    }

    /// <summary>Reads a row back through a new scope; null when it does not exist.</summary>
    public async Task<EmailRequest?> ReadAsync(Guid id)
    {
        await using AsyncServiceScope scope = Jobs.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailRequests.AsNoTracking().SingleOrDefaultAsync(request => request.Id == id, Token);
    }

    /// <summary>The status of a row, or <c>deleted</c> when it does not exist.</summary>
    public async Task<string> StatusOfAsync(Guid id) =>
        (await ReadAsync(id))?.Status.ToString().ToLowerInvariant() ?? "deleted";

    /// <summary>Registers <see cref="FaultyTokenProvider"/> as the link token provider of the outbox's services; set the fault's identity once the account exists.</summary>
    public static Action<IServiceCollection> WithTokenFault(TokenFault fault) => services =>
    {
        services.AddSingleton(fault);
        services.AddScoped<FaultyTokenProvider>();
        services.Configure<IdentityOptions>(
            options => options.Tokens.ProviderMap[EmailLinkTokenProvider.ProviderName] = new TokenProviderDescriptor(typeof(FaultyTokenProvider)));
    };

    /// <summary>The number of link tokens stored for an AccountIdentity.</summary>
    public async Task<long> CountTokensAsync(Guid identityId)
    {
        await using AsyncServiceScope scope = Jobs.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<long>($"""SELECT count(*) AS "Value" FROM app.email_link_tokens WHERE identity_id = {identityId}""")
            .SingleAsync(Token);
    }

    /// <summary>Calls one Record* method (retry: due in 5 minutes, error <c>SmtpCommandException 451</c>; failed: <c>SmtpCommandException 550</c>) and returns whether the row matched.</summary>
    public Task<bool> RecordAsync(OutboxRecordKind kind, Guid id, int attempt) =>
        WithOutboxAsync(outbox => kind switch
        {
            OutboxRecordKind.Sent => outbox.RecordSentAsync(id, attempt, Token),
            OutboxRecordKind.Retry => outbox.RecordRetryAsync(id, attempt, Clock.GetCurrentInstant() + Duration.FromMinutes(5), "SmtpCommandException 451", Token),
            _ => outbox.RecordFailedAsync(id, attempt, "SmtpCommandException 550", Token),
        });

    /// <summary>Every column of a row that a Record* call could change, to compare before and after.</summary>
    public async Task<(string Status, int Attempts, string? Error, Instant? SentAt, Instant NextAttemptAt)> SnapshotAsync(Guid id)
    {
        EmailRequest row = (await ReadAsync(id))!;
        return (row.Status.ToString(), row.AttemptCount, row.LastError, row.SentAt, row.NextAttemptAt);
    }
}
