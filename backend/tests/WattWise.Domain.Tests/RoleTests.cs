using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests;

public class RoleTests
{
    [Fact]
    public void Create_sets_a_version_7_id_and_the_name()
    {
        Role role = Role.Create("Admin");

        Assert.Equal(7, role.Id.Version);
        Assert.Equal("Admin", role.Name);
        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void Create_rejects_a_null_name()
    {
        Assert.Throws<ArgumentNullException>(() => Role.Create(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Create_rejects_an_empty_or_whitespace_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Role.Create(name));
    }

    [Fact]
    public void Grant_twice_with_the_same_permission_adds_it_once()
    {
        Role role = Role.Create("Admin");
        Permission permission = Permission.Create("admin.access", "Opens the admin app.");

        role.Grant(permission);
        role.Grant(permission);

        Assert.Same(permission, Assert.Single(role.Permissions));
    }

    [Fact]
    public void Grant_rejects_null()
    {
        Role role = Role.Create("Admin");

        Assert.Throws<ArgumentNullException>(() => role.Grant(null!));
    }
}
