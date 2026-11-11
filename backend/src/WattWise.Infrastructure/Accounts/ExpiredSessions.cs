using Microsoft.EntityFrameworkCore;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Deletes a session once its newest refresh token has expired, and expired tokens of sessions that go on. The tokens
/// of a deleted session go with it (the foreign key cascades). A used token is kept until it expires, to catch reuse.
/// </summary>
internal sealed class ExpiredSessions(AppDbContext context) : IExpiredSessions
{
    public async Task<int> PurgeAsync(Instant now, CancellationToken cancellationToken)
    {
        DbSet<RefreshToken> tokens = context.Set<RefreshToken>();

        int deleted = await context.Set<Session>()
            .Where(session => !tokens.Any(token => token.SessionId == session.Id && token.ExpiresAt > now))
            .ExecuteDeleteAsync(cancellationToken);
        _ = await tokens.Where(token => token.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        return deleted;
    }
}
