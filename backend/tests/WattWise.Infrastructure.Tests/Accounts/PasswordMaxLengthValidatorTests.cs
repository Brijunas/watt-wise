using Microsoft.AspNetCore.Identity;

using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.Tests.Accounts;

public class PasswordMaxLengthValidatorTests
{
    private static AccountIdentity Identity => AccountIdentity.Create(Guid.NewGuid(), "someone@example.lt");

    [Fact]
    public async Task A_null_password_throws()
    {
        PasswordMaxLengthValidator validator = new();

        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.ValidateAsync(null!, Identity, null));
    }

    // characterization: PasswordMaxLengthValidator.cs:18 (the description text)
    [Fact]
    public async Task The_too_long_error_names_the_limit()
    {
        PasswordMaxLengthValidator validator = new();

        IdentityResult result = await validator.ValidateAsync(null!, Identity, new string('x', 257));

        Assert.Equal("Passwords must be at most 256 characters.", Assert.Single(result.Errors).Description);
    }
}
