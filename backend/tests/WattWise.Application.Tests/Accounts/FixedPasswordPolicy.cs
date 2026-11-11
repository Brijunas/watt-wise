using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

internal sealed class FixedPasswordPolicy(List<string> calls, params string[] messages) : IPasswordPolicy
{
    public Task<IReadOnlyList<string>> CheckAsync(string password, CancellationToken cancellationToken)
    {
        calls.Add($"Check:{password}");
        return Task.FromResult<IReadOnlyList<string>>(messages);
    }
}
