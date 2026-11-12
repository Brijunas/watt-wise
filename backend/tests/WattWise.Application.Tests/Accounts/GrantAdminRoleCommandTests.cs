using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class GrantAdminRoleCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Granting_to_a_confirmed_account_adds_the_Admin_role_by_the_normalized_email_and_reports_Changed()
    {
        using RolesHost host = new();

        Result<RoleChange> result = await host.Mediator.Send(new GrantAdminRoleCommand("ada@example.com"), Token);

        Assert.Equal(
            (RoleChange.Changed, $"Find:normalized:ada@example.com, AddRole:{RolesHost.AccountId}:Admin"),
            (result.Value, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task Granting_to_an_account_that_is_already_admin_reports_Unchanged()
    {
        using RolesHost host = new();
        host.Roles.AddResult = false;

        Result<RoleChange> result = await host.Mediator.Send(new GrantAdminRoleCommand("ada@example.com"), Token);

        Assert.Equal(RoleChange.Unchanged, result.Value);
    }

    [Fact]
    public async Task Granting_to_an_unknown_email_is_AccountNotFound_and_adds_nothing()
    {
        using RolesHost host = new();

        Result<RoleChange> result = await host.Mediator.Send(new GrantAdminRoleCommand("nobody@example.com"), Token);

        Assert.Equal(
            (new Error("Accounts.AccountNotFound", "The account does not exist.", ErrorType.NotFound), "Find:normalized:nobody@example.com"),
            (result.Error, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task Granting_to_an_unconfirmed_account_is_EmailNotConfirmed_and_adds_nothing()
    {
        using RolesHost host = new(emailConfirmed: false);

        Result<RoleChange> result = await host.Mediator.Send(new GrantAdminRoleCommand("ada@example.com"), Token);

        Assert.Equal(
            ("Accounts.EmailNotConfirmed", "Find:normalized:ada@example.com"),
            (result.Error.Code, string.Join(", ", host.Calls)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_email_is_a_validation_failure_on_Email_and_calls_no_port(string? email)
    {
        using RolesHost host = new();

        Result<RoleChange> result = await host.Mediator.Send(new GrantAdminRoleCommand(email), Token);

        Assert.Equal(
            ("Validation.Failed", "Email", ""),
            (result.Error.Code, string.Join(",", result.Error.FieldErrors!.Keys), string.Join(", ", host.Calls)));
    }

    [Fact]
    public void The_command_text_hides_the_email()
    {
        Assert.Equal("GrantAdminRoleCommand", new GrantAdminRoleCommand("ada@example.com").ToString());
    }
}
