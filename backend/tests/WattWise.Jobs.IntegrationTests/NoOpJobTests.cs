using System.Net;

using Hangfire;
using Hangfire.Storage;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class NoOpJobTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_triggered_no_op_job_succeeds_as_the_recurring_job_no_op()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();
        IRecurringJobManager manager = factory.Services.GetRequiredService<IRecurringJobManager>();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();

        manager.Trigger(NoOpJob.RecurringJobId);
        string jobId = await Poll.UntilAsync(
            () => storage.GetMonitoringApi()
                .SucceededJobs(0, 50)
                .FirstOrDefault(pair => pair.Value.Job?.Type == typeof(NoOpJob)).Key,
            "the no-op job to succeed",
            Token,
            TimeSpan.FromSeconds(30));

        using IStorageConnection connection = storage.GetConnection();
        string? recurringJobId = connection.GetJobParameter(jobId, "RecurringJobId");

        // characterization: backend/src/WattWise.Jobs/Recurring/NoOpJob.cs:9 (Hangfire stores job parameters as JSON, hence the quotes)
        Assert.Equal("\"no-op\"", recurringJobId);
    }

    [Fact]
    public async Task The_no_op_job_is_scheduled_hourly()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();
        using IStorageConnection connection = storage.GetConnection();

        // characterization: backend/src/WattWise.Jobs/Recurring/RecurringJobs.cs:11
        Assert.Equal("0 * * * *", connection.GetRecurringJobs().Single(j => j.Id == NoOpJob.RecurringJobId).Cron);
    }

    [Fact]
    public async Task The_dashboard_is_served()
    {
        await using TestHostFactory<Program> factory = new(
            fixture.Database.ConfigurationFor(DatabaseRole.Jobs),
            services => services.AddSingleton<IStartupFilter, LoopbackClient>());
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/hangfire", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The default local-requests-only filter is the dashboard's only protection; E6 (S6.4) makes it an internal UI on saturn's loopback (jobs.md).
    [Fact]
    public async Task The_dashboard_rejects_a_non_local_request()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/hangfire", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
