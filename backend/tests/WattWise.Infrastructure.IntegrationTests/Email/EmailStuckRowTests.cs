using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailStuckRowTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Duration StuckAfter = Duration.FromMinutes(5);
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Fact]
    public async Task A_row_sending_for_more_than_five_minutes_goes_back_to_pending_due_now_with_a_timeout_error()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedSendingAsync(harness, EmailRequestKind.EmailConfirmation, attemptCount: 1, secondsAgo: 301);

        await RecoverAsync(harness);

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal(
            (EmailRequestStatus.Pending, Now, "SendingTimedOut", 1),
            (row!.Status, row.NextAttemptAt, row.LastError, row.AttemptCount));
    }

    [Theory]
    [InlineData(299)]
    [InlineData(300)]
    public async Task A_row_sending_for_five_minutes_or_less_is_left_alone(int secondsAgo)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedSendingAsync(harness, EmailRequestKind.EmailConfirmation, attemptCount: 1, secondsAgo);

        await RecoverAsync(harness);

        Assert.Equal("sending", await harness.StatusOfAsync(id));
    }

    [Fact]
    public async Task A_row_that_already_timed_out_once_fails()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedSendingAsync(harness, EmailRequestKind.EmailConfirmation, attemptCount: 2, secondsAgo: 400, lastError: "SendingTimedOut");

        await RecoverAsync(harness);

        Assert.Equal("failed", await harness.StatusOfAsync(id));
    }

    [Fact]
    public async Task A_row_stuck_after_a_different_error_is_retried_once_more()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedSendingAsync(
            harness, EmailRequestKind.EmailConfirmation, attemptCount: 2, secondsAgo: 400, lastError: "SmtpCommandException 451");

        await RecoverAsync(harness);

        Assert.Equal("pending", await harness.StatusOfAsync(id));
    }

    [Theory]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 3, "failed")]
    [InlineData(EmailRequestKind.AccountAlreadyExists, 2, "pending")]
    [InlineData(EmailRequestKind.EmailConfirmation, 3, "pending")]
    [InlineData(EmailRequestKind.EmailConfirmation, 6, "failed")]
    [InlineData(EmailRequestKind.PasswordReset, 4, "failed")]
    [InlineData(EmailRequestKind.PasswordReset, 3, "pending")]
    public async Task A_stuck_row_whose_attempts_are_used_up_fails_and_otherwise_goes_back_to_pending(
        EmailRequestKind kind, int attemptCount, string expectedStatus)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedSendingAsync(harness, kind, attemptCount, secondsAgo: 400);

        await RecoverAsync(harness);

        Assert.Equal(expectedStatus, await harness.StatusOfAsync(id));
    }

    [Fact]
    public async Task Pending_and_sent_rows_are_not_touched_by_the_recovery()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid pending = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", lastAttemptAt: Now - Duration.FromMinutes(30), attemptCount: 1);
        Guid sent = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "b@example.com", status: EmailRequestStatus.Sent,
            lastAttemptAt: Now - Duration.FromMinutes(30), attemptCount: 1);

        await RecoverAsync(harness);

        Assert.Equal(["pending", "sent"], [await harness.StatusOfAsync(pending), await harness.StatusOfAsync(sent)]);
    }

    [Fact]
    public async Task A_recovered_row_is_claimed_again_as_the_next_attempt()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            accountId: account.AccountId,
            status: EmailRequestStatus.Sending,
            attemptCount: 1,
            lastAttemptAt: Now - Duration.FromSeconds(400),
            nextAttemptAt: Now - Duration.FromSeconds(400));
        await RecoverAsync(harness);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([(id, 2)], claimed.Select(email => (email.RequestId, email.AttemptCount)));
    }

    private static Task<Guid> SeedSendingAsync(
        OutboxHarness harness, EmailRequestKind kind, int attemptCount, int secondsAgo, string? lastError = null) =>
        harness.SeedAsync(
            kind,
            email: $"stuck-{Guid.NewGuid():N}@example.com",
            status: EmailRequestStatus.Sending,
            attemptCount: attemptCount,
            lastAttemptAt: Now - Duration.FromSeconds(secondsAgo),
            nextAttemptAt: Now - Duration.FromSeconds(secondsAgo),
            lastError: lastError);

    private static Task<int> RecoverAsync(OutboxHarness harness) =>
        harness.WithOutboxAsync(outbox => outbox.RecoverStuckAsync(StuckAfter, OutboxHarness.MaxAttempts, OutboxHarness.Token));
}
