using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The transaction port: records <c>Begin</c> before the work and <c>Commit</c> (a success) or <c>Rollback</c> (a failure)
/// after it, as the real runner does, so a test sees which steps ran inside the transaction.
/// </summary>
internal sealed class RecordingTransactionRunner(List<string> calls) : ITransactionRunner
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        calls.Add("Begin");
        Result<T> result = await work(cancellationToken);
        calls.Add(result.IsSuccess ? "Commit" : "Rollback");
        return result;
    }
}
