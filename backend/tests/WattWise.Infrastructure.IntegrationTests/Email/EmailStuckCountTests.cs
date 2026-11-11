using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>Alone in its class, so its database holds no rows of other tests: the count is the whole table's.</summary>
public class EmailStuckCountTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task The_number_of_rows_recovered_or_failed_is_returned()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Instant now = harness.Clock.GetCurrentInstant();
        await Seed(harness, attemptCount: 1, secondsAgo: 400, lastError: null);
        await Seed(harness, attemptCount: 2, secondsAgo: 400, lastError: "SendingTimedOut");
        await Seed(harness, attemptCount: 1, secondsAgo: 10, lastError: null);

        int count = await harness.WithOutboxAsync(
            outbox => outbox.RecoverStuckAsync(Duration.FromMinutes(5), OutboxHarness.MaxAttempts, OutboxHarness.Token));

        Assert.Equal(2, count);
    }

    private static Task<Guid> Seed(OutboxHarness harness, int attemptCount, int secondsAgo, string? lastError)
    {
        Instant at = harness.Clock.GetCurrentInstant() - Duration.FromSeconds(secondsAgo);
        return harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            email: $"stuck-{Guid.NewGuid():N}@example.com",
            status: EmailRequestStatus.Sending,
            attemptCount: attemptCount,
            lastAttemptAt: at,
            nextAttemptAt: at,
            lastError: lastError);
    }
}
