using System.Reflection;

using Hangfire;
using Hangfire.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class RecurringEmailJobsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_send_job_is_registered_every_five_seconds_in_utc()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();

        RecurringJobDto job = RecurringJobOf<SendEmailsJob>(factory);

        Assert.Equal(("*/5 * * * * *", "UTC"), (job.Cron, job.TimeZoneId));
    }

    [Fact]
    public async Task The_purge_job_is_registered_daily_at_three_in_utc()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();

        RecurringJobDto job = RecurringJobOf<PurgeEmailRequestsJob>(factory);

        Assert.Equal(("0 3 * * *", "UTC"), (job.Cron, job.TimeZoneId));
    }

    // The scheduler polls on this interval (default 15 s) and would run a 5-second job at most every 15 (jobs.md).
    [Fact]
    public async Task The_hangfire_server_polls_its_schedule_every_five_seconds()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();
        BackgroundJobServerHostedService hosted = factory.Services.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>().Single();
        FieldInfo options = typeof(BackgroundJobServerHostedService).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(TimeSpan.FromSeconds(5), ((BackgroundJobServerOptions)options.GetValue(hosted)!).SchedulePollingInterval);
    }

    private static RecurringJobDto RecurringJobOf<TJob>(TestHostFactory<Program> factory)
    {
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();
        using IStorageConnection connection = storage.GetConnection();
        return connection.GetRecurringJobs().Single(job => job.Job?.Type == typeof(TJob));
    }
}
