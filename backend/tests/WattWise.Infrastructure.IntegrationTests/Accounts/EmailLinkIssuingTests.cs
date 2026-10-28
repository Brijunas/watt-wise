using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using NodaTime.Testing;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class EmailLinkIssuingTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    [Theory]
    [InlineData(EmailLinkKind.Confirmation)]
    [InlineData(EmailLinkKind.Reset)]
    public async Task The_stored_hash_is_the_SHA256_of_the_token_bytes(EmailLinkKind kind)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);

        string token = await EmailLinks.IssueAsync(provider, id, kind);

        byte[] stored = await EmailLinks.StoredHashAsync(provider, id, kind);
        Assert.Equal(SHA256.HashData(Base64Url.DecodeFromChars(token)), stored);
    }

    [Fact]
    public async Task A_token_is_43_Base64Url_characters_of_32_bytes()
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);

        string token = await EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset);

        Assert.Equal((43, 32), (token.Length, Base64Url.DecodeFromChars(token).Length));
    }

    [Theory]
    [InlineData(EmailLinkKind.Confirmation)]
    [InlineData(EmailLinkKind.Reset)]
    public async Task A_new_link_replaces_the_older_one_of_its_kind(EmailLinkKind kind)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string older = await EmailLinks.IssueAsync(provider, id, kind);
        string newer = await EmailLinks.IssueAsync(provider, id, kind);

        IdentityResult withOlder = await EmailLinks.RedeemAsync(provider, id, kind, older);
        IdentityResult withNewer = await EmailLinks.RedeemAsync(provider, id, kind, newer);

        Assert.Equal(("InvalidToken", true), (string.Join(",", EmailLinks.Codes(withOlder)), withNewer.Succeeded));
    }

    [Theory]
    [InlineData(EmailLinkKind.Confirmation, EmailLinkKind.Reset)]
    [InlineData(EmailLinkKind.Reset, EmailLinkKind.Confirmation)]
    public async Task A_new_link_of_one_kind_leaves_the_other_kind_working(EmailLinkKind first, EmailLinkKind second)
    {
        await using ServiceProvider provider = fixture.BuildAccountServices(EmailLinks.NewClock());
        Guid id = await NewIdentityAsync(provider);
        string firstToken = await EmailLinks.IssueAsync(provider, id, first);
        _ = await EmailLinks.IssueAsync(provider, id, second);

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, first, firstToken);

        Assert.True(result.Succeeded);
    }

    // Both calls reach the INSERT before either runs it: the barrier holds each until both are there.
    // With a read-then-insert, both would see no row and the second insert would fail on the primary
    // key. The upsert runs both, the later one replacing the earlier, so only one token works.
    [Fact]
    public async Task Two_links_requested_at_once_leave_exactly_one_working_link()
    {
        FakeClock clock = EmailLinks.NewClock();
        CommandBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            clock, services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        Guid id = await NewIdentityAsync(provider);
        barrier.Arm("email_link_tokens", 2);
        string[] tokens = await Task.WhenAll(
            EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset), EmailLinks.IssueAsync(provider, id, EmailLinkKind.Reset));

        IdentityResult first = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, tokens[0]);
        IdentityResult second = await EmailLinks.RedeemAsync(provider, id, EmailLinkKind.Reset, tokens[1]);

        string outcomes = string.Join(
            ",", new[] { first, second }.Select(result => result.Succeeded ? "Succeeded" : string.Join(",", EmailLinks.Codes(result))).Order());
        Assert.Equal("InvalidToken,Succeeded", outcomes);
    }

    private static async Task<Guid> NewIdentityAsync(ServiceProvider provider) =>
        (await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), Token)).IdentityId;
}
