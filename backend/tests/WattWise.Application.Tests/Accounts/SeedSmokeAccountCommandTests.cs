using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class SeedSmokeAccountCommandTests
{
    private const string Password = "Aa1!secret";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // admin, what the account port found, whether it replaced the password, what the role port answers, the outcome, the calls (ACC is the account id).
    [Theory]
    [InlineData(true, SeedOutcome.Created, false, true, SeedOutcome.Created,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, AddRole:ACC:Admin, Commit")]
    [InlineData(true, SeedOutcome.Unchanged, false, true, SeedOutcome.Changed,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, AddRole:ACC:Admin, Commit")]
    [InlineData(true, SeedOutcome.Unchanged, false, false, SeedOutcome.Unchanged,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, AddRole:ACC:Admin, Commit")]
    [InlineData(true, SeedOutcome.Changed, false, true, SeedOutcome.Changed,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, AddRole:ACC:Admin, Commit")]
    [InlineData(true, SeedOutcome.Changed, true, false, SeedOutcome.Changed,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, AddRole:ACC:Admin, EndAll:ACC, Commit")]
    [InlineData(false, SeedOutcome.Created, false, false, SeedOutcome.Created,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, RemoveRole:ACC:Admin, Commit")]
    [InlineData(false, SeedOutcome.Unchanged, false, true, SeedOutcome.Changed,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, RemoveRole:ACC:Admin, EndAll:ACC, Commit")]
    [InlineData(false, SeedOutcome.Unchanged, false, false, SeedOutcome.Unchanged,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, RemoveRole:ACC:Admin, Commit")]
    [InlineData(false, SeedOutcome.Changed, true, false, SeedOutcome.Changed,
        "Check:Aa1!secret, Begin, Ensure:normalized:ada@example.com:Aa1!secret, RemoveRole:ACC:Admin, EndAll:ACC, Commit")]
    public async Task The_role_and_the_sessions_follow_the_wanted_role_and_the_password_change(
        bool admin, SeedOutcome ensured, bool passwordChanged, bool roleChanged, SeedOutcome expected, string expectedCalls)
    {
        using SmokeHost host = new();
        host.Accounts.Result = new EnsuredAccount(SmokeHost.AccountId, ensured, passwordChanged);
        host.Roles.AddResult = roleChanged;
        host.Roles.RemoveResult = roleChanged;

        Result<SeedOutcome> result = await host.Mediator.Send(new SeedSmokeAccountCommand("ada@example.com", Password, admin), Token);

        Assert.Equal(
            (expected, expectedCalls.Replace("ACC", SmokeHost.AccountId.ToString(), StringComparison.Ordinal)),
            (result.Value, string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task A_password_the_policy_rejects_is_a_validation_failure_on_Password_and_touches_no_account()
    {
        using SmokeHost host = new("Too short.");

        Result<SeedOutcome> result = await host.Mediator.Send(new SeedSmokeAccountCommand("ada@example.com", "x", Admin: true), Token);

        Assert.Equal(
            ("Validation.Failed", "Password", "Check:x"),
            (result.Error.Code, string.Join(",", result.Error.FieldErrors!.Keys), string.Join(", ", host.Calls)));
    }

    [Fact]
    public async Task An_invalid_email_is_a_validation_failure_on_Email_and_touches_no_account()
    {
        using SmokeHost host = new();

        Result<SeedOutcome> result = await host.Mediator.Send(new SeedSmokeAccountCommand("not-an-email", Password, Admin: false), Token);

        Assert.Equal(
            ("Validation.Failed", "Email", $"Check:{Password}"),
            (result.Error.Code, string.Join(",", result.Error.FieldErrors!.Keys), string.Join(", ", host.Calls)));
    }

    [Fact]
    public void The_command_text_hides_the_email_and_the_password()
    {
        Assert.Equal("SeedSmokeAccountCommand", new SeedSmokeAccountCommand("ada@example.com", Password, Admin: true).ToString());
    }
}
