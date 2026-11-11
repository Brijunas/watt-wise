using WattWise.Application;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

internal sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
