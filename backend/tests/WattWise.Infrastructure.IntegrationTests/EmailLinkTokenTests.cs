using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

using WattWise.Infrastructure.Identity;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

public class EmailLinkTokenTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string Confirmation = "EmailConfirmation";
    private const string Reset = "ResetPassword";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Every call below runs in a scope of its own, the way each request does.

    [Fact]
    public async Task A_confirmation_link_confirms_the_email_and_a_second_use_fails()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string token = await ConfirmationTokenAsync(provider, id);

        IdentityResult first = await ConfirmEmailAsync(provider, id, token);
        IdentityResult second = await ConfirmEmailAsync(provider, id, token);

        Assert.True(first.Succeeded, Describe(first));
        Assert.True(await InScopeAsync(provider, id, (users, identity) => users.IsEmailConfirmedAsync(identity)));
        Assert.Contains("InvalidToken", Codes(second));
        Assert.Equal(0, await RowCountAsync(provider, id, Confirmation));
    }

    [Fact]
    public async Task A_reset_link_sets_the_new_password_and_a_second_use_fails()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, string oldPassword) = await CreateIdentityAsync(provider);
        string token = await ResetTokenAsync(provider, id);
        string newPassword = AccountsTestSupport.ValidPassword();
        string otherPassword = AccountsTestSupport.ValidPassword();

        IdentityResult first = await ResetPasswordAsync(provider, id, token, newPassword);
        IdentityResult second = await ResetPasswordAsync(provider, id, token, otherPassword);

        Assert.True(first.Succeeded, Describe(first));
        Assert.Contains("InvalidToken", Codes(second));
        Assert.True(await CheckPasswordAsync(provider, id, newPassword));
        Assert.False(await CheckPasswordAsync(provider, id, oldPassword));
        Assert.False(await CheckPasswordAsync(provider, id, otherPassword));
        Assert.Equal(0, await RowCountAsync(provider, id, Reset));
    }

    [Fact]
    public async Task A_link_issued_before_a_restart_still_works()
    {
        FakeClock clock = NewClock();
        Guid id;
        string confirmationToken;
        string resetToken;
        await using (ServiceProvider issuing = fixture.BuildAccountServices(clock))
        {
            (id, _) = await CreateIdentityAsync(issuing);
            confirmationToken = await ConfirmationTokenAsync(issuing, id);
            resetToken = await ResetTokenAsync(issuing, id);
        }

        // A new provider on the same database and clock stands in for the restarted Api.
        await using ServiceProvider restarted = fixture.BuildAccountServices(clock);

        IdentityResult confirmed = await ConfirmEmailAsync(restarted, id, confirmationToken);
        IdentityResult reset = await ResetPasswordAsync(restarted, id, resetToken, AccountsTestSupport.ValidPassword());

        Assert.True(confirmed.Succeeded, Describe(confirmed));
        Assert.True(reset.Succeeded, Describe(reset));
    }

    [Theory]
    [InlineData(3540, true)] // 59 minutes
    [InlineData(3601, false)] // 1 hour and 1 second
    public async Task A_reset_link_lasts_one_hour(int secondsAfterIssue, bool expectedToWork)
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string token = await ResetTokenAsync(provider, id);
        clock.Advance(Duration.FromSeconds(secondsAfterIssue));

        IdentityResult result = await ResetPasswordAsync(provider, id, token, AccountsTestSupport.ValidPassword());

        Assert.Equal(expectedToWork, result.Succeeded);
        if (!expectedToWork)
        {
            Assert.Contains("InvalidToken", Codes(result));
        }
    }

    [Theory]
    [InlineData(82800, true)] // 23 hours
    [InlineData(86401, false)] // 24 hours and 1 second
    public async Task A_confirmation_link_lasts_24_hours(int secondsAfterIssue, bool expectedToWork)
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string token = await ConfirmationTokenAsync(provider, id);
        clock.Advance(Duration.FromSeconds(secondsAfterIssue));

        IdentityResult result = await ConfirmEmailAsync(provider, id, token);

        Assert.Equal(expectedToWork, result.Succeeded);
        if (!expectedToWork)
        {
            Assert.Contains("InvalidToken", Codes(result));
        }
    }

    [Fact]
    public async Task A_new_reset_link_replaces_the_older_one()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string older = await ResetTokenAsync(provider, id);
        clock.Advance(Duration.FromMinutes(1));
        string newer = await ResetTokenAsync(provider, id);

        IdentityResult withOlder = await ResetPasswordAsync(provider, id, older, AccountsTestSupport.ValidPassword());
        IdentityResult withNewer = await ResetPasswordAsync(provider, id, newer, AccountsTestSupport.ValidPassword());

        Assert.NotEqual(older, newer);
        Assert.Contains("InvalidToken", Codes(withOlder));
        Assert.True(withNewer.Succeeded, Describe(withNewer));
    }

    [Fact]
    public async Task A_new_link_of_one_kind_leaves_the_other_kind_alone()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string confirmationToken = await ConfirmationTokenAsync(provider, id);
        _ = await ResetTokenAsync(provider, id);

        IdentityResult confirmed = await ConfirmEmailAsync(provider, id, confirmationToken);

        Assert.True(confirmed.Succeeded, Describe(confirmed));
        Assert.Equal(1, await RowCountAsync(provider, id, Reset));
    }

    [Fact]
    public async Task A_rejected_new_password_does_not_use_up_the_link()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, string oldPassword) = await CreateIdentityAsync(provider);
        string token = await ResetTokenAsync(provider, id);

        IdentityResult rejected = await ResetPasswordAsync(provider, id, token, "short");

        Assert.False(rejected.Succeeded);
        Assert.Contains("PasswordTooShort", Codes(rejected));
        Assert.DoesNotContain("InvalidToken", Codes(rejected));
        Assert.Equal(1, await RowCountAsync(provider, id, Reset));
        Assert.True(await CheckPasswordAsync(provider, id, oldPassword));

        string newPassword = AccountsTestSupport.ValidPassword();
        IdentityResult accepted = await ResetPasswordAsync(provider, id, token, newPassword);

        Assert.True(accepted.Succeeded, Describe(accepted));
        Assert.True(await CheckPasswordAsync(provider, id, newPassword));
    }

    [Fact]
    public async Task An_altered_token_is_rejected_and_the_real_one_still_works()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string token = await ResetTokenAsync(provider, id);
        string[] altered =
        [
            (token[0] == 'A' ? 'B' : 'A') + token[1..], // one character changed
            token[..^1], // one character short
            token + "A", // one character long
            token[..^1] + SameBytesLastCharacter(token[^1]), // decodes to the same bytes, but is not the token issued
            "not a token!",
            string.Empty,
        ];

        List<string> problems = [];
        foreach (string candidate in altered)
        {
            IdentityResult? result = null;
            Exception? thrown = await Record.ExceptionAsync(async () =>
                result = await ResetPasswordAsync(provider, id, candidate, AccountsTestSupport.ValidPassword()));

            if (thrown is not null)
            {
                problems.Add($"\"{candidate}\" threw {thrown.GetType().Name}.");
            }
            else if (result is null || result.Succeeded || !Codes(result).Contains("InvalidToken"))
            {
                problems.Add($"\"{candidate}\" was not rejected as InvalidToken.");
            }
        }

        Assert.Empty(problems);
        IdentityResult real = await ResetPasswordAsync(provider, id, token, AccountsTestSupport.ValidPassword());

        Assert.True(real.Succeeded, Describe(real));
    }

    [Fact]
    public async Task Another_identitys_token_is_rejected()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        (Guid otherId, _) = await CreateIdentityAsync(provider);
        _ = await ResetTokenAsync(provider, id);
        string othersToken = await ResetTokenAsync(provider, otherId);

        IdentityResult result = await ResetPasswordAsync(provider, id, othersToken, AccountsTestSupport.ValidPassword());

        Assert.False(result.Succeeded);
        Assert.Contains("InvalidToken", Codes(result));
        Assert.Equal(1, await RowCountAsync(provider, otherId, Reset));
    }

    [Fact]
    public async Task A_confirmation_token_cannot_be_used_for_a_reset_and_the_other_way_round()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        string confirmationToken = await ConfirmationTokenAsync(provider, id);
        string resetToken = await ResetTokenAsync(provider, id);

        IdentityResult resetWithConfirmation = await ResetPasswordAsync(
            provider, id, confirmationToken, AccountsTestSupport.ValidPassword());
        IdentityResult confirmWithReset = await ConfirmEmailAsync(provider, id, resetToken);

        Assert.Contains("InvalidToken", Codes(resetWithConfirmation));
        Assert.Contains("InvalidToken", Codes(confirmWithReset));
        Assert.Equal(1, await RowCountAsync(provider, id, Confirmation));
        Assert.Equal(1, await RowCountAsync(provider, id, Reset));
    }

    // Both scopes check the link (loading its row) before either saves: the barrier holds each
    // SaveChanges until both have arrived. The first save then deletes the row and the second finds
    // nothing left to change, which Identity reports as ConcurrencyFailure.
    [Fact]
    public async Task Two_uses_of_the_same_reset_link_at_once_let_exactly_one_succeed()
    {
        FakeClock clock = NewClock();
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            clock, services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        (Guid id, _) = await CreateIdentityAsync(provider);
        string token = await ResetTokenAsync(provider, id);
        string firstPassword = AccountsTestSupport.ValidPassword();
        string secondPassword = AccountsTestSupport.ValidPassword();
        barrier.Arm(2);

        IdentityResult[] results = await Task.WhenAll(
            ResetPasswordAsync(provider, id, token, firstPassword),
            ResetPasswordAsync(provider, id, token, secondPassword));

        Assert.Equal(2, barrier.Arrived);
        IdentityResult winner = Assert.Single(results, result => result.Succeeded);
        IdentityResult loser = Assert.Single(results, result => !result.Succeeded);
        Assert.Contains("ConcurrencyFailure", Codes(loser));
        bool firstWon = results[0].Succeeded;
        Assert.Equal(firstWon, await CheckPasswordAsync(provider, id, firstPassword));
        Assert.Equal(!firstWon, await CheckPasswordAsync(provider, id, secondPassword));
        Assert.Equal(0, await RowCountAsync(provider, id, Reset));
    }

    [Fact]
    public async Task The_stored_row_holds_a_32_byte_hash_and_never_the_token()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);

        string token = await ResetTokenAsync(provider, id);

        Assert.Equal(43, token.Length);
        byte[] tokenBytes = Base64Url.DecodeFromChars(token);
        Assert.Equal(32, tokenBytes.Length);
        byte[] stored = await StoredHashAsync(provider, id, Reset);
        Assert.Equal(32, stored.Length);
        Assert.Equal(SHA256.HashData(tokenBytes), stored);
        Assert.NotEqual(tokenBytes, stored);
        Assert.Equal(1, await RowCountAsync(provider, id, Reset));
    }

    [Fact]
    public async Task Deleting_the_identity_deletes_its_rows()
    {
        FakeClock clock = NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        (Guid id, _) = await CreateIdentityAsync(provider);
        (Guid otherId, _) = await CreateIdentityAsync(provider);
        _ = await ConfirmationTokenAsync(provider, id);
        _ = await ResetTokenAsync(provider, id);
        _ = await ResetTokenAsync(provider, otherId);
        Assert.Equal(2, await RowCountAsync(provider, id));

        IdentityResult deleted = await InScopeAsync(provider, id, (users, identity) => users.DeleteAsync(identity));

        Assert.True(deleted.Succeeded, Describe(deleted));
        Assert.Equal(0, await RowCountAsync(provider, id));
        Assert.Equal(1, await RowCountAsync(provider, otherId));
    }

    // The last of the 43 characters carries 4 bits of the hash and 2 unused ones, so flipping its lowest bit leaves the decoded bytes alone.
    private static char SameBytesLastCharacter(char last)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        return alphabet[alphabet.IndexOf(last, StringComparison.Ordinal) ^ 1];
    }

    private static FakeClock NewClock() => new(Instant.FromUtc(2026, 10, 26, 12, 0));

    private static string[] Codes(IdentityResult result) => [.. result.Errors.Select(error => error.Code)];

    private static string Describe(IdentityResult result) => string.Join(", ", Codes(result));

    private static async Task<(Guid Id, string Password)> CreateIdentityAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        string password = AccountsTestSupport.ValidPassword();
        AccountIdentity identity = await AccountsTestSupport.CreateIdentityAsync(
            scope.ServiceProvider, AccountsTestSupport.UniqueEmail(), password, Token);
        return (identity.Id, password);
    }

    private static async Task<T> InScopeAsync<T>(
        ServiceProvider provider, Guid id, Func<UserManager<AccountIdentity>, AccountIdentity, Task<T>> action)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = await users.FindByIdAsync(id.ToString())
            ?? throw new InvalidOperationException("The identity does not exist.");
        return await action(users, identity);
    }

    private static Task<string> ConfirmationTokenAsync(ServiceProvider provider, Guid id) =>
        InScopeAsync(provider, id, (users, identity) => users.GenerateEmailConfirmationTokenAsync(identity));

    private static Task<string> ResetTokenAsync(ServiceProvider provider, Guid id) =>
        InScopeAsync(provider, id, (users, identity) => users.GeneratePasswordResetTokenAsync(identity));

    private static Task<IdentityResult> ConfirmEmailAsync(ServiceProvider provider, Guid id, string token) =>
        InScopeAsync(provider, id, (users, identity) => users.ConfirmEmailAsync(identity, token));

    private static Task<IdentityResult> ResetPasswordAsync(
        ServiceProvider provider, Guid id, string token, string newPassword) =>
        InScopeAsync(provider, id, (users, identity) => users.ResetPasswordAsync(identity, token, newPassword));

    private static Task<bool> CheckPasswordAsync(ServiceProvider provider, Guid id, string password) =>
        InScopeAsync(provider, id, (users, identity) => users.CheckPasswordAsync(identity, password));

    private static async Task<int> RowCountAsync(ServiceProvider provider, Guid id, string? purpose = null)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM app.email_link_tokens
                WHERE identity_id = {id} AND ({purpose}::text IS NULL OR purpose = {purpose})
                """)
            .SingleAsync(Token);
    }

    private static async Task<byte[]> StoredHashAsync(ServiceProvider provider, Guid id, string purpose)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<byte[]>($"""
                SELECT token_hash AS "Value" FROM app.email_link_tokens
                WHERE identity_id = {id} AND purpose = {purpose}
                """)
            .SingleAsync(Token);
    }
}
