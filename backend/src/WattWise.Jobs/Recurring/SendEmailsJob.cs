using Hangfire;

using Mediator;

using WattWise.Application.Email;

namespace WattWise.Jobs.Recurring;

/// <summary>
/// Sends the due emails of the outbox every 5 seconds. Hangfire's own retry is off because each outbox row keeps its
/// retries; the concurrency filter only saves work, since the outbox's claim is what prevents a double send.
/// </summary>
public sealed class SendEmailsJob(ISender sender)
{
    public const string RecurringJobId = "send-emails";

    /// <summary>Six parts: the first is seconds.</summary>
    public const string Cron = "*/5 * * * * *";

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(5)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await sender.Send(new SendDueEmailsCommand(), cancellationToken);
    }
}
