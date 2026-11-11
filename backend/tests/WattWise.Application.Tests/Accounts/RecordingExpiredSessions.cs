using NodaTime;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

internal sealed class RecordingExpiredSessions : IExpiredSessions
{
    public Instant? PurgedAt { get; private set; }

    public int Result { get; set; }

    public Task<int> PurgeAsync(Instant now, CancellationToken cancellationToken)
    {
        PurgedAt = now;
        return Task.FromResult(Result);
    }
}
