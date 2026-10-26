using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// Holds each non-query command whose text contains a given fragment until a set number of them have
/// reached it, so a test can make several scopes run the same statement at the same moment. Does
/// nothing until armed.
/// </summary>
internal sealed class CommandBarrier : DbCommandInterceptor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private TaskCompletionSource? _gate;
    private string _fragment = string.Empty;
    private int _participants;
    private int _arrived;

    /// <summary>How many matching commands have reached the barrier since it was armed.</summary>
    public int Arrived => Volatile.Read(ref _arrived);

    /// <summary>From now on, commands containing <paramref name="fragment"/> wait until <paramref name="count"/> of them have arrived.</summary>
    public void Arm(string fragment, int count)
    {
        _fragment = fragment;
        _participants = count;
        _arrived = 0;
        _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? current = _gate;
        if (current is not null && command.CommandText.Contains(_fragment, StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref _arrived) == _participants)
            {
                current.SetResult();
            }

            await current.Task.WaitAsync(Timeout, cancellationToken);
        }

        return result;
    }
}
