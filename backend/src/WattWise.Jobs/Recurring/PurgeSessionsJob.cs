using Mediator;

using WattWise.Application.Accounts;

namespace WattWise.Jobs.Recurring;

/// <summary>Deletes expired refresh tokens and the sessions with none left, once a day.</summary>
public sealed class PurgeSessionsJob(ISender sender)
{
    public const string RecurringJobId = "purge-sessions";

    /// <summary>Daily at 03:30 UTC.</summary>
    public const string Cron = "30 3 * * *";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await sender.Send(new PurgeExpiredSessionsCommand(), cancellationToken);
    }
}
