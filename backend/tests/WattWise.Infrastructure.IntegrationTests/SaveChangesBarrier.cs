using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// Holds each <c>SaveChanges</c> until a set number of contexts have reached it, so a test can make
/// several scopes load their data before any of them writes. Does nothing until armed.
/// </summary>
internal sealed class SaveChangesBarrier : SaveChangesInterceptor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private TaskCompletionSource? gate;
    private int participants;
    private int arrived;

    /// <summary>How many saves have reached the barrier since it was armed.</summary>
    public int Arrived => Volatile.Read(ref arrived);

    /// <summary>From now on, saves wait until <paramref name="count"/> of them have arrived.</summary>
    public void Arm(int count)
    {
        participants = count;
        arrived = 0;
        gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? current = gate;
        if (current is not null)
        {
            if (Interlocked.Increment(ref arrived) == participants)
            {
                current.SetResult();
            }

            await current.Task.WaitAsync(Timeout, cancellationToken);
        }

        return result;
    }
}
