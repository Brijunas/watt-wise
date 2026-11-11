using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimLockedRowTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    // Another pass holds a row (SKIP LOCKED): this pass takes the others and does not wait.
    [Fact]
    public async Task A_row_locked_by_another_transaction_is_skipped_and_the_claim_does_not_wait()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount lockedAccount = await harness.AddAccountAsync();
        OutboxAccount freeAccount = await harness.AddAccountAsync();
        Guid locked = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: lockedAccount.AccountId, nextAttemptAt: Now - Duration.FromSeconds(2));
        Guid free = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: freeAccount.AccountId, nextAttemptAt: Now - Duration.FromSeconds(1));
        await using AsyncServiceScope holder = harness.Jobs.CreateAsyncScope();
        AppDbContext holderDb = holder.ServiceProvider.GetRequiredService<AppDbContext>();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await holderDb.Database.BeginTransactionAsync(OutboxHarness.Token);
        await holderDb.Database.ExecuteSqlAsync($"SELECT id FROM app.email_requests WHERE id = {locked} FOR UPDATE", OutboxHarness.Token);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync().WaitAsync(TimeSpan.FromSeconds(10), OutboxHarness.Token);

        await transaction.RollbackAsync(OutboxHarness.Token);
        Assert.Equal(
            ($"{free}", "pending"),
            (string.Join(",", claimed.Select(email => email.RequestId)), await harness.StatusOfAsync(locked)));
    }
}
