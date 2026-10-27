using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime.Testing;

using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class EmailLinkRedemptionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Fact]
    public async Task A_confirmation_link_confirms_the_email()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Confirmation);
        Assert.False(await AccountsTestSupport.InScopeAsync(provider, id, (users, accountIdentity) => users.IsEmailConfirmedAsync(accountIdentity)));

        await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Confirmation, token);

        bool confirmed = await AccountsTestSupport.InScopeAsync(provider, id, (users, accountIdentity) => users.IsEmailConfirmedAsync(accountIdentity));
        Assert.True(confirmed);
    }

    [Fact]
    public async Task A_reset_link_sets_the_new_password()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);
        string newPassword = AccountsTestSupport.ValidPassword();

        await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, token, newPassword);

        Assert.True(await AccountsTestSupport.CheckPasswordAsync(provider, id, newPassword));
    }

    [Theory]
    [InlineData(EmailLinkKind.Confirmation)]
    [InlineData(EmailLinkKind.Reset)]
    public async Task A_second_use_of_a_link_fails(EmailLinkKind kind)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, kind);
        IdentityResult first = await EmailLinks.RedeemAsync(provider, id, kind, token);
        Assert.True(first.Succeeded);

        IdentityResult second = await EmailLinks.RedeemAsync(provider, id, kind, token);

        Assert.Equal(["InvalidToken"], EmailLinks.Codes(second));
    }

    // Both scopes check the link (loading its row) before either saves: the barrier holds each
    // SaveChanges until both have arrived. The first save deletes the row and the second finds
    // nothing left to change, which ASP.NET Core Identity reports as ConcurrencyFailure.
    [Fact]
    public async Task Two_uses_of_the_same_reset_link_at_once_let_exactly_one_succeed()
    {
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            EmailLinks.NewClock(), services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);
        string firstPassword = AccountsTestSupport.ValidPassword();
        string secondPassword = AccountsTestSupport.ValidPassword();
        barrier.Arm(2);

        IdentityResult[] results = await Task.WhenAll(
            EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, token, firstPassword),
            EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, token, secondPassword));

        string outcomes = string.Join(",", results.Select(result => result.Succeeded ? "Succeeded" : string.Join(",", EmailLinks.Codes(result))).Order());
        bool firstWon = results[0].Succeeded;
        bool firstWorks = await AccountsTestSupport.CheckPasswordAsync(provider, id, firstPassword);
        bool secondWorks = await AccountsTestSupport.CheckPasswordAsync(provider, id, secondPassword);
        Assert.Equal(("ConcurrencyFailure,Succeeded", firstWon, !firstWon), (outcomes, firstWorks, secondWorks));
    }

    [Fact]
    public async Task Another_identitys_link_is_rejected()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        Guid otherId = await NewIdentityAsync(provider);
        _ = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);
        string othersToken = await EmailLinks.IssueAsync(provider, otherId, EmailLinkKind.Reset);

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, othersToken);

        Assert.Equal(["InvalidToken"], EmailLinks.Codes(result));
    }

    [Theory]
    [InlineData(EmailLinkKind.Confirmation, EmailLinkKind.Reset)]
    [InlineData(EmailLinkKind.Reset, EmailLinkKind.Confirmation)]
    public async Task A_link_of_one_kind_is_rejected_for_the_other_kind(EmailLinkKind issued, EmailLinkKind used)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, issued);
        _ = await EmailLinks.IssueAsync(provider, id, used); // The row the token is tried against exists.

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, used, token);

        Assert.Equal(["InvalidToken"], EmailLinks.Codes(result));
    }

    public static TheoryData<NotCanonicalToken> NotCanonicalTokens => new()
    {
        new NotCanonicalToken("characters outside the Base64Url alphabet", _ => "not a token!"),
        new NotCanonicalToken("an empty token", _ => string.Empty),
        new NotCanonicalToken("one character short (31 bytes)", token => token[..^1]),
        new NotCanonicalToken("one character long (33 bytes)", token => token + "A"),
        new NotCanonicalToken("a last character that decodes to the same bytes", token => token[..^1] + SameBytesLastCharacter(token[^1])),
    };

    // characterization: EmailLinkTokenProvider.cs:129-142 (only the canonical 43-character Base64Url form is accepted)
    [Theory]
    [MemberData(nameof(NotCanonicalTokens))]
    public async Task A_token_that_is_not_the_canonical_form_is_rejected_without_throwing(NotCanonicalToken alteration)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, alteration.Apply(token));

        Assert.Equal(["InvalidToken"], EmailLinks.Codes(result));
    }

    [Fact]
    public async Task A_rejected_new_password_does_not_use_up_the_reset_link()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);
        IdentityResult rejected = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, token, "Abcdefgh-123x"); // 13 characters
        Assert.Equal(["PasswordTooShort"], EmailLinks.Codes(rejected));

        IdentityResult accepted = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, token);

        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task A_link_issued_by_one_provider_works_on_a_new_provider()
    {
        FakeClock clock = EmailLinks.NewClock();
        Guid id;
        string token;
        await using (ServiceProvider issuing = fixture.BuildAccountServices(clock))
        {
            id = await NewIdentityAsync(issuing);
            token = await EmailLinks.IssueAsync(issuing, id, EmailLinkKind.Reset);
        }

        // A new provider on the same database and clock stands in for the restarted Api.
        await using ServiceProvider restarted = fixture.BuildAccountServices(clock);

        IdentityResult result = await EmailLinks.RedeemAsync(restarted, id, EmailLinkKind.Reset, token);

        Assert.True(result.Succeeded);
    }

    // characterization: EmailLinkTokenProvider.cs:55 (a link of another purpose is not supported, even when only checked)
    [Fact]
    public async Task Verifying_a_link_for_an_unsupported_purpose_throws()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);

        await Assert.ThrowsAsync<NotSupportedException>(() => AccountsTestSupport.InScopeAsync(
            provider,
            id,
            (users, accountIdentity) => users.VerifyUserTokenAsync(accountIdentity, EmailLinkTokenProvider.ProviderName, "other", "token")));
    }

    // The last of the 43 characters carries 4 bits of the hash and 2 unused ones, so flipping its lowest bit leaves the decoded bytes alone.
    private static char SameBytesLastCharacter(char last)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        return alphabet[alphabet.IndexOf(last, StringComparison.Ordinal) ^ 1];
    }

    private static async Task<Guid> NewIdentityAsync(ServiceProvider provider) =>
        (await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token)).IdentityId;
}
