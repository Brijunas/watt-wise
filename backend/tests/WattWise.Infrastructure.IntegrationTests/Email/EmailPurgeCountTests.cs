using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>Alone in its class, so its database holds no rows of other tests: the count is the whole table's.</summary>
public class EmailPurgeCountTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task The_number_of_rows_deleted_is_returned()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Instant now = harness.Clock.GetCurrentInstant();
        Instant cutoff = now - Duration.FromDays(30);
        await Seed(harness, EmailRequestStatus.Sent, cutoff - Duration.FromDays(1));
        await Seed(harness, EmailRequestStatus.Failed, cutoff - Duration.FromDays(2));
        await Seed(harness, EmailRequestStatus.Sent, cutoff + Duration.FromDays(1));

        int deleted = await harness.WithOutboxAsync(outbox => outbox.PurgeAsync(cutoff, OutboxHarness.Token));

        Assert.Equal(2, deleted);
    }

    private static Task<Guid> Seed(OutboxHarness harness, EmailRequestStatus status, Instant createdAt) =>
        harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: $"purge-{Guid.NewGuid():N}@example.com", status: status, createdAt: createdAt);
}
