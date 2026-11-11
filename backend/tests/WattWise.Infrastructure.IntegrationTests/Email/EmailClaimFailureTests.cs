using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailClaimFailureTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Without Links:FrontendBaseUrl the validated Links options throw when read: a bug in the setup, not in the row's data.
    [Fact]
    public async Task A_bug_while_preparing_one_row_fails_that_row_with_the_exception_type_name()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture, withLinks: false);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        await harness.ClaimAsync();

        EmailRequest? row = await harness.ReadAsync(id);

        Assert.Equal((EmailRequestStatus.Failed, "OptionsValidationException"), (row!.Status, row.LastError));
    }

    [Fact]
    public async Task The_other_rows_of_the_batch_are_still_returned()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture, withLinks: false);
        OutboxAccount broken = await harness.AddAccountAsync(confirmed: false);
        OutboxAccount fine = await harness.AddAccountAsync(confirmed: true);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: broken.AccountId);
        Guid notice = await harness.SeedAsync(EmailRequestKind.AccountAlreadyExists, accountId: fine.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([(notice, typeof(AccountAlreadyExistsEmail))], claimed.Select(email => (email.RequestId, email.Message.GetType())));
    }
}
