using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The bulk session port: records the account whose sessions it was asked to end.</summary>
internal sealed class RecordingAccountSessions(List<string> calls) : IAccountSessions
{
    public Task<int> EndAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        calls.Add($"EndAll:{accountId}");
        return Task.FromResult(1);
    }
}
