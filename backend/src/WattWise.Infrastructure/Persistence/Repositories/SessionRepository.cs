using Microsoft.EntityFrameworkCore;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;

namespace WattWise.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository(AppDbContext context) : ISessionRepository
{
    public async Task<Session?> GetByRefreshTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        // The hash is unique, so at most one token and one session match.
        return await context.Set<Session>()
            .Where(session => context.Set<RefreshToken>().Any(token => token.SessionId == session.Id && token.TokenHash == tokenHash))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(Session session)
    {
        context.Set<Session>().Add(session);
    }

    public void Remove(Session session)
    {
        context.Set<Session>().Remove(session);
    }
}
