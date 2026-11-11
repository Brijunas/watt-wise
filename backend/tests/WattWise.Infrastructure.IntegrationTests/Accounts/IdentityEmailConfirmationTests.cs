using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityEmailConfirmationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<bool> ConfirmAsync(ServiceProvider provider, Guid id, string token)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailConfirmation>().ConfirmAsync(id, token, Token);
    }

    private async Task<(ServiceProvider Provider, Guid Id)> NewIdentityAsync()
    {
        ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        AccountIdentityIds ids = await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token);
        return (provider, ids.IdentityId);
    }

    [Fact]
    public async Task An_unknown_identity_returns_false()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();

        Assert.False(await ConfirmAsync(provider, Guid.NewGuid(), "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
    }

    [Fact]
    public async Task A_valid_token_confirms_the_email_and_returns_true()
    {
        (ServiceProvider provider, Guid id) = await NewIdentityAsync();
        await using ServiceProvider _ = provider;
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Confirmation);

        bool result = await ConfirmAsync(provider, id, token);

        Assert.Equal(
            (true, true),
            (result, await AccountsTestSupport.InScopeAsync(provider, id, (users, identity) => users.IsEmailConfirmedAsync(identity))));
    }

    [Fact]
    public async Task A_bad_token_returns_false_and_leaves_the_email_unconfirmed()
    {
        (ServiceProvider provider, Guid id) = await NewIdentityAsync();
        await using ServiceProvider _ = provider;
        await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Confirmation);

        bool result = await ConfirmAsync(provider, id, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        Assert.Equal(
            (false, false),
            (result, await AccountsTestSupport.InScopeAsync(provider, id, (users, identity) => users.IsEmailConfirmedAsync(identity))));
    }
}
