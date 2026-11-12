using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AccountsQueriesSecurityStampTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    private static async Task<string?> StampAsync(ServiceProvider provider, Guid accountId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountsQueries>().GetSecurityStampAsync(accountId, Token);
    }

    [Fact]
    public async Task The_stamp_is_the_one_stored_on_the_accounts_identity()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());
        string stored = await AccountsTestSupport.InScopeAsync(provider, ids.IdentityId, (users, identity) => users.GetSecurityStampAsync(identity));

        Assert.Equal(stored, await StampAsync(provider, ids.AccountId));
    }

    [Fact]
    public async Task Another_accounts_stamp_is_not_returned()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        AccountSeeder.Ids first = await AccountSeeder.CreateAccountAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());
        AccountSeeder.Ids second = await AccountSeeder.CreateAccountAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword());

        Assert.NotEqual(await StampAsync(provider, first.AccountId), await StampAsync(provider, second.AccountId));
    }

    [Fact]
    public async Task An_unknown_account_has_no_stamp()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);

        Assert.Null(await StampAsync(provider, Guid.NewGuid()));
    }
}
