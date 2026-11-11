using Microsoft.Extensions.DependencyInjection;

using WattWise.Application;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class UnitOfWorkTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Saving_persists_the_changes_staged_in_the_scope()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Domain.Accounts.Account account = AccountsTestSupport.NewAccount();
            db.Accounts.Add(account);
            accountId = account.Id;

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(AccountsTestSupport.Token);
        }

        Assert.Equal("1|0|0", await RegistrationRows.OfAccountAsync(reader, accountId));
    }
}
