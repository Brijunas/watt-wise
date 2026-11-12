using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimPartialTokenTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Fact]
    public async Task The_failed_rows_partial_token_is_not_stored_and_the_row_is_failed_with_the_exception_type()
    {
        TokenFault fault = new() { TrackPartialToken = true };
        await using OutboxHarness harness = OutboxHarness.Create(fixture, configureJobs: OutboxHarness.WithTokenFault(fault));
        OutboxAccount broken = await harness.AddAccountAsync(confirmed: false);
        OutboxAccount fine = await harness.AddAccountAsync(confirmed: false);
        fault.IdentityId = broken.IdentityId;
        Guid brokenRow = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: broken.AccountId, nextAttemptAt: Now - Duration.FromSeconds(2));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: fine.AccountId, nextAttemptAt: Now - Duration.FromSeconds(1));
        await harness.ClaimAsync();

        EmailRequest? row = await harness.ReadAsync(brokenRow);

        Assert.Equal(
            (EmailRequestStatus.Failed, "InvalidOperationException", 0L),
            (row!.Status, row.LastError, await harness.CountTokensAsync(broken.IdentityId)));
    }
}
