using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class EmailLinkLifetimeTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const int ConfirmationSeconds = 86400; // 24 hours
    private const int ResetSeconds = 3600; // 1 hour

    [Theory]
    [InlineData(EmailLinkKind.Confirmation, ConfirmationSeconds)]
    [InlineData(EmailLinkKind.Reset, ResetSeconds)]
    public async Task A_link_works_one_microsecond_before_its_lifetime_ends(EmailLinkKind kind, int lifetimeSeconds)
    {
        FakeClock clock = EmailLinks.NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, kind);
        clock.Advance(Duration.FromSeconds(lifetimeSeconds) - Duration.FromNanoseconds(1000));

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, kind, token);

        Assert.True(result.Succeeded);
    }

    // characterization: EmailLinkTokenProvider.cs:63 (at the expiry instant the link no longer works)
    [Theory]
    [InlineData(EmailLinkKind.Confirmation, ConfirmationSeconds)]
    [InlineData(EmailLinkKind.Reset, ResetSeconds)]
    public async Task A_link_no_longer_works_at_the_instant_it_expires(EmailLinkKind kind, int lifetimeSeconds)
    {
        FakeClock clock = EmailLinks.NewClock();
        await using ServiceProvider provider = fixture.BuildAccountServices(clock);
        Guid id = await NewIdentityAsync(provider);
        string token = await EmailLinks.IssueAsync(provider, id, kind);
        clock.Advance(Duration.FromSeconds(lifetimeSeconds));

        IdentityResult result = await EmailLinks.RedeemAsync(provider, id, kind, token);

        Assert.Equal(["InvalidToken"], EmailLinks.Codes(result));
    }

    private static async Task<Guid> NewIdentityAsync(ServiceProvider provider) =>
        (await AccountsTestSupport.CreateAccountIdentityAsync(
            provider, AccountsTestSupport.UniqueEmail(), AccountsTestSupport.ValidPassword(), AccountsTestSupport.Token)).IdentityId;
}
