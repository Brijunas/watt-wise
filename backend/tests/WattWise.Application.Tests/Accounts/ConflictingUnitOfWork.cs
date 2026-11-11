namespace WattWise.Application.Tests.Accounts;

/// <summary>
/// The saves it is told to (<see cref="ConflictNext"/>) lose a race: they throw <see cref="ConcurrencyConflictException"/> and, as the real
/// unit of work does by dropping its tracked changes, undo the removals staged since the last save.
/// </summary>
internal sealed class ConflictingUnitOfWork(List<string> calls, InMemorySessions sessions) : IUnitOfWork
{
    private int _remaining;

    /// <summary>The next <paramref name="count"/> saves lose a race.</summary>
    public void ConflictNext(int count) => _remaining = count;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_remaining > 0)
        {
            _remaining--;
            calls.Add("SaveConflict");
            sessions.UndoStagedRemovals();
            throw new ConcurrencyConflictException();
        }

        calls.Add("Save");
        sessions.CommitStagedRemovals();
        return Task.CompletedTask;
    }
}
