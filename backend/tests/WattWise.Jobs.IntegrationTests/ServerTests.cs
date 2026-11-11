using Hangfire;
using Hangfire.Storage.Monitoring;

using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class ServerTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task The_hangfire_server_runs_five_workers()
    {
        await using TestHostFactory<Program> factory = new(JobsTestSettings.For(fixture));
        using HttpClient client = factory.CreateClient();
        JobStorage storage = factory.Services.GetRequiredService<JobStorage>();

        // jobs.md "Sizing": WorkerCount is fixed at 5.
        ServerDto server = await Poll.UntilAsync(
            () => storage.GetMonitoringApi().Servers().FirstOrDefault(),
            "the Hangfire server to be listed",
            TestContext.Current.CancellationToken,
            TimeSpan.FromSeconds(30));

        Assert.Equal(5, server.WorkersCount);
    }
}
