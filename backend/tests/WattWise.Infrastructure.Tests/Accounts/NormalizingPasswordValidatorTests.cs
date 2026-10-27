using Microsoft.AspNetCore.Identity;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class NormalizingPasswordValidatorTests
{
    [Fact]
    public async Task A_null_password_throws()
    {
        NormalizingPasswordValidator validator = new(new IdentityErrorDescriber());
        AccountIdentity identity = AccountIdentity.Create(Guid.NewGuid(), "someone@example.lt");

        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.ValidateAsync(null!, identity, null));
    }
}
