using WattWise.Application.Accounts;
using WattWise.Application.Results;

using Unit = Mediator.Unit;

namespace WattWise.Application.Tests.Accounts;

public class ResetPasswordCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid IdentityId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ResetPasswordCommand Command() => new(IdentityId.ToString(), "link-token", "new-pw");

    [Fact]
    public async Task A_valid_link_resets_then_ends_all_sessions_of_the_account_inside_one_transaction()
    {
        using SessionsHost host = new();
        host.PasswordReset.Answer = PasswordResetOutcome.Reset(AccountId);

        Result<Unit> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            (true, $"Begin > Reset:{IdentityId}:link-token:new-pw > EndAll:{AccountId} > Commit"),
            (result.IsSuccess, string.Join(" > ", host.Calls)));
    }

    [Fact]
    public async Task An_invalid_link_rolls_back_ends_no_session_and_answers_InvalidEmailLink()
    {
        using SessionsHost host = new();
        host.PasswordReset.Answer = PasswordResetOutcome.InvalidLink();

        Result<Unit> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            (new Error("Accounts.InvalidEmailLink", "The link is invalid or has expired.", ErrorType.BadRequest), $"Begin > Reset:{IdentityId}:link-token:new-pw > Rollback"),
            (result.Error, string.Join(" > ", host.Calls)));
    }

    [Fact]
    public async Task A_rule_breaking_new_password_is_refused_before_the_transaction_opens()
    {
        using SessionsHost host = new(passwordMessages: "too short");

        Result<Unit> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(("Validation.Failed", 0), (result.Error.Code, host.Calls.Count));
    }

    [Fact]
    public void The_command_text_hides_the_token_and_the_password()
    {
        Assert.Equal("ResetPasswordCommand", new ResetPasswordCommand("id", "secret-token", "hunter2").ToString());
    }
}
