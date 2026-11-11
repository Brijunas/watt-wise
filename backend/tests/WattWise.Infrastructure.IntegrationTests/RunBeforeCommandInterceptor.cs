using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// Runs a callback once, just before the first non-query command whose text contains a given fragment, so a test can change
/// the stored data between a use case's read and its write. The callback runs before the command starts. Does nothing until armed.
/// </summary>
internal sealed class RunBeforeCommandInterceptor : DbCommandInterceptor
{
    private string? _fragment;
    private Func<Task>? _callback;

    /// <summary>From now on, the first command containing <paramref name="fragment"/> runs <paramref name="callback"/> first.</summary>
    public void Arm(string fragment, Func<Task> callback)
    {
        _fragment = fragment;
        _callback = callback;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Func<Task>? callback = _callback;
        if (callback is not null && _fragment is not null && command.CommandText.Contains(_fragment, StringComparison.Ordinal))
        {
            // Disarm first: the callback's own statements must not trigger it again.
            _callback = null;
            await callback();
        }

        return result;
    }
}
