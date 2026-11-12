using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AdminRoleGrantTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string AdminRoleId = "0192e5a0-0000-7000-8000-000000000001";

    private static Task<AccountSeeder.Ids> NewAccountAsync(ServiceProvider api, string email, bool confirmed = true) =>
        AccountSeeder.CreateAccountAsync(api, email, AccountsTestSupport.ValidPassword(), confirmed);

    [Fact]
    public async Task Granting_adds_the_seeded_Admin_role_to_the_account_and_reports_Changed()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(api, email);

        Result<RoleChange> result = await AdminRoleSupport.GrantAsync(cli, email);

        Assert.Equal((RoleChange.Changed, AdminRoleId), (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId)));
    }

    [Fact]
    public async Task Granting_again_reports_Unchanged_and_keeps_one_row()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(api, email);
        await AdminRoleSupport.GrantAsync(cli, email);

        Result<RoleChange> result = await AdminRoleSupport.GrantAsync(cli, email);

        Assert.Equal((RoleChange.Unchanged, AdminRoleId), (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId)));
    }

    [Fact]
    public async Task The_email_is_matched_whatever_its_case_and_surrounding_spaces()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(api, email);

        Result<RoleChange> result = await AdminRoleSupport.GrantAsync(cli, $" {email.ToUpperInvariant()} ");

        Assert.Equal((RoleChange.Changed, AdminRoleId), (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId)));
    }

    [Fact]
    public async Task An_unknown_email_fails_with_AccountNotFound_and_gives_no_other_account_the_role()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        AccountSeeder.Ids bystander = await NewAccountAsync(api, AccountsTestSupport.UniqueEmail());

        Result<RoleChange> result = await AdminRoleSupport.GrantAsync(cli, AccountsTestSupport.UniqueEmail());

        Assert.Equal(("Accounts.AccountNotFound", ""), (result.Error.Code, await AdminRoleSupport.RoleIdsOfAsync(cli, bystander.AccountId)));
    }

    [Fact]
    public async Task An_unconfirmed_email_fails_with_EmailNotConfirmed_and_the_account_gets_no_role()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await NewAccountAsync(api, email, confirmed: false);

        Result<RoleChange> result = await AdminRoleSupport.GrantAsync(cli, email);

        Assert.Equal(("Accounts.EmailNotConfirmed", ""), (result.Error.Code, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId)));
    }
}
