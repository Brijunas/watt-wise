using Microsoft.EntityFrameworkCore;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Deletes an account's sessions, or one of them, with one statement; the refresh tokens go with them (the foreign key cascades).</summary>
internal sealed class AccountSessions(AppDbContext context) : IAccountSessions
{
    public async Task<int> EndAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await context.Set<Session>()
            .Where(session => session.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> EndAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        return await context.Set<Session>()
            .Where(session => session.Id == sessionId && session.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
