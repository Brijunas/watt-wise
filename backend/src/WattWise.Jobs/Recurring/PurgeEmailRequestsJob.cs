using Mediator;

using WattWise.Application.Email;

namespace WattWise.Jobs.Recurring;

/// <summary>Deletes old finished outbox rows once a day.</summary>
public sealed class PurgeEmailRequestsJob(ISender sender)
{
    public const string RecurringJobId = "purge-email-requests";

    /// <summary>Daily at 03:00 UTC.</summary>
    public const string Cron = "0 3 * * *";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await sender.Send(new PurgeEmailRequestsCommand(), cancellationToken);
    }
}
