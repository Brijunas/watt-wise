using WattWise.Application.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

/// <summary>A claim takes every pending row, so this test has a class and database clone of its own.</summary>
public class EmailClaimConcurrencyTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
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
}
