using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.IntegrationTests.Accounts;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimTokenRecoveryTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Fact]
    public async Task After_a_token_failure_the_next_row_still_gets_a_token_that_works()
    {
        TokenFault fault = new() { TrackPartialToken = true };
        await using OutboxHarness harness = OutboxHarness.Create(fixture, configureJobs: OutboxHarness.WithTokenFault(fault));
        OutboxAccount broken = await harness.AddAccountAsync(confirmed: false);
        OutboxAccount fine = await harness.AddAccountAsync(confirmed: false);
        fault.IdentityId = broken.IdentityId;
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: broken.AccountId, nextAttemptAt: Now - Duration.FromSeconds(2));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: fine.AccountId, nextAttemptAt: Now - Duration.FromSeconds(1));
        ReadyEmail ready = Assert.Single(await harness.ClaimAsync());
        string token = TokenOf(ready);

        Microsoft.AspNetCore.Identity.IdentityResult result =
            await EmailLinks.RedeemAsync(harness.Api, fine.IdentityId, EmailLinkKind.Confirmation, token);

        Assert.True(result.Succeeded);
    }

    private static string TokenOf(ReadyEmail ready) =>
        System.Text.RegularExpressions.Regex.Match(((WattWise.Application.Accounts.EmailConfirmationEmail)ready.Message).Link.Query, "[?&]token=([^&]+)").Groups[1].Value;
}
