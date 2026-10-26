using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Identity;

namespace WattWise.Infrastructure.IntegrationTests;

public class PasswordRuleTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Abcdefgh-123x", "PasswordTooShort")] // 13 characters, every class present
    [InlineData("ABCDEFGH-1234567", "PasswordRequiresLower")]
    [InlineData("abcdefgh-1234567", "PasswordRequiresUpper")]
    [InlineData("Abcdefgh-ijklmnop", "PasswordRequiresDigit")]
    [InlineData("Abcdefgh1234567x", "PasswordRequiresNonAlphanumeric")]
    public async Task A_password_that_breaks_a_rule_is_rejected(string password, string expectedCode)
    {
        IdentityResult result = await CreateAsync(password);

        Assert.False(result.Succeeded);
        Assert.Contains(expectedCode, result.Errors.Select(error => error.Code));
    }

    [Fact]
    public async Task A_password_over_256_characters_is_rejected()
    {
        string password = "Aa1-" + new string('x', 253); // 257 characters, every class present

        IdentityResult result = await CreateAsync(password);

        Assert.False(result.Succeeded);
        Assert.Equal("PasswordTooLong", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task A_password_of_exactly_256_characters_is_accepted()
    {
        IdentityResult result = await CreateAsync("Aa1-" + new string('x', 252));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task A_valid_14_character_password_is_accepted()
    {
        IdentityResult result = await CreateAsync("Abcdefgh-1234x");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task A_password_set_in_nfc_verifies_when_entered_in_nfd()
    {
        const string nfc = "Pässwörd-Ünïcode-1";
        string nfd = nfc.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(nfc, nfd);
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AccountIdentity identity = await AccountsTestSupport.CreateIdentityAsync(
            scope.ServiceProvider, AccountsTestSupport.UniqueEmail(), nfc, Token);
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();

        Assert.True(await users.CheckPasswordAsync(identity, nfd));
        Assert.True(await users.CheckPasswordAsync(identity, nfc));
        Assert.False(await users.CheckPasswordAsync(identity, nfc + "x"));
    }

    private async Task<IdentityResult> CreateAsync(string password)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        // The account is saved first so an accepted password can be stored.
        var db = scope.ServiceProvider.GetRequiredService<WattWise.Infrastructure.Persistence.AppDbContext>();
        WattWise.Domain.Accounts.Account account = AccountsTestSupport.NewAccount();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);

        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        return await users.CreateAsync(AccountIdentity.Create(account.Id, AccountsTestSupport.UniqueEmail()), password);
    }
}
