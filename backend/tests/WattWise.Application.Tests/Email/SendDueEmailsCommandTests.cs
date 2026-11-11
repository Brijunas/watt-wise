using Microsoft.Extensions.Logging;

using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

public class SendDueEmailsCommandTests : IDisposable
{
    private readonly EmailHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task A_pass_recovers_stuck_rows_then_claims_then_sends_then_records()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 1);
        _host.Outbox.ToClaim.Add(ready);

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(["recover", "claim", "send", $"sent {ready.RequestId}"], _host.Calls);
    }

    [Fact]
    public async Task Stuck_rows_are_recovered_after_five_minutes_with_each_kinds_attempts()
    {
        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        var (stuckAfter, maxAttempts) = _host.Outbox.Recovery!.Value;
        Assert.Equal(
            (300.0, "PasswordReset=4,EmailConfirmation=6,AccountAlreadyExists=3"),
            (stuckAfter.TotalSeconds, string.Join(",", new[] { EmailRequestKind.PasswordReset, EmailRequestKind.EmailConfirmation, EmailRequestKind.AccountAlreadyExists }.Select(kind => $"{kind}={maxAttempts[kind]}"))));
    }

    [Fact]
    public async Task A_pass_claims_at_most_20_rows()
    {
        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(20, _host.Outbox.BatchSize);
    }

    [Fact]
    public async Task When_nothing_is_due_nothing_is_sent_or_recorded()
    {
        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(["recover", "claim"], _host.Calls);
    }

    [Fact]
    public async Task The_message_id_local_part_is_the_row_id_and_the_attempt_number()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 3);
        _host.Outbox.ToClaim.Add(ready);

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal([(ready.Message, $"{ready.RequestId}.3")], _host.Sender.Sent);
    }

    [Fact]
    public async Task A_sent_message_is_recorded_as_sent()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.PasswordReset, 1);
        _host.Outbox.ToClaim.Add(ready);

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal($"sent {ready.RequestId}", _host.Calls[^1]);
    }

    [Theory]
    [InlineData(EmailRequestKind.EmailConfirmation, 1, "2026-11-11T12:01:00Z")]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 2, "2026-11-11T12:30:00Z")]
    public async Task A_retryable_result_with_attempts_left_goes_back_to_pending_after_the_next_delay(
        EmailRequestKind kind, int attempt, string expectedNextAttempt)
    {
        ReadyEmail ready = _host.Ready(kind, attempt);
        _host.Outbox.ToClaim.Add(ready);
        _host.Sender.Answer(ready.Message.To, SendResult.Retryable("SmtpCommandException 451"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal($"retry {ready.RequestId} {expectedNextAttempt} SmtpCommandException 451", _host.Calls[^1]);
    }

    [Fact]
    public async Task A_retryable_result_on_the_last_attempt_fails_the_row()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.AccountAlreadyExists, 3);
        _host.Outbox.ToClaim.Add(ready);
        _host.Sender.Answer(ready.Message.To, SendResult.Retryable("SmtpCommandException 451"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal($"failed {ready.RequestId} SmtpCommandException 451", _host.Calls[^1]);
    }

    [Fact]
    public async Task A_permanent_result_fails_the_row_even_with_attempts_left()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 1);
        _host.Outbox.ToClaim.Add(ready);
        _host.Sender.Answer(ready.Message.To, SendResult.Permanent("SmtpCommandException 550"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal($"failed {ready.RequestId} SmtpCommandException 550", _host.Calls[^1]);
    }

    [Fact]
    public async Task A_bug_in_one_row_fails_it_with_the_exception_type_and_the_other_rows_are_still_sent()
    {
        ReadyEmail first = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "first@example.com");
        ReadyEmail broken = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "broken@example.com");
        ReadyEmail third = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "third@example.com");
        _host.Outbox.ToClaim.AddRange([first, broken, third]);
        _host.Sender.Throw(broken.Message.To, new InvalidOperationException("template broke for broken@example.com"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(
            [$"sent {first.RequestId}", $"failed {broken.RequestId} InvalidOperationException", $"sent {third.RequestId}"],
            _host.Calls.Where(call => call.StartsWith("sent", StringComparison.Ordinal) || call.StartsWith("failed", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_bug_is_logged_at_error_without_the_recipient()
    {
        ReadyEmail broken = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "broken@example.com");
        _host.Outbox.ToClaim.Add(broken);
        _host.Sender.Throw(broken.Message.To, new InvalidOperationException("template broke for broken@example.com"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        string[] errors = [.. _host.Logs.GetSnapshot().Where(record => record.Level == LogLevel.Error).Select(record => record.Message)];
        Assert.Equal((1, false), (errors.Length, errors.Any(message => message.Contains("broken@example.com", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task The_response_counts_what_was_sent_retried_and_failed()
    {
        ReadyEmail sent = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "sent@example.com");
        ReadyEmail retried = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "retried@example.com");
        ReadyEmail failed = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "failed@example.com");
        _host.Outbox.ToClaim.AddRange([sent, retried, failed, _host.Ready(EmailRequestKind.EmailConfirmation, 1, "sent2@example.com")]);
        _host.Sender.Answer("retried@example.com", SendResult.Retryable("SmtpCommandException 451"));
        _host.Sender.Answer("failed@example.com", SendResult.Permanent("SmtpCommandException 550"));

        var result = await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(new SendDueEmailsResponse(2, 1, 1), result.Value);
    }

    // Shutdown, not a bug: the row stays "sending" for the next pass's stuck-row recovery.
    [Fact]
    public async Task Cancellation_is_not_swallowed_and_the_row_is_not_recorded()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 1);
        _host.Outbox.ToClaim.Add(ready);
        _host.Sender.Throw(ready.Message.To, new OperationCanceledException());

        Exception? exception = await Record.ExceptionAsync(async () => await _host.Mediator.Send(new SendDueEmailsCommand(), Token));

        Assert.Equal((true, "recover,claim,send"), (exception is OperationCanceledException, string.Join(",", _host.Calls)));
    }

    // The message was delivered: marking the row failed would hide that and invite a resend by hand.
    [Fact]
    public async Task A_failure_recording_a_sent_message_propagates_and_does_not_fail_the_row()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 1);
        _host.Outbox.ToClaim.Add(ready);
        _host.Outbox.RecordSentFailure = new InvalidOperationException("database is down");

        Exception? exception = await Record.ExceptionAsync(async () => await _host.Mediator.Send(new SendDueEmailsCommand(), Token));

        Assert.Equal(
            (typeof(InvalidOperationException), "recover,claim,send,sent " + ready.RequestId),
            (exception?.GetType(), string.Join(",", _host.Calls)));
    }

    [Fact]
    public async Task Every_record_carries_the_attempt_number_that_was_claimed()
    {
        ReadyEmail sent = _host.Ready(EmailRequestKind.EmailConfirmation, 3, "sent@example.com");
        ReadyEmail retried = _host.Ready(EmailRequestKind.EmailConfirmation, 2, "retried@example.com");
        ReadyEmail failed = _host.Ready(EmailRequestKind.EmailConfirmation, 5, "failed@example.com");
        _host.Outbox.ToClaim.AddRange([sent, retried, failed]);
        _host.Sender.Answer("retried@example.com", SendResult.Retryable("SmtpCommandException 451"));
        _host.Sender.Answer("failed@example.com", SendResult.Permanent("SmtpCommandException 550"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal([3, 2, 5], _host.Outbox.RecordedAttempts);
    }

    [Fact]
    public async Task A_bug_record_also_carries_the_claimed_attempt_number()
    {
        ReadyEmail broken = _host.Ready(EmailRequestKind.EmailConfirmation, 4, "broken@example.com");
        _host.Outbox.ToClaim.Add(broken);
        _host.Sender.Throw(broken.Message.To, new InvalidOperationException("template broke"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal([4], _host.Outbox.RecordedAttempts);
    }

    // The row was recovered or reclaimed while the send ran: the late result changes nothing, which is not an error.
    [Fact]
    public async Task A_result_for_a_row_that_had_moved_on_is_logged_at_warning_with_the_kind_only()
    {
        _host.Outbox.ToClaim.Add(_host.Ready(EmailRequestKind.PasswordReset, 1, "ada@example.com"));
        _host.Outbox.RecordMatches = false;

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(
            ["Email PasswordReset result not recorded: the row had moved on"],
            _host.Logs.GetSnapshot().Where(record => record.Level == LogLevel.Warning).Select(record => record.Message));
    }

    [Fact]
    public async Task A_result_that_matched_its_row_logs_no_warning()
    {
        _host.Outbox.ToClaim.Add(_host.Ready(EmailRequestKind.PasswordReset, 1));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.DoesNotContain(_host.Logs.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_permanent_failure_is_logged_at_error_with_the_kind_and_language_only()
    {
        ReadyEmail ready = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "ada@example.com", WattWise.Domain.Localization.Language.En);
        _host.Outbox.ToClaim.Add(ready);
        _host.Sender.Answer(ready.Message.To, SendResult.Permanent("SmtpCommandException 550"));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(
            ["Email EmailConfirmation in En failed for good"],
            _host.Logs.GetSnapshot().Where(record => record.Level == LogLevel.Error).Select(record => record.Message));
    }

    // No new send may start later than 3 minutes after the claim, so a send that starts in time ends before the 5-minute recovery.
    [Theory]
    [InlineData(180, 3)]
    [InlineData(181, 1)]
    public async Task No_send_starts_more_than_three_minutes_after_the_claim(int secondsTheFirstSendTakes, int expectedSends)
    {
        _host.Outbox.ToClaim.AddRange(
        [
            _host.Ready(EmailRequestKind.EmailConfirmation, 1, "a@example.com"),
            _host.Ready(EmailRequestKind.EmailConfirmation, 1, "b@example.com"),
            _host.Ready(EmailRequestKind.EmailConfirmation, 1, "c@example.com"),
        ]);
        bool slow = true;
        _host.Sender.OnSend = () =>
        {
            if (slow)
            {
                slow = false;
                _host.Clock.Advance(NodaTime.Duration.FromSeconds(secondsTheFirstSendTakes));
            }
        };

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(expectedSends, _host.Sender.Sent.Count);
    }

    [Fact]
    public async Task Rows_skipped_after_the_send_window_are_not_recorded_so_they_stay_sending()
    {
        ReadyEmail first = _host.Ready(EmailRequestKind.EmailConfirmation, 1, "a@example.com");
        _host.Outbox.ToClaim.AddRange([first, _host.Ready(EmailRequestKind.EmailConfirmation, 1, "b@example.com")]);
        _host.Sender.OnSend = () => _host.Clock.Advance(NodaTime.Duration.FromSeconds(181));

        await _host.Mediator.Send(new SendDueEmailsCommand(), Token);

        Assert.Equal(["recover", "claim", "send", $"sent {first.RequestId}"], _host.Calls);
    }
}
