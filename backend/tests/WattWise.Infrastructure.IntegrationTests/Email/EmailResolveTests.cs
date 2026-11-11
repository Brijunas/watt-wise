using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailResolveTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData(EmailRequestKind.EmailConfirmation)]
    [InlineData(EmailRequestKind.PasswordReset)]
    public async Task A_request_for_an_unknown_address_is_deleted_and_nothing_is_sent(EmailRequestKind kind)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        Guid id = await harness.SeedAsync(kind, email: $"nobody-{Guid.NewGuid():N}@example.com");

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(("deleted", false), (await harness.StatusOfAsync(id), claimed.Any(email => email.RequestId == id)));
    }

    [Fact]
    public async Task A_confirmation_for_an_already_confirmed_account_is_deleted_and_nothing_is_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(("deleted", false), (await harness.StatusOfAsync(id), claimed.Any(email => email.RequestId == id)));
    }

    [Fact]
    public async Task An_account_keyed_confirmation_for_an_already_confirmed_account_is_deleted_and_nothing_is_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        Guid id = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(("deleted", false), (await harness.StatusOfAsync(id), claimed.Any(email => email.RequestId == id)));
    }

    [Fact]
    public async Task An_account_keyed_password_reset_for_an_unconfirmed_account_is_deleted_and_nothing_is_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid id = await harness.SeedAsync(EmailRequestKind.PasswordReset, accountId: account.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(("deleted", false), (await harness.StatusOfAsync(id), claimed.Any(email => email.RequestId == id)));
    }

    [Fact]
    public async Task A_password_reset_for_an_unconfirmed_account_is_deleted_and_nothing_is_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid id = await harness.SeedAsync(EmailRequestKind.PasswordReset, email: account.Email);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(("deleted", false), (await harness.StatusOfAsync(id), claimed.Any(email => email.RequestId == id)));
    }

    [Fact]
    public async Task A_password_reset_for_a_confirmed_account_is_sent_to_its_address()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        await harness.SeedAsync(EmailRequestKind.PasswordReset, email: account.Email);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        ReadyEmail ready = Assert.Single(claimed);
        Assert.Equal((typeof(PasswordResetEmail), account.Email), (ready.Message.GetType(), ready.Message.To));
    }

    [Fact]
    public async Task A_confirmation_for_an_unconfirmed_account_is_sent_to_its_address()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        ReadyEmail ready = Assert.Single(claimed);
        Assert.Equal((typeof(EmailConfirmationEmail), account.Email), (ready.Message.GetType(), ready.Message.To));
    }

    [Fact]
    public async Task An_account_already_exists_notice_is_sent_without_a_link_and_creates_no_token()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        await harness.SeedAsync(EmailRequestKind.AccountAlreadyExists, accountId: account.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        long tokens = await CountTokensAsync(harness, account.IdentityId);
        ReadyEmail ready = Assert.Single(claimed);
        Assert.Equal((typeof(AccountAlreadyExistsEmail), account.Email, 0L), (ready.Message.GetType(), ready.Message.To, tokens));
    }

    [Theory]
    [InlineData(Language.Lt)]
    [InlineData(Language.En)]
    public async Task The_message_is_in_the_accounts_language(Language language)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(language);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(language, Assert.Single(claimed).Message.Language);
    }

    [Fact]
    public async Task Each_account_gets_its_own_address_and_language()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount lithuanian = await harness.AddAccountAsync(Language.Lt);
        OutboxAccount english = await harness.AddAccountAsync(Language.En);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: lithuanian.AccountId);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: english.AccountId);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(
            new[] { (lithuanian.Email, Language.Lt), (english.Email, Language.En) }.Order(),
            claimed.Select(email => (email.Message.To, email.Message.Language)).Order());
    }

    private static async Task<long> CountTokensAsync(OutboxHarness harness, Guid identityId)
    {
        await using AsyncServiceScope scope = harness.Jobs.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<long>($"""SELECT count(*) AS "Value" FROM app.email_link_tokens WHERE identity_id = {identityId}""")
            .SingleAsync(OutboxHarness.Token);
    }
}
