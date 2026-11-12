using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class RevokeAdminRoleCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Revoking_removes_the_role_and_ends_every_session_in_one_transaction_in_that_order()
    {
        using RolesHost host = new();

        Result<RoleChange> result = await host.Mediator.Send(new RevokeAdminRoleCommand("ada@example.com"), Token);

        Assert.Equal(
            (
                RoleChange.Changed,
                $"Find:normalized:ada@example.com, Begin, RemoveRole:{RolesHost.AccountId}:Admin, EndAll:{RolesHost.AccountId}, Commit"),
            (result.Value, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task Revoking_from_an_account_without_the_role_reports_Unchanged_and_ends_no_session()
    {
        using RolesHost host = new();
        host.Roles.RemoveResult = false;

        Result<RoleChange> result = await host.Mediator.Send(new RevokeAdminRoleCommand("ada@example.com"), Token);

        Assert.Equal(
            (RoleChange.Unchanged, $"Find:normalized:ada@example.com, Begin, RemoveRole:{RolesHost.AccountId}:Admin, Commit"),
            (result.Value, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task Revoking_from_an_unknown_email_is_AccountNotFound_and_changes_nothing()
    {
        using RolesHost host = new();

        Result<RoleChange> result = await host.Mediator.Send(new RevokeAdminRoleCommand("nobody@example.com"), Token);

        Assert.Equal(
            (new Error("Accounts.AccountNotFound", "The account does not exist.", ErrorType.NotFound), "Find:normalized:nobody@example.com"),
            (result.Error, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task Revoking_from_an_unconfirmed_account_is_EmailNotConfirmed_and_changes_nothing()
    {
        using RolesHost host = new(emailConfirmed: false);

        Result<RoleChange> result = await host.Mediator.Send(new RevokeAdminRoleCommand("ada@example.com"), Token);

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

        Result<RoleChange> result = await host.Mediator.Send(new RevokeAdminRoleCommand(email), Token);

        Assert.Equal(
            ("Validation.Failed", "Email", ""),
            (result.Error.Code, string.Join(",", result.Error.FieldErrors!.Keys), string.Join(", ", host.Calls)));
    }

    [Fact]
    public void The_command_text_hides_the_email()
    {
        Assert.Equal("RevokeAdminRoleCommand", new RevokeAdminRoleCommand("ada@example.com").ToString());
    }
}
