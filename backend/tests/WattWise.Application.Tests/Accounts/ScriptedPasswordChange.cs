using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The password change port: answers what it is told and records the account and passwords it got.</summary>
internal sealed class ScriptedPasswordChange(List<string> calls) : IPasswordChange
{
    public CurrentPasswordCheck CheckAnswer { get; set; } = CurrentPasswordCheck.Verified();

    public PasswordChangeOutcome ChangeAnswer { get; set; } = PasswordChangeOutcome.Changed("new-stamp");

    public Task<CurrentPasswordCheck> CheckCurrentAsync(Guid accountId, string currentPassword, CancellationToken cancellationToken)
    {
        calls.Add($"CheckCurrent:{accountId}:{currentPassword}");
        return Task.FromResult(CheckAnswer);
    }

    public Task<PasswordChangeOutcome> ChangeAsync(Guid accountId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        calls.Add($"Change:{accountId}:{currentPassword}:{newPassword}");
        return Task.FromResult(ChangeAnswer);
    }
}
