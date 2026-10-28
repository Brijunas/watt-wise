using System.Text;

using Microsoft.Extensions.DependencyInjection;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountPasswordTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_password_set_in_NFC_verifies_in_NFD_from_a_new_scope()
    {
        const string nfc = "Pässwörd-Ünïcode-1";
        string nfd = nfc.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(nfc, nfd);
        await using ServiceProvider provider = fixture.BuildAccountServices();
        AccountIdentityIds ids = await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), nfc, AccountsTestSupport.Token);

        bool verified = await AccountsTestSupport.CheckPasswordAsync(provider, ids.IdentityId, nfd);

        Assert.True(verified);
    }
}
