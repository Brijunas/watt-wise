using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>Deletes sessions that can no longer refresh.</summary>
public interface IExpiredSessions
{
    /// <summary>Deletes refresh tokens expired at <paramref name="now"/> and the sessions left with none; returns the number of sessions deleted.</summary>
    Task<int> PurgeAsync(Instant now, CancellationToken cancellationToken);
}
