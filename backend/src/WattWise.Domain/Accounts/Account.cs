using NodaTime;

namespace WattWise.Domain.Accounts;

/// <summary>
/// The user: what everything the user owns hangs off. How the user proves who they are (email and
/// password, later OAuth) is kept apart, in Infrastructure; roles and permissions sit here so they
/// hold whatever way the user signed in.
/// </summary>
public sealed class Account
{
    private readonly List<Role> _roles = [];

    // For EF Core.
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

    public IReadOnlyCollection<Role> Roles => _roles;

    public static Account Create(Language preferredLanguage, Instant now)
    {
        return new Account(Guid.CreateVersion7(), preferredLanguage, now);
    }

    /// <summary>Gives the account the role; adding it again changes nothing.</summary>
    public void AddRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (!_roles.Any(existing => existing.Id == role.Id))
        {
            _roles.Add(role);
        }
    }

    /// <summary>
    /// The names of the permissions the account's roles grant, each once. Needs <see cref="Roles"/> and
    /// their permissions loaded (<c>Include(a =&gt; a.Roles).ThenInclude(r =&gt; r.Permissions)</c>);
    /// otherwise it returns an empty list.
    /// </summary>
    public IReadOnlyCollection<string> Permissions()
    {
        return [.. _roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).Distinct(StringComparer.Ordinal)];
    }
}
