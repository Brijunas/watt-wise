using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Identity;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

public class DataProtectionRestartTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_email_token_survives_a_restart_and_the_stored_keys_are_encrypted()
    {
        (Guid id, string token) = await IssueTokenAsync();

        // A new provider on the same database stands in for the restarted Api.
        await using ServiceProvider restarted = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = restarted.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity? identity = await users.FindByIdAsync(id.ToString());
        Assert.NotNull(identity);

        IdentityResult result = await users.ConfirmEmailAsync(identity, token);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string[] keys = await db.DataProtectionKeys.Select(key => key.Xml ?? string.Empty).ToArrayAsync(Token);
        Assert.NotEmpty(keys);
        Assert.All(keys, xml =>
        {
            Assert.Contains("encryptedSecret", xml);
            Assert.DoesNotContain("<masterKey", xml);
        });
    }

    // The key ring skips keys it can't decrypt and the token provider treats any unprotect failure
    // as an invalid token, so with another certificate ConfirmEmailAsync fails with InvalidToken
    // rather than throwing.
    [Fact]
    public async Task An_email_token_cannot_be_validated_with_another_certificate()
    {
        (Guid id, string token) = await IssueTokenAsync();

        await using ServiceProvider other = fixture.BuildAccountServices(TestCertificates.OtherPem);
        await using AsyncServiceScope scope = other.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity? identity = await users.FindByIdAsync(id.ToString());
        Assert.NotNull(identity);

        IdentityResult result = await users.ConfirmEmailAsync(identity, token);

        Assert.False(result.Succeeded);
        Assert.Contains("InvalidToken", result.Errors.Select(error => error.Code));
    }

    private async Task<(Guid Id, string Token)> IssueTokenAsync()
    {
        await using ServiceProvider issuing = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = issuing.CreateAsyncScope();
        AccountIdentity identity = await AccountsTestSupport.CreateIdentityAsync(
            scope.ServiceProvider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token);
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        return (identity.Id, await users.GenerateEmailConfirmationTokenAsync(identity));
    }
}
