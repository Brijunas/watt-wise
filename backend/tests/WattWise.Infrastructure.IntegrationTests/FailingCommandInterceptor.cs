using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// Fails each non-query command whose text contains a given fragment, before it reaches the database, so a test can break
/// one step of a use case (such as the bulk delete of sessions) and check that the steps before it left nothing behind.
/// Does nothing until armed.
/// </summary>
internal sealed class FailingCommandInterceptor : DbCommandInterceptor
{
    private string? _fragment;

    /// <summary>The commands that were failed.</summary>
    public int Failures { get; private set; }

    /// <summary>From now on, a command containing <paramref name="fragment"/> throws <see cref="InvalidOperationException"/>.</summary>
    public void Arm(string fragment) => _fragment = fragment;

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_fragment is not null && command.CommandText.Contains(_fragment, StringComparison.Ordinal))
        {
            Failures++;
            throw new InvalidOperationException("Injected fault.");
        }

        return ValueTask.FromResult(result);
    }
}
