namespace WattWise.Domain.Accounts;

/// <summary>A named set of permissions that can be given to accounts.</summary>
public sealed class Role
{
    private readonly List<Permission> _permissions = [];

    // For EF Core.
    private Role()
    {
    }

    private Role(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    /// <summary>Unique name, such as <c>Admin</c>.</summary>
    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<Permission> Permissions => _permissions;

    public static Role Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role(Guid.CreateVersion7(), name);
    }

    /// <summary>Makes the role grant the permission; granting it again changes nothing.</summary>
    public void Grant(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (!_permissions.Any(existing => existing.Id == permission.Id))
        {
            _permissions.Add(permission);
        }
    }
}
