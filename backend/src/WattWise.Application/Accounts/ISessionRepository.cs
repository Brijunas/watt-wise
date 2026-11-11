using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

public interface ISessionRepository
{
    /// <summary>The session that has a refresh token with this hash, used or not; <c>null</c> when there is none.</summary>
    Task<Session?> GetByRefreshTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    void Add(Session session);

    void Remove(Session session);
}
