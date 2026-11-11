using NodaTime;

using WattWise.Application.Email;

namespace WattWise.Application.Tests.Email;

public class EmailRetryPolicyTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    private static readonly SpecRetrySettings Settings = new();

    [Theory]
    [InlineData(EmailRequestKind.EmailConfirmation, 1, 1)]
    [InlineData(EmailRequestKind.EmailConfirmation, 2, 5)]
    [InlineData(EmailRequestKind.EmailConfirmation, 3, 15)]
    [InlineData(EmailRequestKind.EmailConfirmation, 4, 60)]
    [InlineData(EmailRequestKind.EmailConfirmation, 5, 240)]
    [InlineData(EmailRequestKind.PasswordReset, 1, 1)]
    [InlineData(EmailRequestKind.PasswordReset, 2, 5)]
    [InlineData(EmailRequestKind.PasswordReset, 3, 15)]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 1, 5)]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 2, 30)]
    public void A_retryable_result_with_attempts_left_is_retried_after_the_delay_before_the_next_attempt(
        EmailRequestKind kind, int attempt, int expectedDelayMinutes)
    {
        RetryDecision decision = EmailRetryPolicy.Decide(Settings.For(kind), attempt, SendResult.Retryable("SmtpCommandException 451"), Now);

        Assert.Equal(
            new RetryDecision(RetryAction.Retry, Now + Duration.FromMinutes(expectedDelayMinutes), "SmtpCommandException 451"),
            decision);
    }

    [Theory]
    [InlineData(EmailRequestKind.PasswordReset, 4)]
    [InlineData(EmailRequestKind.EmailConfirmation, 6)]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 3)]
    public void A_retryable_result_on_the_last_attempt_is_final(EmailRequestKind kind, int lastAttempt)
    {
        RetryDecision decision = EmailRetryPolicy.Decide(Settings.For(kind), lastAttempt, SendResult.Retryable("SmtpCommandException 451"), Now);

        Assert.Equal(new RetryDecision(RetryAction.Failed, null, "SmtpCommandException 451"), decision);
    }

    [Fact]
    public void A_permanent_result_is_final_even_with_attempts_left()
    {
        RetryDecision decision = EmailRetryPolicy.Decide(Settings.For(EmailRequestKind.EmailConfirmation), 1, SendResult.Permanent("SmtpCommandException 550"), Now);

        Assert.Equal(new RetryDecision(RetryAction.Failed, null, "SmtpCommandException 550"), decision);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void A_sent_result_is_sent_on_any_attempt_including_the_last(int attempt)
    {
        RetryDecision decision = EmailRetryPolicy.Decide(Settings.For(EmailRequestKind.EmailConfirmation), attempt, SendResult.Sent, Now);

        Assert.Equal(new RetryDecision(RetryAction.Sent, null, null), decision);
    }

    [Fact]
    public void A_missing_schedule_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => EmailRetryPolicy.Decide(null!, 1, SendResult.Sent, Now));
    }

    [Fact]
    public void A_missing_result_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => EmailRetryPolicy.Decide(Settings.For(EmailRequestKind.PasswordReset), 1, null!, Now));
    }
}
