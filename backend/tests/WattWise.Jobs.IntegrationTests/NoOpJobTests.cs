using System.Net;

using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

public class NoOpJobTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_no_op_job_executes()
    {
        await using JobsFactory factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Hangfire));
        using HttpClient client = factory.CreateClient();
        IRecurringJobManager manager = factory.Services.GetRequiredService<IRecurringJobManager>();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();

        manager.Trigger(NoOpJob.RecurringJobId);

        SucceededJobDto succeeded = await Poll.UntilAsync(
            () => storage.GetMonitoringApi()
                .SucceededJobs(0, 50)
                .Select(pair => pair.Value)
                .FirstOrDefault(job => job.Job?.Type == typeof(NoOpJob)),
            "the no-op job to succeed",
            Token,
            TimeSpan.FromSeconds(30));

        Assert.Equal(typeof(NoOpJob), succeeded.Job.Type);
    }

    [Fact]
    public async Task The_no_op_job_is_registered_as_recurring()
    {
        await using JobsFactory factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Hangfire));
        using HttpClient client = factory.CreateClient();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();

        using IStorageConnection connection = storage.GetConnection();
        RecurringJobDto job = Assert.Single(connection.GetRecurringJobs(), j => j.Id == NoOpJob.RecurringJobId);

        Assert.False(string.IsNullOrWhiteSpace(job.Cron));
    }

    [Fact]
    public async Task The_dashboard_is_served()
    {
        await using JobsFactory factory = new(
            fixture.Database.ConfigurationFor(DatabaseRole.Hangfire),
            services => services.AddSingleton<IStartupFilter, LoopbackClient>());
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/hangfire", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
