using System.Reflection;

using Hangfire;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Email;
using WattWise.Jobs.Recurring;

namespace WattWise.Jobs.IntegrationTests;

[Collection(JobsHostCollection.Name)]
public class SendEmailsJobFailureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Each outbox row keeps its own retries; Hangfire's would stack on them (jobs.md).
    [Fact]
    public async Task A_failing_send_pass_is_not_retried_by_hangfire()
    {
        await using TestHostFactory<Program> factory = new(
            JobsTestSettings.For(fixture),
            services => services.AddScoped<IEmailOutbox, FailingOutbox>());
        using HttpClient client = factory.CreateClient();
        IBackgroundJobClient jobs = factory.Services.GetRequiredService<IBackgroundJobClient>();
        Hangfire.JobStorage storage = factory.Services.GetRequiredService<Hangfire.JobStorage>();

        string jobId = jobs.Enqueue<SendEmailsJob>(job => job.ExecuteAsync(CancellationToken.None));
        string state = await Poll.UntilAsync(
            () =>
            {
                string? current = storage.GetMonitoringApi().JobDetails(jobId)?.History.FirstOrDefault()?.StateName;
                return current is "Failed" or "Scheduled" or "Succeeded" ? current : null;
            },
            "the failing pass to finish",
            Token,
            TimeSpan.FromSeconds(30));

        Assert.Equal("Failed", state);
    }

    [Fact]
    public void The_send_job_never_runs_twice_at_once_and_waits_at_most_five_seconds_for_the_lock()
    {
        MethodInfo execute = typeof(SendEmailsJob).GetMethod(nameof(SendEmailsJob.ExecuteAsync))!;
        DisableConcurrentExecutionAttribute attribute = execute.GetCustomAttribute<DisableConcurrentExecutionAttribute>()!;
        FieldInfo timeout = typeof(DisableConcurrentExecutionAttribute).GetField("_timeoutSec", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(DisableConcurrentExecutionAttribute).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => field.FieldType == typeof(int));

        Assert.Equal(5, (int)timeout.GetValue(attribute)!);
    }

    [Fact]
    public void The_send_job_turns_hangfires_automatic_retry_off()
    {
        MethodInfo execute = typeof(SendEmailsJob).GetMethod(nameof(SendEmailsJob.ExecuteAsync))!;

        Assert.Equal(0, execute.GetCustomAttribute<AutomaticRetryAttribute>()!.Attempts);
    }

    private sealed class FailingOutbox : IEmailOutbox
    {
        public Task<int> RecoverStuckAsync(Duration stuckAfter, IReadOnlyDictionary<EmailRequestKind, int> maxAttempts, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the pass fails");

        public Task<IReadOnlyList<ReadyEmail>> ClaimAsync(int batchSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> RecordSentAsync(Guid requestId, int attempt, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> RecordRetryAsync(Guid requestId, int attempt, Instant nextAttemptAt, string error, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RecordFailedAsync(Guid requestId, int attempt, string error, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> PurgeAsync(Instant createdBefore, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
