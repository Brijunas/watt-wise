using NodaTime;

using WattWise.Domain.Localization;

namespace WattWise.Domain.Identities;

/// <summary>
/// The user: what everything the user owns hangs off. How the user proves who they are (email and
/// password, later OAuth) is kept apart, in Infrastructure. The account holds the ids of its roles;
/// what they grant is read by a query.
/// </summary>
public sealed class Account
{
    private readonly List<AccountRole> _roles = [];

    // Used by EF Core.
    private Account()
    {
    }

    private Account(Guid id, Language preferredLanguage, Instant createdAt)
    {
        Id = id;
        PreferredLanguage = preferredLanguage;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Language PreferredLanguage { get; private set; }

    public Instant CreatedAt { get; private set; }

    public IReadOnlyCollection<Guid> RoleIds => [.. _roles.Select(role => role.RoleId)];

    public static Account Create(Language preferredLanguage, Instant now)
    {
        return new Account(Guid.CreateVersion7(), preferredLanguage, now);
    }

    /// <summary>Gives the account the role; adding it again changes nothing.</summary>
    public void AddRole(Guid roleId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(roleId, Guid.Empty);

        if (!_roles.Any(existing => existing.RoleId == roleId))
        {
            _roles.Add(AccountRole.Create(Id, roleId));
        }
    }
}
