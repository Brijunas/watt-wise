using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The smoke account port: records the email and password it is asked to ensure and answers what it is told to.</summary>
internal sealed class RecordingSmokeAccounts(List<string> calls) : ISmokeAccounts
{
    /// <summary>What <see cref="EnsureAsync"/> answers.</summary>
    public EnsuredAccount Result { get; set; } = new(SmokeHost.AccountId, SeedOutcome.Unchanged, PasswordChanged: false);

    public Task<EnsuredAccount> EnsureAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        calls.Add($"Ensure:{normalizedEmail}:{password}");
        return Task.FromResult(Result);
    }
}
