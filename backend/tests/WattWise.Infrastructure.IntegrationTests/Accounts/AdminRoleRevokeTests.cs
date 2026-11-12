using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class AdminRoleRevokeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant SignedInAt = Instant.FromUtc(2026, 11, 11, 12, 0);

    private static async Task<(AccountSeeder.Ids Ids, string Email, Guid SessionId)> AdminWithSessionAsync(
        ServiceProvider api, bool confirmed = true)
    {
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(api, email, AccountsTestSupport.ValidPassword(), confirmed);
        await AccountSeeder.AssignRoleAsync(api, ids.AccountId, RoleNames.AdminId);
        return (ids, email, await AddSessionAsync(api, ids.AccountId));
    }

    private static async Task<Guid> AddSessionAsync(ServiceProvider api, Guid accountId)
    {
        Session session = SessionSeeder.Start(accountId, "Firefox on Linux", SignedInAt, SignedInAt + Duration.FromDays(7));
        await SessionSeeder.AddAsync(api, session);
        return session.Id;
    }

    [Fact]
    public async Task Revoking_removes_the_role_row_and_deletes_the_accounts_sessions_with_their_tokens()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        (AccountSeeder.Ids ids, string email, Guid sessionId) = await AdminWithSessionAsync(api);

        Result<RoleChange> result = await AdminRoleSupport.RevokeAsync(cli, email);

        Assert.Equal(
            (RoleChange.Changed, "", "0|0"),
            (result.Value, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task Revoking_leaves_another_accounts_role_and_sessions()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        (_, string email, _) = await AdminWithSessionAsync(api);
        (AccountSeeder.Ids otherIds, _, Guid otherSessionId) = await AdminWithSessionAsync(api);

        await AdminRoleSupport.RevokeAsync(cli, email);

        Assert.Equal(
            ("0192e5a0-0000-7000-8000-000000000001", "1|1"),
            (await AdminRoleSupport.RoleIdsOfAsync(cli, otherIds.AccountId), await SessionRows.OfSessionAsync(cli, otherSessionId)));
    }

    [Fact]
    public async Task Revoking_from_an_account_without_the_role_reports_Unchanged_and_keeps_its_sessions()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        string email = AccountsTestSupport.UniqueEmail();
        AccountSeeder.Ids ids = await AccountSeeder.CreateAccountAsync(api, email, AccountsTestSupport.ValidPassword());
        Guid sessionId = await AddSessionAsync(api, ids.AccountId);

        Result<RoleChange> result = await AdminRoleSupport.RevokeAsync(cli, email);

        Assert.Equal((RoleChange.Unchanged, "1|1"), (result.Value, await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task Revoking_an_unknown_email_fails_with_AccountNotFound_and_ends_no_other_accounts_session()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        (AccountSeeder.Ids ids, _, Guid sessionId) = await AdminWithSessionAsync(api);

        Result<RoleChange> result = await AdminRoleSupport.RevokeAsync(cli, AccountsTestSupport.UniqueEmail());

        Assert.Equal(
            ("Accounts.AccountNotFound", "0192e5a0-0000-7000-8000-000000000001", "1|1"),
            (result.Error.Code, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task Revoking_an_unconfirmed_email_fails_with_EmailNotConfirmed_and_keeps_the_role_and_sessions()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        await using ServiceProvider cli = fixture.BuildServices(DatabaseRole.Cli);
        (AccountSeeder.Ids ids, string email, Guid sessionId) = await AdminWithSessionAsync(api, confirmed: false);

        Result<RoleChange> result = await AdminRoleSupport.RevokeAsync(cli, email);

        Assert.Equal(
            ("Accounts.EmailNotConfirmed", "0192e5a0-0000-7000-8000-000000000001", "1|1"),
            (result.Error.Code, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }

    [Fact]
    public async Task Revoking_rolls_the_role_removal_back_when_ending_the_sessions_fails()
    {
        await using ServiceProvider api = fixture.BuildAccountServices();
        FailingCommandInterceptor fault = new();
        await using ServiceProvider cli = fixture.BuildServices(
            DatabaseRole.Cli, services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(fault)));
        (AccountSeeder.Ids ids, string email, Guid sessionId) = await AdminWithSessionAsync(api);
        fault.Arm("DELETE FROM app.sessions");

        Exception? thrown = await Record.ExceptionAsync(() => AdminRoleSupport.RevokeAsync(cli, email));

        Assert.Equal(
            ("Injected fault.", "0192e5a0-0000-7000-8000-000000000001", "1|1"),
            (thrown?.Message, await AdminRoleSupport.RoleIdsOfAsync(cli, ids.AccountId), await SessionRows.OfSessionAsync(cli, sessionId)));
    }
}
