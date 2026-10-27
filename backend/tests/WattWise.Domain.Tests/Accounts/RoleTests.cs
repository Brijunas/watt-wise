using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests.Accounts;

public class RoleTests
{
    [Fact]
    public void Create_makes_a_version_7_id()
    {
        Role role = Role.Create("Admin");

        Assert.Equal(7, role.Id.Version);
    }

    [Fact]
    public void Create_keeps_the_name()
    {
        Role role = Role.Create("Admin");

        Assert.Equal("Admin", role.Name);
    }

    [Fact]
    public void Create_starts_with_no_permissions()
    {
        Role role = Role.Create("Admin");

        Assert.Empty(role.PermissionIds);
    }

    [Fact]
    public void Create_rejects_a_null_name()
    {
        Assert.Throws<ArgumentNullException>("name", () => Role.Create(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Create_rejects_an_empty_or_whitespace_name(string name)
    {
        Assert.Throws<ArgumentException>("name", () => Role.Create(name));
    }

    [Fact]
    public void Grant_keeps_two_different_permissions()
    {
        Role role = Role.Create("Admin");
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();

        role.Grant(first);
        role.Grant(second);

        Assert.Equal(new HashSet<Guid> { first, second }, role.PermissionIds.ToHashSet());
    }

    [Fact]
    public void Grant_twice_with_the_same_permission_adds_it_once()
    {
        Role role = Role.Create("Admin");
        Guid permissionId = Guid.CreateVersion7();

        role.Grant(permissionId);
        role.Grant(permissionId);

        Assert.Equal([permissionId], role.PermissionIds);
    }

    [Fact]
    public void Grant_rejects_an_empty_permission_id()
    {
        Role role = Role.Create("Admin");

        Assert.Throws<ArgumentOutOfRangeException>("permissionId", () => role.Grant(Guid.Empty));
    }
}
