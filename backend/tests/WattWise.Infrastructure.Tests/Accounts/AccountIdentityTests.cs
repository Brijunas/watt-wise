using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class AccountIdentityTests
{
    [Fact]
    public void Create_stores_the_email_and_user_name_normalized()
    {
        AccountIdentity identity = AccountIdentity.Create(Guid.NewGuid(), "  ﬁ@X.LT ");

        Assert.Equal(("fi@x.lt", "fi@x.lt"), (identity.Email, identity.UserName));
    }

    [Fact]
    public void Setting_the_email_and_user_name_to_null_stores_null()
    {
        AccountIdentity identity = AccountIdentity.Create(Guid.NewGuid(), "a@x.lt");

        identity.Email = null;
        identity.UserName = null;

        Assert.Equal((null, null), (identity.Email, identity.UserName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_rejects_a_blank_email(string? email)
    {
        Assert.ThrowsAny<ArgumentException>(() => AccountIdentity.Create(Guid.NewGuid(), email!));
    }
}
