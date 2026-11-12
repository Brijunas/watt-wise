using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Tests.Accounts;

public class ChangePasswordCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChangePasswordCommand Command(string? userAgent = "agent") => new(AccountId, "old-pw", "new-pw", userAgent);

    [Fact]
    public async Task A_change_checks_the_current_password_then_changes_ends_all_sessions_and_starts_one_inside_one_transaction()
    {
        using SessionsHost host = new();
        host.Queries.Permissions = ["a.read"];

        await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            [
                $"CheckCurrent:{AccountId}:old-pw",
                "Begin",
                $"Change:{AccountId}:old-pw:new-pw",
                $"EndAll:{AccountId}",
                "AddSession",
                "Save",
                $"Issue:{AccountId}:a.read:2026-11-11T12:00:00Z",
                "Commit",
            ],
            host.Calls);
    }

    [Fact]
    public async Task A_change_answers_new_tokens_valid_from_now()
    {
        using SessionsHost host = new();

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            new SignedIn("jwt", Instant.FromUtc(2026, 11, 11, 12, 15), "token-1", Instant.FromUtc(2026, 11, 18, 12, 0)),
            result.Value);
    }

    [Fact]
    public async Task The_new_session_carries_the_new_security_stamp_the_device_label_and_the_id_in_the_access_token()
    {
        using SessionsHost host = new();
        host.PasswordChange.ChangeAnswer = PasswordChangeOutcome.Changed("stamp-after-change");

        await host.Mediator.Send(Command("Mozilla/5.0"), Token);

        Session session = Assert.Single(host.Sessions.Stored);
        Assert.Equal(
            (AccountId, "label:Mozilla/5.0", "stamp-after-change", session.Id),
            (session.AccountId, session.DeviceLabel, session.SecurityStamp, host.AccessTokens.SessionIds.Single()));
    }

    public static TheoryData<CurrentPasswordCheck, string, ErrorType> RefusedChecks => new()
    {
        { CurrentPasswordCheck.WrongPassword, "Accounts.InvalidCurrentPassword", ErrorType.Validation },
        { CurrentPasswordCheck.LockedOut, "Accounts.LockedOut", ErrorType.Locked },
        { CurrentPasswordCheck.NotFound, "Accounts.AccountNotFound", ErrorType.NotFound },
    };

    [Theory]
    [MemberData(nameof(RefusedChecks))]
    public async Task A_refused_current_password_check_answers_its_error_and_never_opens_the_transaction_or_touches_a_session(
        CurrentPasswordCheck check, string code, ErrorType type)
    {
        using SessionsHost host = new();
        host.PasswordChange.CheckAnswer = check;
        host.Sessions.Stored.Add(SeededSession());

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            (code, type, $"CheckCurrent:{AccountId}:old-pw", 1),
            (result.Error.Code, result.Error.Type, string.Join(" > ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_wrong_current_password_answers_a_field_error_on_the_current_password()
    {
        using SessionsHost host = new();
        host.PasswordChange.CheckAnswer = CurrentPasswordCheck.WrongPassword;

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(["The current password is wrong."], result.Error.FieldErrors!["CurrentPassword"]);
    }

    public static TheoryData<PasswordChangeOutcome, string, ErrorType> FailedChanges => new()
    {
        { PasswordChangeOutcome.WrongPassword(), "Accounts.InvalidCurrentPassword", ErrorType.Validation },
        { PasswordChangeOutcome.NotFound(), "Accounts.AccountNotFound", ErrorType.NotFound },
        { PasswordChangeOutcome.Conflict(), "Accounts.PasswordChangeConflict", ErrorType.Conflict },
    };

    [Theory]
    [MemberData(nameof(FailedChanges))]
    public async Task A_failed_change_rolls_back_and_neither_ends_nor_starts_a_session(
        PasswordChangeOutcome outcome, string code, ErrorType type)
    {
        using SessionsHost host = new();
        host.PasswordChange.ChangeAnswer = outcome;
        host.Sessions.Stored.Add(SeededSession());

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            (code, type, $"CheckCurrent:{AccountId}:old-pw > Begin > Change:{AccountId}:old-pw:new-pw > Rollback", 1),
            (result.Error.Code, result.Error.Type, string.Join(" > ", host.Calls), host.Sessions.Stored.Count));
    }

    [Fact]
    public async Task A_session_save_that_loses_a_race_answers_a_conflict_and_rolls_back()
    {
        using SessionsHost host = new();
        host.ConflictNextSaves(1);

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal(
            (
                "Accounts.PasswordChangeConflict",
                ErrorType.Conflict,
                "The account was changed by another request; try again.",
                $"CheckCurrent:{AccountId}:old-pw > Begin > Change:{AccountId}:old-pw:new-pw > EndAll:{AccountId} > AddSession > SaveConflict > Rollback"),
            (result.Error.Code, result.Error.Type, result.Error.Description, string.Join(" > ", host.Calls)));
    }

    [Fact]
    public async Task A_rule_breaking_new_password_is_refused_before_anything_runs()
    {
        using SessionsHost host = new(passwordMessages: "too short");

        Result<SignedIn> result = await host.Mediator.Send(Command(), Token);

        Assert.Equal((ErrorType.Validation, "Validation.Failed", 0), (result.Error.Type, result.Error.Code, host.Calls.Count));
    }

    [Fact]
    public void The_command_text_hides_both_passwords()
    {
        Assert.Equal("ChangePasswordCommand", new ChangePasswordCommand(AccountId, "hunter2", "hunter3", "agent").ToString());
    }

    private static Session SeededSession() =>
        Session.Start(AccountId, "Old device", "stamp", System.Text.Encoding.UTF8.GetBytes("old-token"), Instant.FromUtc(2026, 11, 10, 12, 0), Instant.FromUtc(2026, 11, 17, 12, 0));
}
