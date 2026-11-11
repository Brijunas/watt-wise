using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task A_due_pending_row_is_claimed_as_the_first_attempt()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([(id, EmailRequestKind.EmailConfirmation, 1)], claimed.Select(email => (email.RequestId, email.Kind, email.AttemptCount)));
    }

    [Fact]
    public async Task Claiming_marks_the_row_sending_counts_the_attempt_and_stamps_the_time()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, attemptCount: 2);
        await harness.ClaimAsync();

        EmailRequest? row = await harness.ReadAsync(id);

        Assert.Equal(
            (EmailRequestStatus.Sending, 3, (Instant?)Instant.FromUtc(2026, 11, 11, 12, 0)),
            (row!.Status, row.AttemptCount, row.LastAttemptAt));
    }

    [Fact]
    public async Task A_row_due_in_the_future_is_not_claimed()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            accountId: account.AccountId,
            nextAttemptAt: Instant.FromUtc(2026, 11, 11, 12, 0, 1));
        await harness.ClaimAsync();

        EmailRequest? row = await harness.ReadAsync(id);

        Assert.Equal((EmailRequestStatus.Pending, 0), (row!.Status, row.AttemptCount));
    }

    [Theory]
    [InlineData(EmailRequestStatus.Sending)]
    [InlineData(EmailRequestStatus.Sent)]
    [InlineData(EmailRequestStatus.Failed)]
    public async Task A_row_that_is_not_pending_is_not_claimed(EmailRequestStatus status)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, status: status, attemptCount: 1);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.DoesNotContain(id, claimed.Select(email => email.RequestId));
    }

    [Fact]
    public async Task A_second_pass_does_not_claim_a_row_again()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        await harness.ClaimAsync();

        IReadOnlyList<ReadyEmail> second = await harness.ClaimAsync();

        Assert.DoesNotContain(id, second.Select(email => email.RequestId));
    }

    [Fact]
    public async Task Passes_running_at_once_claim_every_row_exactly_once()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        List<Guid> ids = [];
        for (int i = 0; i < 10; i++)
        {
            OutboxAccount account = await harness.AddAccountAsync();
            ids.Add(await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId));
        }

        IReadOnlyList<ReadyEmail>[] passes = await Task.WhenAll(
            harness.ClaimAsync(), harness.ClaimAsync(), harness.ClaimAsync(), harness.ClaimAsync());

        Assert.Equal(ids.Order(), passes.SelectMany(pass => pass).Select(email => email.RequestId).Order());
    }

    [Fact]
    public async Task A_pass_claims_at_most_the_batch_size_oldest_due_first()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Instant now = harness.Clock.GetCurrentInstant();
        List<Guid> ids = [];
        for (int i = 0; i < 25; i++)
        {
            OutboxAccount account = await harness.AddAccountAsync();

            // Row 0 has been due for 25 seconds, row 24 for 1 second.
            ids.Add(await harness.SeedAsync(
                EmailRequestKind.EmailConfirmation, accountId: account.AccountId, nextAttemptAt: now - Duration.FromSeconds(25 - i)));
        }

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync(20);

        Assert.Equal(ids.Take(20), claimed.Select(email => email.RequestId));
    }
}
