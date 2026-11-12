using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The password reset port: answers what it is told and records the id, token and password it got.</summary>
internal sealed class ScriptedPasswordReset(List<string> calls) : IPasswordReset
{
    public PasswordResetOutcome Answer { get; set; } = PasswordResetOutcome.InvalidLink();

    public Task<PasswordResetOutcome> ResetAsync(Guid accountIdentityId, string token, string newPassword, CancellationToken cancellationToken)
    {
        calls.Add($"Reset:{accountIdentityId}:{token}:{newPassword}");
        return Task.FromResult(Answer);
    }
}
