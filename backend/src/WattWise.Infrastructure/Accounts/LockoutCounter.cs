using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Counts wrong passwords toward the lockout with one UPDATE statement each, and not with <c>UserManager.AccessFailedAsync</c>:
/// that reads the count, adds one and saves under the row's concurrency stamp, so parallel wrong guesses clash and drop
/// increments, and far more than the allowed number would fit in a lockout window. The statements do what ASP.NET Core
/// Identity does (count, and at the limit lock and reset the count) from the options and the clock, and give the row a new
/// concurrency stamp. They commit at once, so callers use them outside <c>ITransactionRunner</c> and never on a row they
/// hold as a tracked identity inside it.
/// </summary>
internal sealed class LockoutCounter(AppDbContext context, IOptions<IdentityOptions> identityOptions, TimeProvider timeProvider)
{
    public async Task CountFailureAsync(Guid identityId, CancellationToken cancellationToken)
    {
        LockoutOptions lockout = identityOptions.Value.Lockout;
        int limit = lockout.MaxFailedAccessAttempts;
        DateTimeOffset lockedUntil = timeProvider.GetUtcNow() + lockout.DefaultLockoutTimeSpan;
        string stamp = Guid.NewGuid().ToString();

        // Every right-hand side reads the row as it was before the statement, so the count and the lock agree.
        _ = await context.Users
            .Where(user => user.Id == identityId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.AccessFailedCount, user => user.AccessFailedCount + 1 >= limit ? 0 : user.AccessFailedCount + 1)
                    .SetProperty(user => user.LockoutEnd, user => user.AccessFailedCount + 1 >= limit ? lockedUntil : user.LockoutEnd)
                    .SetProperty(user => user.ConcurrencyStamp, stamp),
                cancellationToken);
    }

    public async Task ResetFailureCountAsync(Guid identityId, CancellationToken cancellationToken)
    {
        string stamp = Guid.NewGuid().ToString();

        // Only when there is something to reset. A wrong guess counted at the same moment may be wiped with it; that is
        // harmless, since whoever gets here has just given the right password.
        _ = await context.Users
            .Where(user => user.Id == identityId && user.AccessFailedCount > 0)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.AccessFailedCount, 0)
                    .SetProperty(user => user.ConcurrencyStamp, stamp),
                cancellationToken);
    }
}
