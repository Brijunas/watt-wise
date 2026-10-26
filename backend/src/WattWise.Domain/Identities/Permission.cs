namespace WattWise.Domain.Identities;

/// <summary>
/// One thing an account may be allowed to do, such as <c>admin.access</c>. Endpoints check
/// permissions, never role names; a role grants a set of them.
/// </summary>
public sealed class Permission
{
    // Used by EF Core.
    private Permission()
    {
    }

    private Permission(Guid id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public Guid Id { get; private set; }

    /// <summary>Unique name, the one endpoints check.</summary>
    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static Permission Create(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);

        return new Permission(Guid.CreateVersion7(), name, description);
    }
}
