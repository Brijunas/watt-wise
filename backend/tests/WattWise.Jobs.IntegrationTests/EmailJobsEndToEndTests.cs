using Hangfire;
using Hangfire.Storage;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.Persistence;
using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

/// <summary>Both outbox jobs through the real host, mediator, handlers and database; only SMTP is replaced.</summary>
[Collection(JobsHostCollection.Name)]
public class EmailJobsEndToEndTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_due_confirmation_request_is_sent_in_the_accounts_language_and_marked_sent()
    {
        CapturingEmailSender sender = new();
        await using TestHostFactory<Program> factory = HostWith(sender);
        using HttpClient client = factory.CreateClient();
        (Guid accountId, string email) = await AddAccountAsync(factory, Language.En);
        Guid id = await SeedAsync(factory, accountId, EmailRequestStatus.Pending, SystemClock.Instance.GetCurrentInstant());

        Trigger<SendEmailsJob>(factory);
        string status = await Poll.UntilAsync(
            async () => await StatusAsync(factory, id) == "sent" ? "sent" : null,
            "the request to be sent",
            Token,
            TimeSpan.FromSeconds(30));

        (EmailMessage message, string messageId) = Assert.Single(sender.Sent);
        Assert.Equal(
            ("sent", typeof(EmailConfirmationEmail), email, Language.En, $"{id}.1"),
            (status, message.GetType(), message.To, message.Language, messageId));
    }

    [Fact]
    public async Task The_purge_job_deletes_a_request_created_more_than_30_days_ago_and_keeps_a_newer_one()
    {
        await using TestHostFactory<Program> factory = HostWith(new CapturingEmailSender());
        using HttpClient client = factory.CreateClient();
        (Guid accountId, _) = await AddAccountAsync(factory, Language.Lt);
        Instant now = SystemClock.Instance.GetCurrentInstant();
        Guid old = await SeedAsync(factory, accountId, EmailRequestStatus.Sent, now - Duration.FromDays(31));
        Guid recent = await SeedAsync(factory, accountId, EmailRequestStatus.Sent, now - Duration.FromDays(29));

        Trigger<PurgeEmailRequestsJob>(factory);
        string oldStatus = await Poll.UntilAsync(
            async () => await StatusAsync(factory, old) == "deleted" ? "deleted" : null,
            "the old request to be purged",
            Token,
            TimeSpan.FromSeconds(30));

        Assert.Equal(("deleted", "sent"), (oldStatus, await StatusAsync(factory, recent)));
    }

    private TestHostFactory<Program> HostWith(CapturingEmailSender sender) => new(
        JobsTestSettings.For(fixture),
        services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
        });

    private static void Trigger<TJob>(TestHostFactory<Program> factory)
    {
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();
        using IStorageConnection connection = storage.GetConnection();
        string id = connection.GetRecurringJobs().Single(job => job.Job?.Type == typeof(TJob)).Id;
        factory.Services.GetRequiredService<IRecurringJobManager>().Trigger(id);
    }

    private static async Task<(Guid AccountId, string Email)> AddAccountAsync(TestHostFactory<Program> factory, Language language)
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = Account.Create(language, SystemClock.Instance.GetCurrentInstant());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        IdentityResult result = await users.CreateAsync(AccountIdentity.Create(account.Id, email));
        Assert.True(result.Succeeded);
        return (account.Id, email);
    }

    private static async Task<Guid> SeedAsync(TestHostFactory<Program> factory, Guid accountId, EmailRequestStatus status, Instant createdAt)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        EmailRequest request = new()
        {
            Id = Guid.CreateVersion7(),
            Kind = EmailRequestKind.EmailConfirmation,
            AccountId = accountId,
            Status = status,
            NextAttemptAt = createdAt,
            CreatedAt = createdAt,
        };
        db.EmailRequests.Add(request);
        await db.SaveChangesAsync(Token);
        return request.Id;
    }

    private static async Task<string> StatusAsync(TestHostFactory<Program> factory, Guid id)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        EmailRequest? row = await db.EmailRequests.AsNoTracking().SingleOrDefaultAsync(request => request.Id == id, Token);
        return row?.Status.ToString().ToLowerInvariant() ?? "deleted";
    }
}
