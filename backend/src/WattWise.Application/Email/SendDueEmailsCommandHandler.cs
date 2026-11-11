using System.Globalization;

using Mediator;

using Microsoft.Extensions.Logging;

using NodaTime;

using WattWise.Application.Results;
using WattWise.Domain.Localization;

namespace WattWise.Application.Email;

public sealed partial class SendDueEmailsCommandHandler(
    IEmailOutbox outbox,
    IEmailSender sender,
    IEmailRetrySettings retrySettings,
    IClock clock,
    ILogger<SendDueEmailsCommandHandler> logger)
    : ICommandHandler<SendDueEmailsCommand, Result<SendDueEmailsResponse>>
{
    public const int BatchSize = 20;

    /// <summary>The 2-minute SMTP timeout plus a margin.</summary>
    public static readonly Duration StuckAfter = Duration.FromMinutes(5);

    /// <summary>
    /// No new send starts later than this after the claim: the stuck threshold minus the 2-minute SMTP timeout, so a
    /// send that starts in time ends before recovery would take the row. Skipped rows stay sending and recovery returns them.
    /// </summary>
    public static readonly Duration SendWindow = Duration.FromMinutes(3);

    public async ValueTask<Result<SendDueEmailsResponse>> Handle(SendDueEmailsCommand command, CancellationToken cancellationToken)
    {
        Dictionary<EmailRequestKind, int> maxAttempts = Enum.GetValues<EmailRequestKind>()
            .ToDictionary(kind => kind, kind => retrySettings.For(kind).Attempts);

        await outbox.RecoverStuckAsync(StuckAfter, maxAttempts, cancellationToken);
        Instant claimedAt = clock.GetCurrentInstant();
        IReadOnlyList<ReadyEmail> ready = await outbox.ClaimAsync(BatchSize, cancellationToken);

        int sent = 0;
        int retried = 0;
        int failed = 0;
        foreach (ReadyEmail email in ready)
        {
            if (clock.GetCurrentInstant() - claimedAt > SendWindow)
            {
                break;
            }

            RetryAction action = await SendOneAsync(email, cancellationToken);
            switch (action)
            {
                case RetryAction.Sent:
                    sent++;
                    break;
                case RetryAction.Retry:
                    retried++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        return new SendDueEmailsResponse(sent, retried, failed);
    }

    private async Task<RetryAction> SendOneAsync(ReadyEmail email, CancellationToken cancellationToken)
    {
        SendResult result;
        try
        {
            string localPart = string.Create(CultureInfo.InvariantCulture, $"{email.RequestId}.{email.AttemptCount}");
            result = await sender.SendAsync(email.Message, localPart, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A bug (a broken template, a missing setting): this row fails, the others go on.
            // Only the type name is stored and logged; the message could hold an address.
            LogRecorded(await outbox.RecordFailedAsync(email.RequestId, email.AttemptCount, exception.GetType().Name, cancellationToken), email);
            LogError(email.Kind, email.Message.Language);
            return RetryAction.Failed;
        }

        // Recording is outside the catch above: a database error here must not mark a delivered email failed.
        // It propagates, the row stays sending and the stuck-row recovery handles it.
        RetryDecision decision = EmailRetryPolicy.Decide(
            retrySettings.For(email.Kind), email.AttemptCount, result, clock.GetCurrentInstant());

        switch (decision.Action)
        {
            case RetryAction.Sent:
                LogRecorded(await outbox.RecordSentAsync(email.RequestId, email.AttemptCount, cancellationToken), email);
                break;
            case RetryAction.Retry:
                LogRecorded(await outbox.RecordRetryAsync(email.RequestId, email.AttemptCount, decision.NextAttemptAt!.Value, decision.Error!, cancellationToken), email);
                break;
            default:
                LogRecorded(await outbox.RecordFailedAsync(email.RequestId, email.AttemptCount, decision.Error!, cancellationToken), email);
                LogFailed(email.Kind, email.Message.Language);
                break;
        }

        return decision.Action;
    }

    private void LogRecorded(bool matched, ReadyEmail email)
    {
        if (!matched)
        {
            LogMovedOn(email.Kind);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {Kind} result not recorded: the row had moved on")]
    private partial void LogMovedOn(EmailRequestKind kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email {Kind} in {Language} failed for good")]
    private partial void LogFailed(EmailRequestKind kind, Language language);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email {Kind} in {Language} failed with an unexpected error")]
    private partial void LogError(EmailRequestKind kind, Language language);
}
