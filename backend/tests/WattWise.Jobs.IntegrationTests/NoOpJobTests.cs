using System.Net;

using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class NoOpJobTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_no_op_job_executes()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
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
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();

        using IStorageConnection connection = storage.GetConnection();
        RecurringJobDto job = Assert.Single(connection.GetRecurringJobs(), j => j.Id == NoOpJob.RecurringJobId);

        Assert.False(string.IsNullOrWhiteSpace(job.Cron));
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

    // Until E4 adds admin access, the local-requests-only filter is the dashboard's only protection.
    [Fact]
    public async Task The_dashboard_rejects_a_non_local_request()
    {
        await using TestHostFactory<Program> factory = new(fixture.Database.ConfigurationFor(DatabaseRole.Jobs));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/hangfire", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
