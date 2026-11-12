using NodaTime;

using Npgsql;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimDatabaseErrorTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

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
}
