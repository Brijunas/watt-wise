using Hangfire;

namespace WattWise.Jobs.Recurring;

/// <summary>Declares every recurring job. Registered at startup; Hangfire upserts them by id.</summary>
public static class RecurringJobs
{
    public static void Register(IRecurringJobManager manager)
    {
        // Hangfire substitutes its own cancellation token for CancellationToken.None when the job runs.
        manager.AddOrUpdate<NoOpJob>(NoOpJob.RecurringJobId, job => job.ExecuteAsync(CancellationToken.None), Cron.Hourly());
    }
}
