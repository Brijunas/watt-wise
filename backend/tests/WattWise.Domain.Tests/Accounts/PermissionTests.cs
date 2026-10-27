using WattWise.Domain.Accounts;

namespace WattWise.Domain.Tests.Accounts;

public class PermissionTests
{
    [Fact]
    public void Create_sets_a_version_7_id_the_name_and_the_description()
    {
        Permission permission = Permission.Create("admin.access", "Opens the admin app.");

        Assert.Equal(7, permission.Id.Version);
        Assert.Equal("admin.access", permission.Name);
        Assert.Equal("Opens the admin app.", permission.Description);
    }

    [Fact]
    public void Create_rejects_a_null_name()
    {
        Assert.Throws<ArgumentNullException>(() => Permission.Create(null!, "Description."));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Create_rejects_an_empty_or_whitespace_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Permission.Create(name, "Description."));
    }
}
