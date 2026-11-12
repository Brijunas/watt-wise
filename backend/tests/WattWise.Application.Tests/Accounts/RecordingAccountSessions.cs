using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The bulk session port: records the account (and session) it was asked to end and answers how many it ended.</summary>
internal sealed class RecordingAccountSessions(List<string> calls) : IAccountSessions
{
    /// <summary>What <see cref="EndAllAsync"/> answers.</summary>
    public int EndAllResult { get; set; } = 1;

    /// <summary>What <see cref="EndAsync"/> answers; 0 stands for an unknown session or another account's.</summary>
    public int EndResult { get; set; } = 1;

    public Task<int> EndAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        calls.Add($"EndAll:{accountId}");
        return Task.FromResult(EndAllResult);
    }

    public Task<int> EndAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        calls.Add($"End:{accountId}:{sessionId}");
        return Task.FromResult(EndResult);
    }
}
