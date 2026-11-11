using Hangfire;
using Hangfire.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;
using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

/// <summary>The session purge through the real host, mediator, handler and database.</summary>
[Collection(JobsHostCollection.Name)]
public class PurgeSessionsJobTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_purge_job_is_registered_daily_at_half_past_three_in_utc()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();

        RecurringJobDto job = RecurringJobOf<PurgeSessionsJob>(factory);

        Assert.Equal(("30 3 * * *", "UTC"), (job.Cron, job.TimeZoneId));
    }

    [Fact]
    public async Task The_purge_job_deletes_an_expired_session_and_keeps_a_live_one()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();
        Instant now = SystemClock.Instance.GetCurrentInstant();
        Guid accountId = (await AccountSeeder.CreateAccountAsync(
            factory.Services, $"user-{Guid.NewGuid():N}@example.com", $"Aa1!{Guid.NewGuid():N}")).AccountId;
        Guid expired = await SeedAsync(factory, accountId, now - Duration.FromDays(8), now - Duration.FromDays(1));
        Guid live = await SeedAsync(factory, accountId, now - Duration.FromDays(1), now + Duration.FromDays(6));

        Trigger<PurgeSessionsJob>(factory);
        string expiredRows = await Poll.UntilAsync(
            async () => await RowsAsync(factory, expired) == "gone" ? "gone" : null,
            "the expired session to be purged",
            Token,
            TimeSpan.FromSeconds(30));

        Assert.Equal(("gone", "present"), (expiredRows, await RowsAsync(factory, live)));
    }

    private static RecurringJobDto RecurringJobOf<TJob>(TestHostFactory<Program> factory)
    {
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();
        using IStorageConnection connection = storage.GetConnection();
        return connection.GetRecurringJobs().Single(job => job.Job?.Type == typeof(TJob));
    }

    private static void Trigger<TJob>(TestHostFactory<Program> factory)
    {
        RecurringJobDto job = RecurringJobOf<TJob>(factory);
        factory.Services.GetRequiredService<IRecurringJobManager>().Trigger(job.Id);
    }

    private static async Task<Guid> SeedAsync(TestHostFactory<Program> factory, Guid accountId, Instant signedInAt, Instant expiresAt)
    {
        Session session = Session.Start(accountId, "Firefox on Linux", Guid.NewGuid().ToByteArray(), signedInAt, expiresAt);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(session);
        await db.SaveChangesAsync(Token);
        return session.Id;
    }

    private static async Task<string> RowsAsync(TestHostFactory<Program> factory, Guid sessionId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        bool exists = await db.Sessions.AsNoTracking().AnyAsync(session => session.Id == sessionId, Token);
        return exists ? "present" : "gone";
    }
}
