using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailRecordTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Fact]
    public async Task A_sent_row_is_marked_sent_with_the_time()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 1, lastAttemptAt: Now);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordSentAsync(id, 1, OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal((EmailRequestStatus.Sent, (Instant?)Now), (row!.Status, row.SentAt));
    }

    [Fact]
    public async Task A_retry_goes_back_to_pending_at_the_given_time_with_the_error_and_keeps_the_attempt_count()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 2, lastAttemptAt: Now);
        Instant next = Now + Duration.FromMinutes(5);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordRetryAsync(id, 2, next, "SmtpCommandException 451", OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal(
            (EmailRequestStatus.Pending, next, "SmtpCommandException 451", 2),
            (row!.Status, row.NextAttemptAt, row.LastError, row.AttemptCount));
    }

    [Fact]
    public async Task A_final_failure_is_marked_failed_with_the_error()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 1, lastAttemptAt: Now);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordFailedAsync(id, 1, "SmtpCommandException 550", OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal((EmailRequestStatus.Failed, "SmtpCommandException 550"), (row!.Status, row.LastError));
    }

    // The error is an exception type name or an SMTP status; the cut is the outbox's, so the column can never overflow.
    [Fact]
    public async Task An_error_longer_than_100_characters_is_cut_to_100()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 1, lastAttemptAt: Now);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordFailedAsync(id, 1, new string('x', 250), OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal(new string('x', 100), row!.LastError);
    }

    [Fact]
    public async Task A_retry_error_longer_than_100_characters_is_cut_to_100()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 1, lastAttemptAt: Now);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordRetryAsync(id, 1, Now + Duration.FromMinutes(1), new string('y', 250), OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal(new string('y', 100), row!.LastError);
    }

    [Fact]
    public async Task An_error_of_exactly_100_characters_is_kept_whole()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation, email: "a@example.com", status: EmailRequestStatus.Sending, attemptCount: 1, lastAttemptAt: Now);

        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordFailedAsync(id, 1, new string('z', 100), OutboxHarness.Token);
            return 0;
        });

        EmailRequest? row = await harness.ReadAsync(id);
        Assert.Equal(new string('z', 100), row!.LastError);
    }
}
