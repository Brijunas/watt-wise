using NodaTime;

namespace WattWise.Application.Email;

/// <summary>Decides, without I/O, whether and when a failed send is tried again.</summary>
public static class EmailRetryPolicy
{
    /// <summary>
    /// Decides what happens to a row after an attempt. A retryable failure with attempts left is retried after the
    /// next delay; a permanent failure, or a retryable one on the last attempt, is final.
    /// </summary>
    /// <param name="schedule">The schedule of the row's kind.</param>
    /// <param name="attempt">The number of the attempt just made, starting at 1 (already counted).</param>
    /// <param name="result">How the attempt ended.</param>
    /// <param name="now">The current time.</param>
    public static RetryDecision Decide(EmailRetrySchedule schedule, int attempt, SendResult result, Instant now)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome switch
        {
            SendOutcome.Sent => new RetryDecision(RetryAction.Sent, null, null),
            SendOutcome.Retryable when attempt < schedule.Attempts =>
                new RetryDecision(RetryAction.Retry, now + Duration.FromTimeSpan(schedule.Delays[attempt]), result.Error),
            _ => new RetryDecision(RetryAction.Failed, null, result.Error),
        };
    }
}
