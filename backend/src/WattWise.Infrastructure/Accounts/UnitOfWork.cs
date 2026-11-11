using Microsoft.EntityFrameworkCore;

using WattWise.Application;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

internal sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The context keeps the failed changes; drop them so the handler can load fresh state and go on.
            context.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(exception);
        }
    }
}
