using NodaTime;

namespace WattWise.Application.Email;

/// <summary>
/// The worker's side of the email outbox: the SQL, the lookups and the link creation, behind a port because Application has
/// no EF Core. The handler owns the order of steps and the retry decision (through <see cref="EmailRetryPolicy"/>);
/// each method here does one step and takes its time from <c>IClock</c>. Every method commits its own transaction.
/// The handler's order is: <see cref="RecoverStuckAsync"/>, <see cref="ClaimAsync"/>, send each item, then record.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Step 1. Rows in <c>sending</c> whose last attempt is older than <paramref name="stuckAfter"/> go back to <c>pending</c>
    /// (due now, <c>last_error</c> = <c>SendingTimedOut</c>), unless they already timed out once (<c>last_error</c> is already
    /// <c>SendingTimedOut</c>) or their attempts are used up, in which case they become <c>failed</c>.
    /// </summary>
    /// <param name="stuckAfter">How long a row may stay in <c>sending</c>.</param>
    /// <param name="maxAttempts">The number of attempts each kind allows; every kind has an entry.</param>
    /// <returns>The number of rows recovered or failed.</returns>
    Task<int> RecoverStuckAsync(Duration stuckAfter, IReadOnlyDictionary<EmailRequestKind, int> maxAttempts, CancellationToken cancellationToken);

    /// <summary>
    /// Steps 2 to 6, in one transaction committed before any SMTP traffic. Claims up to <paramref name="batchSize"/> due
    /// <c>pending</c> rows (to <c>sending</c>, counting the attempt), coalesces them (only the newest request per kind and
    /// account or address survives; older ones become <c>superseded</c>), resolves each to an account and its language, and
    /// creates the link token. The row is deleted and nothing is returned for an unknown address, a confirmation for an
    /// already-confirmed account, and a password reset for an unconfirmed account.
    /// </summary>
    /// <returns>The messages to send, in claim order; empty when nothing is due.</returns>
    Task<IReadOnlyList<ReadyEmail>> ClaimAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Step 8, success: marks the row <c>sent</c> and sets <c>sent_at</c>, but only while it is still <c>sending</c> with
    /// <c>attempt_count</c> = <paramref name="attempt"/>, so a late result never overwrites a row that recovery or a later pass moved on.
    /// </summary>
    /// <returns>True when the row matched and was updated; false when it had moved on (not an error).</returns>
    Task<bool> RecordSentAsync(Guid requestId, int attempt, CancellationToken cancellationToken);

    /// <summary>
    /// Step 8, retry: back to <c>pending</c>, due at <paramref name="nextAttemptAt"/>, with <paramref name="error"/> as <c>last_error</c>;
    /// only while the row is still <c>sending</c> with <c>attempt_count</c> = <paramref name="attempt"/>.
    /// </summary>
    /// <returns>True when the row matched and was updated; false when it had moved on.</returns>
    Task<bool> RecordRetryAsync(Guid requestId, int attempt, Instant nextAttemptAt, string error, CancellationToken cancellationToken);

    /// <summary>
    /// Step 8, final failure: marks the row <c>failed</c> with <paramref name="error"/> (an exception type name or SMTP status,
    /// cut to 100 characters); only while the row is still <c>sending</c> with <c>attempt_count</c> = <paramref name="attempt"/>.
    /// </summary>
    /// <returns>True when the row matched and was updated; false when it had moved on.</returns>
    Task<bool> RecordFailedAsync(Guid requestId, int attempt, string error, CancellationToken cancellationToken);

    /// <summary>Deletes <c>sent</c>, <c>superseded</c> and <c>failed</c> rows created before <paramref name="createdBefore"/>.</summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> PurgeAsync(Instant createdBefore, CancellationToken cancellationToken);
}
