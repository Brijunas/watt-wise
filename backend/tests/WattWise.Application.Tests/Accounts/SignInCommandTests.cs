using WattWise.Application.Accounts;
using WattWise.Application.Results;

namespace WattWise.Application.Tests.Accounts;

public class SignInCommandTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lockout_answers_the_time_remaining_at_the_time_of_the_check()
    {
        using SessionsHost host = new(PasswordCheck.LockedOut(NodaTime.Instant.FromUtc(2026, 11, 11, 12, 15)));
        host.Clock.Advance(NodaTime.Duration.FromMilliseconds(1250));

        Result<SignedIn> result = await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal(NodaTime.Duration.FromMilliseconds(898750), result.Error.RetryAfter);
    }

    [Fact]
    public async Task A_successful_sign_in_checks_the_normalized_address_stores_one_session_and_issues_the_token_after_saving()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));
        host.Queries.Permissions = ["a.read", "b.write"];

        await host.Mediator.Send(new SignInCommand("Ada@Example.com", "pw", "agent"), Token);

        Assert.Equal(
            [
                "Check:normalized:Ada@Example.com:pw",
                "AddSession",
                "Save",
                $"Issue:{AccountId}:a.read,b.write:2026-11-11T12:00:00Z",
            ],
            host.Calls);
    }

    [Fact]
    public async Task A_successful_sign_in_returns_both_tokens_with_their_expiries()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        Result<SignedIn> result = await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal(
            new SignedIn("jwt", NodaTime.Instant.FromUtc(2026, 11, 11, 12, 15), "token-1", NodaTime.Instant.FromUtc(2026, 11, 18, 12, 0)),
            result.Value);
    }

    [Fact]
    public async Task The_session_belongs_to_the_account_carries_the_device_label_and_holds_the_hash_of_the_refresh_token()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "Mozilla/5.0"), Token);

        WattWise.Domain.Accounts.Session session = Assert.Single(host.Sessions.Stored);
        // 746F6B656E2D31 is "token-1" in UTF-8.
        Assert.Equal(
            (AccountId, "label:Mozilla/5.0", "746F6B656E2D31"),
            (session.AccountId, session.DeviceLabel, Convert.ToHexString(session.RefreshTokens.Single().TokenHash)));
    }

    [Fact]
    public async Task The_session_carries_the_security_stamp_the_password_check_verified()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp-x"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal("stamp-x", host.Sessions.Stored.Single().SecurityStamp);
    }

    [Fact]
    public void The_text_of_a_successful_password_check_hides_the_stamp()
    {
        Assert.Equal("PasswordCheck Succeeded", PasswordCheck.Succeeded(AccountId, "secret-stamp").ToString());
    }

    [Fact]
    public async Task The_access_token_carries_the_id_of_the_stored_session()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal(host.Sessions.Stored.Single().Id, host.AccessTokens.SessionIds.Single());
    }

    [Fact]
    public async Task A_missing_user_agent_is_passed_on_to_the_label_port_as_null()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", null), Token);

        Assert.Equal("label:", host.Sessions.Stored.Single().DeviceLabel);
    }

    [Fact]
    public async Task A_sign_in_with_no_permissions_issues_a_token_with_none()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal($"Issue:{AccountId}::2026-11-11T12:00:00Z", host.Calls[^1]);
    }

    [Fact]
    public async Task The_password_check_is_asked_before_anything_else_is_stored()
    {
        using SessionsHost host = new(PasswordCheck.InvalidCredentials());

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal(["Check:normalized:ada@example.com:pw"], host.Calls);
    }

    public static TheoryData<PasswordCheck, Error> Failures => new()
    {
        { PasswordCheck.InvalidCredentials(), new Error("Accounts.InvalidCredentials", "The email or password is wrong.", ErrorType.Unauthorized) },
        { PasswordCheck.EmailNotConfirmed(), new Error("Accounts.EmailNotConfirmed", "The email address is not confirmed.", ErrorType.Forbidden) },
        { PasswordCheck.LockedOut(SessionsHost.Now + NodaTime.Duration.FromMinutes(15)), new Error("Accounts.LockedOut", "Too many failed sign-ins; try again later.", ErrorType.Locked, RetryAfter: NodaTime.Duration.FromMinutes(15)) },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Each_failed_check_answers_its_own_error_and_stores_no_session(PasswordCheck check, Error expected)
    {
        using SessionsHost host = new(check);

        Result<SignedIn> result = await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent"), Token);

        Assert.Equal((expected, 0), (result.Error, host.Sessions.Stored.Count));
    }

    [Fact]
    public void The_command_text_hides_the_password()
    {
        Assert.Equal("SignInCommand", new SignInCommand("ada@example.com", "hunter2", "agent").ToString());
    }

    private static WattWise.Domain.Accounts.Session SeededSession(Guid accountId, string token) =>
        WattWise.Domain.Accounts.Session.Start(
            accountId, "Old device", "stamp", System.Text.Encoding.UTF8.GetBytes(token), NodaTime.Instant.FromUtc(2026, 11, 10, 12, 0), NodaTime.Instant.FromUtc(2026, 11, 17, 12, 0));

    [Fact]
    public async Task Signing_in_with_the_cookie_of_an_older_session_of_the_same_account_ends_that_session_after_saving_the_new_one()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));
        host.Sessions.Stored.Add(SeededSession(AccountId, "old-token"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent", "old-token"), Token);

        Assert.Equal(
            "AddSession, Save, RemoveSession, Save | label:agent",
            string.Join(", ", host.Calls.Where(call => call is "AddSession" or "Save" or "RemoveSession"))
                + " | " + string.Join(",", host.Sessions.Stored.Select(session => session.DeviceLabel)));
    }

    [Fact]
    public async Task Signing_in_with_the_cookie_of_another_accounts_session_leaves_that_session_alone()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));
        host.Sessions.Stored.Add(SeededSession(Guid.Parse("00000000-0000-0000-0000-0000000000b2"), "other-token"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent", "other-token"), Token);

        Assert.Equal("AddSession, Save", string.Join(", ", host.Calls.Where(call => call is "AddSession" or "Save" or "RemoveSession")));
    }

    [Fact]
    public async Task Signing_in_with_an_unknown_cookie_deletes_nothing()
    {
        using SessionsHost host = new(PasswordCheck.Succeeded(AccountId, "stamp"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent", "unknown-token"), Token);

        Assert.Equal("AddSession, Save", string.Join(", ", host.Calls.Where(call => call is "AddSession" or "Save" or "RemoveSession")));
    }

    [Fact]
    public async Task A_failed_sign_in_with_a_cookie_does_not_end_the_session_of_the_cookie()
    {
        using SessionsHost host = new(PasswordCheck.InvalidCredentials());
        host.Sessions.Stored.Add(SeededSession(AccountId, "old-token"));

        await host.Mediator.Send(new SignInCommand("ada@example.com", "pw", "agent", "old-token"), Token);

        Assert.Single(host.Sessions.Stored);
    }
}
