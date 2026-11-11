using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The password check port: answers what it is told and records the address and password it got.</summary>
internal sealed class RecordingPasswordSignIn(List<string> calls, PasswordCheck answer) : IPasswordSignIn
{
    public Task<PasswordCheck> CheckAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        calls.Add($"Check:{normalizedEmail}:{password}");
        return Task.FromResult(answer);
    }
}
