using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailLateRecordTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Theory]
    [InlineData(OutboxRecordKind.Sent, "Sent")]
    [InlineData(OutboxRecordKind.Retry, "Pending")]
    [InlineData(OutboxRecordKind.Failed, "Failed")]
    public async Task A_result_for_the_row_it_was_claimed_as_updates_it_and_returns_true(OutboxRecordKind kind, string expectedStatus)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, EmailRequestStatus.Sending, attemptCount: 1);

        bool matched = await harness.RecordAsync(kind, id, attempt: 1);

        Assert.Equal((true, expectedStatus), (matched, (await harness.SnapshotAsync(id)).Status));
    }

    // Recovery moved the row to pending or failed, or a later pass claimed it again as attempt 2.
    [Theory]
    [InlineData(OutboxRecordKind.Sent, EmailRequestStatus.Pending, 1)]
    [InlineData(OutboxRecordKind.Sent, EmailRequestStatus.Failed, 1)]
    [InlineData(OutboxRecordKind.Sent, EmailRequestStatus.Sending, 2)]
    [InlineData(OutboxRecordKind.Retry, EmailRequestStatus.Pending, 1)]
    [InlineData(OutboxRecordKind.Retry, EmailRequestStatus.Failed, 1)]
    [InlineData(OutboxRecordKind.Retry, EmailRequestStatus.Sending, 2)]
    [InlineData(OutboxRecordKind.Failed, EmailRequestStatus.Pending, 1)]
    [InlineData(OutboxRecordKind.Failed, EmailRequestStatus.Failed, 1)]
    [InlineData(OutboxRecordKind.Failed, EmailRequestStatus.Sending, 2)]
    public async Task A_late_result_changes_nothing_and_returns_false(OutboxRecordKind kind, EmailRequestStatus status, int attemptCount)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await SeedAsync(harness, status, attemptCount);
        var before = await harness.SnapshotAsync(id);

        bool matched = await harness.RecordAsync(kind, id, attempt: 1);

        Assert.Equal((false, before), (matched, await harness.SnapshotAsync(id)));
    }

    [Theory]
    [InlineData(OutboxRecordKind.Sent)]
    [InlineData(OutboxRecordKind.Retry)]
    [InlineData(OutboxRecordKind.Failed)]
    public async Task A_result_changes_only_its_own_row(OutboxRecordKind kind)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid own = await SeedAsync(harness, EmailRequestStatus.Sending, attemptCount: 1);
        Guid other = await SeedAsync(harness, EmailRequestStatus.Sending, attemptCount: 1);
        var otherBefore = await harness.SnapshotAsync(other);

        await harness.RecordAsync(kind, own, attempt: 1);

        Assert.Equal(otherBefore, await harness.SnapshotAsync(other));
    }

    private static Task<Guid> SeedAsync(OutboxHarness harness, EmailRequestStatus status, int attemptCount) =>
        harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            email: $"late-{Guid.NewGuid():N}@example.com",
            status: status,
            attemptCount: attemptCount,
            lastAttemptAt: Now);
}
