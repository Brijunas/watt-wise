using NodaTime;

using WattWise.Application.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>A claim takes the oldest due rows of the whole table, so this test has a class and database clone of its own.</summary>
public class EmailClaimBatchTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
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
