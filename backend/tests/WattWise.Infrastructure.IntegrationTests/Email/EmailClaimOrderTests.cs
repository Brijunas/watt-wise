using WattWise.Application.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>Alone in its class: the claim takes every due row of the database, so rows left by other tests would join the order.</summary>
public class EmailClaimOrderTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Rows due at the same time come back ordered by id, so a pass is the same on every run.
    [Fact]
    public async Task Rows_due_at_the_same_time_are_returned_in_id_order()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid lower = Guid.Parse("00000000-0000-7000-8000-000000000001");
        Guid higher = Guid.Parse("00000000-0000-7000-8000-000000000002");
        OutboxAccount first = await harness.AddAccountAsync();
        OutboxAccount second = await harness.AddAccountAsync();
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: first.AccountId, id: higher);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: second.AccountId, id: lower);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([lower, higher], claimed.Select(email => email.RequestId));
    }
}
