using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Infrastructure.IntegrationTests.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Email;

public class EmailLinkTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // 32 random bytes in Base64Url are 43 characters.
    private const string TokenPattern = "[A-Za-z0-9_-]{43}";

    [Fact]
    public async Task A_confirmation_link_points_to_the_frontends_confirm_page_with_the_identity_id_and_a_token()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, email: account.Email);

        Uri link = LinkOf(Assert.Single(await harness.ClaimAsync()));

        Assert.Matches($"^https://app\\.wattwise\\.test/confirm-email\\?id={account.IdentityId}&token={TokenPattern}$", link.AbsoluteUri);
    }

    [Fact]
    public async Task A_reset_link_points_to_the_frontends_reset_page_with_the_identity_id_and_a_token()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        await harness.SeedAsync(EmailRequestKind.PasswordReset, email: account.Email);

        Uri link = LinkOf(Assert.Single(await harness.ClaimAsync()));

        Assert.Matches($"^https://app\\.wattwise\\.test/reset-password\\?id={account.IdentityId}&token={TokenPattern}$", link.AbsoluteUri);
    }

    [Fact]
    public async Task The_confirmation_token_in_the_link_confirms_the_email()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        string token = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));

        await EmailLinks.RedeemAsync(harness.Api, account.IdentityId, EmailLinkKind.Confirmation, token);

        bool confirmed = await AccountsTestSupport.InScopeAsync(
            harness.Api, account.IdentityId, (users, identity) => users.IsEmailConfirmedAsync(identity));
        Assert.True(confirmed);
    }

    [Fact]
    public async Task The_reset_token_in_the_link_sets_a_new_password()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: true);
        await harness.SeedAsync(EmailRequestKind.PasswordReset, accountId: account.AccountId);
        string token = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));

        string newPassword = AccountsTestSupport.ValidPassword();
        await EmailLinks.RedeemAsync(harness.Api, account.IdentityId, EmailLinkKind.Reset, token, newPassword);

        Assert.True(await AccountsTestSupport.CheckPasswordAsync(harness.Api, account.IdentityId, newPassword));
    }

    [Fact]
    public async Task Only_the_sha256_hash_of_the_token_is_stored()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        string token = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));

        byte[] stored = await EmailLinks.StoredHashAsync(harness.Api, account.IdentityId, EmailLinkKind.Confirmation);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Base64Url.DecodeFromChars(token))), Convert.ToHexString(stored));
    }

    [Theory]
    [InlineData(EmailRequestKind.EmailConfirmation)]
    [InlineData(EmailRequestKind.PasswordReset)]
    public async Task The_plaintext_token_is_in_no_table(EmailRequestKind kind)
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: kind == EmailRequestKind.PasswordReset);
        Guid id = await harness.SeedAsync(kind, accountId: account.AccountId);
        string token = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));
        await harness.WithOutboxAsync(async outbox =>
        {
            await outbox.RecordSentAsync(id, 1, OutboxHarness.Token);
            return 0;
        });

        IReadOnlyList<string> tablesWithToken = await TablesContainingAsync(harness, token);

        Assert.Empty(tablesWithToken);
    }

    [Fact]
    public async Task A_newer_link_replaces_the_older_one()
    {
        await using OutboxHarness harness = OutboxHarness.Create(fixture);
        OutboxAccount account = await harness.AddAccountAsync(confirmed: false);
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        string first = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));
        await harness.SeedAsync(EmailRequestKind.EmailConfirmation, accountId: account.AccountId);
        string second = TokenOf(LinkOf(Assert.Single(await harness.ClaimAsync())));

        IdentityResult firstResult = await EmailLinks.RedeemAsync(harness.Api, account.IdentityId, EmailLinkKind.Confirmation, first);
        bool confirmedAfterFirst = await AccountsTestSupport.InScopeAsync(
            harness.Api, account.IdentityId, (users, identity) => users.IsEmailConfirmedAsync(identity));
        await EmailLinks.RedeemAsync(harness.Api, account.IdentityId, EmailLinkKind.Confirmation, second);
        bool confirmedAfterSecond = await AccountsTestSupport.InScopeAsync(
            harness.Api, account.IdentityId, (users, identity) => users.IsEmailConfirmedAsync(identity));

        Assert.Equal(("InvalidToken", false, true), (string.Join(",", EmailLinks.Codes(firstResult)), confirmedAfterFirst, confirmedAfterSecond));
    }

    private static Uri LinkOf(ReadyEmail ready) => ready.Message switch
    {
        EmailConfirmationEmail confirmation => confirmation.Link,
        PasswordResetEmail reset => reset.Link,
        _ => throw new InvalidOperationException($"{ready.Message.GetType().Name} has no link."),
    };

    private static string TokenOf(Uri link) =>
        Regex.Match(link.Query, "[?&]token=([^&]+)").Groups[1].Value;

    /// <summary>The names of the <c>app</c> tables with a row whose text form contains <paramref name="text"/>.</summary>
    private static async Task<IReadOnlyList<string>> TablesContainingAsync(OutboxHarness harness, string text)
    {
        await using AsyncServiceScope scope = harness.Jobs.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<string> tables = await db.Database
            .SqlQuery<string>($"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'app' AND table_type = 'BASE TABLE'""")
            .ToListAsync(OutboxHarness.Token);

        List<string> found = [];
        foreach (string table in tables)
        {
            // The table name comes from the catalog above, never from input; the text is a parameter.
#pragma warning disable EF1003
            long matches = await db.Database
                .SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM app.\"" + table + "\" t WHERE t::text LIKE '%' || {0} || '%'", text)
                .SingleAsync(OutboxHarness.Token);
#pragma warning restore EF1003
            if (matches > 0)
            {
                found.Add(table);
            }
        }

        return found;
    }
}
