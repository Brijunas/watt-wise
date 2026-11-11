using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailPurgeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);
    private static readonly Instant Cutoff = Now - Duration.FromDays(30);

    [Theory]
    [InlineData(EmailRequestStatus.Sent)]
    [InlineData(EmailRequestStatus.Superseded)]
    [InlineData(EmailRequestStatus.Failed)]
    public async Task A_finished_row_created_before_the_cutoff_is_deleted(EmailRequestStatus status)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, status, Cutoff - Duration.FromSeconds(1));

        await PurgeAsync(harness);

        Assert.Equal("deleted", await harness.StatusOfAsync(id));
    }

    [Theory]
    [InlineData(EmailRequestStatus.Sent)]
    [InlineData(EmailRequestStatus.Superseded)]
    [InlineData(EmailRequestStatus.Failed)]
    public async Task A_finished_row_created_after_the_cutoff_is_kept(EmailRequestStatus status)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, status, Cutoff + Duration.FromSeconds(1));

        await PurgeAsync(harness);

        Assert.Equal(status.ToString().ToLowerInvariant(), await harness.StatusOfAsync(id));
    }

    [Fact]
    public async Task A_finished_row_created_exactly_at_the_cutoff_is_kept()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, EmailRequestStatus.Sent, Cutoff);

        await PurgeAsync(harness);

        Assert.Equal("sent", await harness.StatusOfAsync(id));
    }

    [Theory]
    [InlineData(EmailRequestStatus.Pending)]
    [InlineData(EmailRequestStatus.Sending)]
    public async Task An_old_row_that_is_not_finished_is_kept(EmailRequestStatus status)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, status, Cutoff - Duration.FromDays(60));

        await PurgeAsync(harness);

        Assert.Equal(status.ToString().ToLowerInvariant(), await harness.StatusOfAsync(id));
    }

    [Fact]
    public async Task The_age_is_the_creation_time_not_the_time_it_was_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            email: $"purge-{Guid.NewGuid():N}@example.com",
            status: EmailRequestStatus.Sent,
            createdAt: Cutoff - Duration.FromDays(1),
            sentAt: Now);

        await PurgeAsync(harness);

        Assert.Equal("deleted", await harness.StatusOfAsync(id));
    }

    private static Task<Guid> SeedAsync(OutboxHarness harness, EmailRequestStatus status, Instant createdAt) =>
        harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            email: $"purge-{Guid.NewGuid():N}@example.com",
            status: status,
            createdAt: createdAt);

    private static Task<int> PurgeAsync(OutboxHarness harness) =>
        harness.WithOutboxAsync(outbox => outbox.PurgeAsync(Cutoff, OutboxHarness.Token));
}
