using WattWise.Application.Results;

namespace WattWise.Application;

/// <summary>
/// Runs a use case's steps in one database transaction, for the steps one <see cref="IUnitOfWork"/> save can't cover: an
/// ASP.NET Core Identity call, or a bulk statement, next to other writes. Writes that must survive a failure (the lockout
/// count) stay outside it.
/// </summary>
public interface ITransactionRunner
{
    /// <summary>
    /// Runs <paramref name="work"/> and commits when its result succeeded. When the result failed or the work threw, rolls
    /// back and drops every tracked change. Joins a transaction that is already open: the owner commits or rolls back, and a failed result still drops the tracked changes. The work may run more than once, so
    /// it loads its own state.
    /// </summary>
    /// <param name="work">The steps; they receive the token to pass on.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken);
}
