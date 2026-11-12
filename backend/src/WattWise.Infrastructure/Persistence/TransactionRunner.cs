using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using WattWise.Application;
using WattWise.Application.Results;

namespace WattWise.Infrastructure.Persistence;

/// <summary>
/// Runs the work in one transaction on the scope's <see cref="AppDbContext"/>, so ASP.NET Core Identity's saves, bulk
/// statements and <see cref="IUnitOfWork"/> all join it. Besides the email outbox the only code that opens a transaction. It
/// goes through the execution strategy, which a retrying strategy needs for a user-started transaction; the work may then
/// run again, and each run starts with an empty change tracker.
/// </summary>
internal sealed class TransactionRunner(AppDbContext context) : ITransactionRunner
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            // Joined: the transaction's owner commits or rolls back, but a failed result still drops the tracked changes.
            Result<T> joined = await work(cancellationToken);
            if (joined.IsFailure)
            {
                context.ChangeTracker.Clear();
            }

            return joined;
        }

        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async () =>
            {
                context.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    Result<T> result = await work(cancellationToken);
                    if (result.IsSuccess)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return result;
                    }

                    await transaction.RollbackAsync(cancellationToken);
                    context.ChangeTracker.Clear();
                    return result;
                }
                catch
                {
                    // Disposing the transaction rolls it back.
                    context.ChangeTracker.Clear();
                    throw;
                }
            });
    }
}
