using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests.Accounts;

public class PermissionTests
{
    [Fact]
    public void Create_makes_a_version_7_id()
    {
        Permission permission = Permission.Create("admin.access", "Opens the admin app.");

        Assert.Equal(7, permission.Id.Version);
    }

    [Fact]
    public void Create_keeps_the_name_and_the_description()
    {
        Permission permission = Permission.Create("admin.access", "Opens the admin app.");

        Assert.Equal(("admin.access", "Opens the admin app."), (permission.Name, permission.Description));
    }

    [Fact]
    public void Create_rejects_a_null_name()
    {
        Assert.Throws<ArgumentNullException>("name", () => Permission.Create(null!, "Description."));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Create_rejects_an_empty_or_whitespace_name(string name)
    {
        Assert.Throws<ArgumentException>("name", () => Permission.Create(name, "Description."));
    }

    [Fact]
    public void Create_rejects_a_null_description()
    {
        Assert.Throws<ArgumentNullException>("description", () => Permission.Create("admin.access", null!));
    }
}
