using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class IdentityAccountRegistrationTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task The_lookup_finds_the_account_of_an_existing_address()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        string email = AccountsTestSupport.UniqueEmail();
        AccountIdentityIds ids = await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        Guid? found = await scope.ServiceProvider.GetRequiredService<IAccountRegistration>().FindAccountIdAsync(email, Token);

        Assert.Equal(ids.AccountId, found);
    }

    [Fact]
    public async Task The_lookup_finds_nothing_for_an_unknown_address()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        Guid? found = await scope.ServiceProvider.GetRequiredService<IAccountRegistration>()
            .FindAccountIdAsync(AccountsTestSupport.UniqueEmail(), Token);

        Assert.Null(found);
    }

    [Fact]
    public async Task Creating_the_identity_saves_the_staged_account_the_identity_and_the_email_request_together()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        Guid accountId;
        bool created;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            IAccountRegistration registration = scope.ServiceProvider.GetRequiredService<IAccountRegistration>();
            accountId = registration.StageAccount(Language.En);
            scope.ServiceProvider.GetRequiredService<IEmailRequests>().Add(EmailRequestKind.EmailConfirmation, accountId);

            created = await registration.CreateIdentityAsync(accountId, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token);
        }

        Assert.Equal((true, "1|1|1"), (created, await RegistrationRows.OfAccountAsync(reader, accountId)));
    }

    [Fact]
    public async Task An_address_that_already_has_an_identity_returns_false()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        string email = AccountsTestSupport.UniqueEmail();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAccountRegistration registration = scope.ServiceProvider.GetRequiredService<IAccountRegistration>();
        Guid accountId = registration.StageAccount(Language.Lt);

        bool created = await registration.CreateIdentityAsync(accountId, email, AccountsTestSupport.ValidPassword(), Token);

        Assert.False(created);
    }

    [Fact]
    public async Task A_unique_violation_from_a_concurrent_request_returns_false_and_saves_nothing_even_when_the_scope_saves_later()
    {
        // Without the user validator the duplicate is only caught by the database, as when another request commits between the check and the insert.
        await using ServiceProvider provider = fixture.BuildAccountServices(configure: services => services.RemoveAll<IUserValidator<AccountIdentity>>());
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        Guid accountId;
        bool created;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            IAccountRegistration registration = scope.ServiceProvider.GetRequiredService<IAccountRegistration>();
            accountId = registration.StageAccount(Language.Lt);
            scope.ServiceProvider.GetRequiredService<IEmailRequests>().Add(EmailRequestKind.EmailConfirmation, accountId);

            created = await registration.CreateIdentityAsync(accountId, email, AccountsTestSupport.ValidPassword(), Token);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
        }

        Assert.Equal((false, "0|0|0"), (created, await RegistrationRows.OfAccountAsync(reader, accountId)));
    }

    [Fact]
    public async Task The_context_is_usable_after_a_unique_violation()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(configure: services => services.RemoveAll<IUserValidator<AccountIdentity>>());
        string email = AccountsTestSupport.UniqueEmail();
        AccountIdentityIds other = await AccountsTestSupport.CreateAccountIdentityAsync(provider, email, AccountsTestSupport.ValidPassword(), Token);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAccountRegistration registration = scope.ServiceProvider.GetRequiredService<IAccountRegistration>();
        Guid accountId = registration.StageAccount(Language.Lt);
        await registration.CreateIdentityAsync(accountId, email, AccountsTestSupport.ValidPassword(), Token);

        Guid? found = await registration.FindAccountIdAsync(email, Token);

        Assert.Equal(other.AccountId, found);
    }

    [Fact]
    public async Task Any_other_database_error_propagates()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAccountRegistration registration = scope.ServiceProvider.GetRequiredService<IAccountRegistration>();

        // An account that was never staged breaks the foreign key, not the unique index.
        Exception? exception = await Record.ExceptionAsync(() =>
            registration.CreateIdentityAsync(Guid.NewGuid(), AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token));

        Assert.Equal("23503:fk_identity_users_accounts_account_id", AccountsTestSupport.ViolationOf(exception));
    }
}
