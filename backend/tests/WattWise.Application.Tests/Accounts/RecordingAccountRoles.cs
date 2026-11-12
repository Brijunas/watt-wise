using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The role port: knows one account by its normalized email, records every change and answers what it is told to.</summary>
internal sealed class RecordingAccountRoles(List<string> calls) : IAccountRoles
{
    /// <summary>The account <see cref="FindByEmailAsync"/> finds, by normalized email; none when null.</summary>
    public (string NormalizedEmail, RoleHolder Holder)? Known { get; set; }

    /// <summary>What <see cref="AddAsync"/> answers: false stands for an account that already had the role.</summary>
    public bool AddResult { get; set; } = true;

    /// <summary>What <see cref="RemoveAsync"/> answers: false stands for an account without the role.</summary>
    public bool RemoveResult { get; set; } = true;

    public Task<RoleHolder?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        calls.Add($"Find:{normalizedEmail}");
        return Task.FromResult(Known is { } known && known.NormalizedEmail == normalizedEmail ? known.Holder : null);
    }

    public Task<bool> AddAsync(Guid accountId, string roleName, CancellationToken cancellationToken)
    {
        calls.Add($"AddRole:{accountId}:{roleName}");
        return Task.FromResult(AddResult);
    }

    public Task<bool> RemoveAsync(Guid accountId, string roleName, CancellationToken cancellationToken)
    {
        calls.Add($"RemoveRole:{accountId}:{roleName}");
        return Task.FromResult(RemoveResult);
    }
}
