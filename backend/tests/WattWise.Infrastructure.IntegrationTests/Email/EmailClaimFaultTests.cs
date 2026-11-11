using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Npgsql;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;
using WattWise.Infrastructure.IntegrationTests.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimFaultTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
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

    // Not an unexpected bug but a database failure: the whole claim rolls back, so no row is left "sending" and no token is kept.
    [Fact]
    public async Task A_database_error_while_preparing_a_row_rolls_the_whole_claim_back()
    {
        TokenFault fault = new() { Exception = new NpgsqlException("connection lost") };
        await using OutboxHarness harness = OutboxHarness.Create(fixture, configureJobs: OutboxHarness.WithTokenFault(fault));
        OutboxAccount first = await harness.AddAccountAsync(confirmed: false);
        OutboxAccount second = await harness.AddAccountAsync(confirmed: false);
        fault.IdentityId = second.IdentityId;
        Guid firstRow = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: first.AccountId, nextAttemptAt: Now - Duration.FromSeconds(2));
        Guid secondRow = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: second.AccountId, nextAttemptAt: Now - Duration.FromSeconds(1));

        Exception? exception = await Record.ExceptionAsync(() => harness.ClaimAsync());

        EmailRequest? firstAfter = await harness.ReadAsync(firstRow);
        EmailRequest? secondAfter = await harness.ReadAsync(secondRow);
        Assert.Equal(
            (typeof(NpgsqlException), "pending,0", "pending,0", 0L),
            (exception?.GetType(), $"{firstAfter!.Status.ToString().ToLowerInvariant()},{firstAfter.AttemptCount}", $"{secondAfter!.Status.ToString().ToLowerInvariant()},{secondAfter.AttemptCount}", await harness.CountTokensAsync(first.IdentityId)));
    }

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

    private static string TokenOf(ReadyEmail ready) =>
        System.Text.RegularExpressions.Regex.Match(((WattWise.Application.Accounts.EmailConfirmationEmail)ready.Message).Link.Query, "[?&]token=([^&]+)").Groups[1].Value;
}
