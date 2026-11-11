using WattWise.Application.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Application.Tests.Accounts;

/// <summary>A hand-written registration port: writes every call to the shared call log.</summary>
internal sealed class RecordingAccountRegistration(List<string> calls) : IAccountRegistration
{
    /// <summary>What <see cref="FindAccountIdAsync"/> returns: the owner of the address, or null.</summary>
    public Guid? Existing { get; set; }

    public Guid StagedAccountId { get; } = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    /// <summary>What <see cref="CreateIdentityAsync"/> returns; false stands for an address taken in the meantime.</summary>
    public bool Created { get; set; } = true;

    public Task<Guid?> FindAccountIdAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        calls.Add($"Find:{normalizedEmail}");
        return Task.FromResult(Existing);
    }

    public Guid StageAccount(Language preferredLanguage)
    {
        calls.Add($"Stage:{preferredLanguage}");
        return StagedAccountId;
    }

    public Task<bool> CreateIdentityAsync(Guid accountId, string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        calls.Add($"CreateIdentity:{accountId}:{normalizedEmail}:{password}");
        return Task.FromResult(Created);
    }

    public void HashAndDiscard(string password) => calls.Add($"HashAndDiscard:{password}");
}
