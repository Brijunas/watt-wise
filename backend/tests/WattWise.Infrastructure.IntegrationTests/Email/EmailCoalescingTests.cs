using NodaTime;

using WattWise.Application.Email;
using WattWise.Infrastructure.Email;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailCoalescingTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 11, 12, 0);

    [Fact]
    public async Task Only_the_newest_request_of_a_kind_and_account_is_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(3));
        Guid newest = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(2));

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([newest], claimed.Select(email => email.RequestId));
    }

    [Fact]
    public async Task The_older_requests_become_superseded_and_the_newest_is_sending()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid oldest = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(3));
        Guid middle = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(2));
        Guid newest = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        await harness.ClaimAsync();

        string[] statuses = [await harness.StatusOfAsync(oldest), await harness.StatusOfAsync(middle), await harness.StatusOfAsync(newest)];

        Assert.Equal(["superseded", "superseded", "sending"], statuses);
    }

    [Fact]
    public async Task Requests_created_at_the_same_time_are_ordered_by_id()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid lower = Guid.Parse("00000000-0000-7000-8000-000000000001");
        Guid higher = Guid.Parse("00000000-0000-7000-8000-000000000002");
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, id: higher);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, id: lower);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([higher], claimed.Select(email => email.RequestId));
    }

    [Fact]
    public async Task An_older_request_that_is_not_yet_due_is_superseded_by_a_newer_one_that_is()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();

        // A retry waiting for its turn, created before the newer request.
        Guid waiting = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            accountId: account.AccountId,
            createdAt: Now - Duration.FromMinutes(2),
            nextAttemptAt: Now + Duration.FromMinutes(10),
            attemptCount: 1);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now);
        await harness.ClaimAsync();

        Assert.Equal("superseded", await harness.StatusOfAsync(waiting));
    }

    [Fact]
    public async Task An_older_request_that_was_already_sent_stays_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid sent = await harness.SeedAsync(
            EmailRequestKind.EmailConfirmation,
            accountId: account.AccountId,
            status: EmailRequestStatus.Sent,
            createdAt: Now - Duration.FromMinutes(2),
            attemptCount: 1,
            sentAt: Now - Duration.FromMinutes(2));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now);
        await harness.ClaimAsync();

        Assert.Equal("sent", await harness.StatusOfAsync(sent));
    }

    [Fact]
    public async Task Requests_of_different_kinds_for_one_account_are_both_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        Guid confirmation = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        Guid alreadyExists = await harness.SeedAsync(EmailRequestKind.AccountAlreadyExists, accountId: account.AccountId, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(new[] { confirmation, alreadyExists }.Order(), claimed.Select(email => email.RequestId).Order());
    }

    [Fact]
    public async Task Requests_of_one_kind_for_different_accounts_are_both_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount first = await harness.AddAccountAsync();
        OutboxAccount second = await harness.AddAccountAsync();
        Guid firstId = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: first.AccountId, createdAt: Now - Duration.FromMinutes(1));
        Guid secondId = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: second.AccountId, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(new[] { firstId, secondId }.Order(), claimed.Select(email => email.RequestId).Order());
    }

    [Fact]
    public async Task Requests_for_different_addresses_are_both_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount first = await harness.AddAccountAsync();
        OutboxAccount second = await harness.AddAccountAsync();
        Guid firstId = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: first.Email, createdAt: Now - Duration.FromMinutes(1));
        Guid secondId = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: second.Email, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(new[] { firstId, secondId }.Order(), claimed.Select(email => email.RequestId).Order());
    }

    [Fact]
    public async Task Two_requests_for_one_address_send_only_the_newest()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync();
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, createdAt: Now - Duration.FromMinutes(1));
        Guid newest = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal([newest], claimed.Select(email => email.RequestId));
    }

    // Register writes an account-keyed request, a later resend an address-keyed one: the owner must get one email, the newest.
    [Fact]
    public async Task A_newer_address_keyed_request_supersedes_an_older_account_keyed_one_for_the_same_account()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid registered = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        Guid resent = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(
            ($"{resent}", "superseded"),
            (string.Join(",", claimed.Select(email => email.RequestId)), await harness.StatusOfAsync(registered)));
    }

    [Fact]
    public async Task The_stored_token_belongs_to_the_one_link_that_was_returned_after_such_a_supersede()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, createdAt: Now);
        ReadyEmail ready = Assert.Single(await harness.ClaimAsync());
        string token = System.Text.RegularExpressions.Regex.Match(((WattWise.Application.Accounts.EmailConfirmationEmail)ready.Message).Link.Query, "[?&]token=([^&]+)").Groups[1].Value;

        byte[] stored = await Accounts.EmailLinks.StoredHashAsync(harness.Api, account.IdentityId, Accounts.EmailLinkKind.Confirmation);

        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Buffers.Text.Base64Url.DecodeFromChars(token))),
            Convert.ToHexString(stored));
    }

    [Fact]
    public async Task An_address_keyed_confirmation_and_an_account_keyed_notice_for_one_account_are_both_sent()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid notice = await harness.SeedAsync(EmailRequestKind.AccountAlreadyExists, accountId: account.AccountId, createdAt: Now - Duration.FromMinutes(1));
        Guid confirmation = await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, createdAt: Now);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal(new[] { notice, confirmation }.Order(), claimed.Select(email => email.RequestId).Order());
    }

    // PostgreSQL orders uuids byte by byte from the first byte of the text form; .NET's Guid.ToByteArray() puts the first group
    // in little-endian order, so ordering by it would pick the other id. Here only the resolved-account coalesce decides.
    [Fact]
    public async Task Requests_created_at_the_same_time_for_one_account_are_ordered_by_the_uuids_text_order()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        Guid higher = Guid.Parse("01000000-0000-7000-8000-000000000000");
        Guid lower = Guid.Parse("00000100-0000-7000-8000-000000000000");
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId, id: lower);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email, id: higher);

        IReadOnlyList<ReadyEmail> claimed = await harness.ClaimAsync();

        Assert.Equal($"{higher}", string.Join(",", claimed.Select(email => email.RequestId)));
    }
}
