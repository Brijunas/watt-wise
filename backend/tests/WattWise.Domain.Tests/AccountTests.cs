using NodaTime;

using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests;

public class AccountTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 2, 3, 4);

    [Fact]
    public void Create_sets_a_version_7_id_and_keeps_the_language_and_time()
    {
        Account account = Account.Create(Language.En, Now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(7, account.Id.Version);
        Assert.Equal(Language.En, account.PreferredLanguage);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Empty(account.Roles);
    }

    [Fact]
    public void AddRole_twice_with_the_same_role_adds_it_once()
    {
        Account account = Account.Create(Language.Lt, Now);
        Role role = Role.Create("Admin");

        account.AddRole(role);
        account.AddRole(role);

        Assert.Same(role, Assert.Single(account.Roles));
    }

    [Fact]
    public void AddRole_rejects_null()
    {
        Account account = Account.Create(Language.Lt, Now);

        Assert.Throws<ArgumentNullException>(() => account.AddRole(null!));
    }

    [Fact]
    public void Permissions_is_empty_without_roles()
    {
        Account account = Account.Create(Language.Lt, Now);

        Assert.Empty(account.Permissions());
    }

    [Fact]
    public void Permissions_lists_a_permission_shared_by_two_roles_once()
    {
        Permission read = Permission.Create("read", "Read.");
        Permission write = Permission.Create("write", "Write.");
        Permission delete = Permission.Create("delete", "Delete.");
        Role editor = Role.Create("Editor");
        editor.Grant(read);
        editor.Grant(write);
        Role reviewer = Role.Create("Reviewer");
        reviewer.Grant(write);
        reviewer.Grant(delete);
        Account account = Account.Create(Language.Lt, Now);
        account.AddRole(editor);
        account.AddRole(reviewer);

        IReadOnlyCollection<string> permissions = account.Permissions();

        Assert.Equal(3, permissions.Count);
        Assert.Equivalent(new[] { "read", "write", "delete" }, permissions, strict: false);
    }
}
