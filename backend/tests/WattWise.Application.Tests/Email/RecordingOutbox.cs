using NodaTime;

using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

/// <summary>A hand-written outbox port: hands out the rows it was given and writes every call to the shared call log.</summary>
internal sealed class RecordingOutbox(List<string> calls) : IEmailOutbox
{
    public List<ReadyEmail> ToClaim { get; } = [];

    public (Duration StuckAfter, IReadOnlyDictionary<EmailRequestKind, int> MaxAttempts)? Recovery { get; private set; }

    public int? BatchSize { get; private set; }

    public Instant? PurgedBefore { get; private set; }

    public int PurgeResult { get; set; }

    /// <summary>Thrown by <see cref="RecordSentAsync"/>, standing in for a database error after a successful send.</summary>
    public Exception? RecordSentFailure { get; set; }

    /// <summary>What every Record* call returns: false stands for a row that had moved on.</summary>
    public bool RecordMatches { get; set; } = true;

    /// <summary>The attempt number each Record* call carried, in call order.</summary>
    public List<int> RecordedAttempts { get; } = [];

    public Task<int> RecoverStuckAsync(Duration stuckAfter, IReadOnlyDictionary<EmailRequestKind, int> maxAttempts, CancellationToken cancellationToken)
    {
        calls.Add("recover");
        Recovery = (stuckAfter, maxAttempts);
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<ReadyEmail>> ClaimAsync(int batchSize, CancellationToken cancellationToken)
    {
        calls.Add("claim");
        BatchSize = batchSize;
        return Task.FromResult<IReadOnlyList<ReadyEmail>>([.. ToClaim]);
    }

    public Task<bool> RecordSentAsync(Guid requestId, int attempt, CancellationToken cancellationToken)
    {
        calls.Add($"sent {requestId}");
        RecordedAttempts.Add(attempt);
        return RecordSentFailure is null ? Task.FromResult(RecordMatches) : Task.FromException<bool>(RecordSentFailure);
    }

    public Task<bool> RecordRetryAsync(Guid requestId, int attempt, Instant nextAttemptAt, string error, CancellationToken cancellationToken)
    {
        calls.Add($"retry {requestId} {nextAttemptAt:uuuu-MM-dd'T'HH:mm:ss'Z'} {error}");
        RecordedAttempts.Add(attempt);
        return Task.FromResult(RecordMatches);
    }

    public Task<bool> RecordFailedAsync(Guid requestId, int attempt, string error, CancellationToken cancellationToken)
    {
        calls.Add($"failed {requestId} {error}");
        RecordedAttempts.Add(attempt);
        return Task.FromResult(RecordMatches);
    }

    public Task<int> PurgeAsync(Instant createdBefore, CancellationToken cancellationToken)
    {
        calls.Add("purge");
        PurgedBefore = createdBefore;
        return Task.FromResult(PurgeResult);
    }
}
